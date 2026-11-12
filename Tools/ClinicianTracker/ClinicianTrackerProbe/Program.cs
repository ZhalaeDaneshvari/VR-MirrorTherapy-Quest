using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Valve.VR;
namespace QuestResearch.ClinicianTracker
{
    static class Live
    {
        static string N(double v,string f="+0.000;-0.000")=>(v+0.0).ToString(f,CultureInfo.InvariantCulture);// +0.0 folds -0 into 0
        public static string Line(TrackerSample s)
        {
            var p=s.Raw.Position;var q=s.Raw.Rotation;var v=s.Raw.Velocity;double speed=Math.Sqrt(v[0]*v[0]+v[1]*v[1]+v[2]*v[2]);
            return (s.Result.ToString()+(s.Raw.Valid?"":" INVALID")+(s.Connected?"":" DISCONNECTED")).PadRight(22)
                +" raw x="+N(p[0])+" y="+N(p[1])+" z="+N(p[2])+" m  heading="+N(PoseMath.HeadingDegrees(q),"+000.0;-000.0")
                +" tilt="+N(PoseMath.TiltDegrees(q),"00.0")+"  speed="+N(speed,"0.00")+" m/s  same="+N(s.RawUnchangedSeconds*1000,"0")+" ms";
        }
    }
    static class Program
    {
        const string Usage=@"Clinician tracker probe (Windows, SteamVR). Read-only; never starts SteamVR.

  ClinicianTrackerProbe list
  ClinicianTrackerProbe live [--serial S] [--seconds N] [--hz 250] [--out logs]
  ClinicianTrackerProbe test [--serial S] [--hz 250] [--out logs] [--slide-cm 30] [--lift-cm 20] [--turn-deg 90]

list  shows every SteamVR device and its identity.
live  prints the clinician tracker pose and logs it until Ctrl+C (or --seconds).
  ClinicianTrackerProbe stream [--serial S] [--hz 250] [--out logs] [--port 47800] [--adb <adb.exe>|none] [--hand right|left]
                               [--dashboard 47801] [--browser yes|no] [--scrcpy <scrcpy.exe>]

list    shows every SteamVR device and its identity.
live    prints the clinician tracker pose and logs it until Ctrl+C (or --seconds).
test    guided ~1 minute movement test (hardware test 1); writes summary.json. Pass the
        distances/angle you will actually measure out so the report can compare them.
stream  sends the pose to the Quest clinician test app over USB (adb reverse) and takes researcher keys:
        C calibrate   V confirm alignment   K marker on/off   0 hand off   1 congruent   2 reflected
        X clear calibration   A redo adb reverse   Q quit";
        static readonly Dictionary<ConsoleKey,string> Keys=new Dictionary<ConsoleKey,string>{
            [ConsoleKey.C]="calibrate",[ConsoleKey.V]="verify",[ConsoleKey.K]="marker",[ConsoleKey.X]="clear",
            [ConsoleKey.D0]="mode-off",[ConsoleKey.NumPad0]="mode-off",[ConsoleKey.D1]="mode-congruent",[ConsoleKey.NumPad1]="mode-congruent",
            [ConsoleKey.D2]="mode-reflected",[ConsoleKey.NumPad2]="mode-reflected"};
        static readonly HashSet<string> Extra=new HashSet<string>{"calibrate-participant","trust"};
        static bool Allowed(string c)
        {
            if(Keys.ContainsValue(c)||Extra.Contains(c))return true;
            var m=System.Text.RegularExpressions.Regex.Match(c,"^participant(-confirm)?:([0-9]{1,2})$");
            return m.Success&&int.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture) is >=1 and <=20;
        }
        static int Stream(ProbeSession session,ProbeLog log,Stopwatch clock,DeviceInfo tracker,Dictionary<string,string> options)
        {
            int port=int.Parse(options.GetValueOrDefault("port","47800"),CultureInfo.InvariantCulture);
            string hand=options.GetValueOrDefault("hand","right");if(hand is not ("right" or "left"))throw new ArgumentException("--hand must be right or left");
            string adb=options.GetValueOrDefault("adb");
            string Clean(string v)=>(v??"").Replace(",", " ");
            string header="H,1,"+Clean(tracker.Serial)+","+Clean(tracker.Model)+","+Stopwatch.Frequency+","+hand;
            string reverse=StreamServer.Reverse(adb,port);Console.WriteLine(reverse);log.Event(clock.ElapsedTicks,"adbReverse",new{port,result=reverse});
            using var server=new StreamServer(port,header,clock);
            var inbox=new System.Collections.Concurrent.ConcurrentQueue<string>();server.Received+=inbox.Enqueue;
            Console.WriteLine("Listening on 127.0.0.1:"+port+". Start the clinician test app on the Quest. Keys: C V K 0 1 2 X A Q.");
            // Researcher dashboard (browser, this PC only): status snapshot, commands, live headset view.
            var gate=new object();var messages=new List<(string t,string text)>();string questJson=null;long questTicks=0;
            string pcTracker="";bool pcValid=false;int battery=-1;double lastBattery=-100;
            var dashQueue=new System.Collections.Concurrent.ConcurrentQueue<string>();Process viewer=null;
            void Note(string text){lock(gate){messages.Add((DateTime.Now.ToString("HH:mm:ss"),text));if(messages.Count>80)messages.RemoveAt(0);}}
            string StatusJson()
            {
                lock(gate)
                {
                    System.Text.Json.JsonElement? quest=null;
                    if(questJson!=null)try{quest=System.Text.Json.JsonDocument.Parse(questJson).RootElement.Clone();}catch(System.Text.Json.JsonException){}
                    return System.Text.Json.JsonSerializer.Serialize(new{pc=new{tracker=pcTracker,valid=pcValid,battery,serial=tracker.Serial,sent=server.Sent,dropped=server.Dropped},
                        headsetConnected=server.ClientConnected,questAgeMs=questJson==null?-1:(clock.ElapsedTicks-questTicks)*1000.0/Stopwatch.Frequency,quest,
                        messages=messages.ConvertAll(m=>new{m.t,m.text})});
                }
            }
            string DashCommand(string c)
            {
                c=c.Trim().ToLowerInvariant();if(!Allowed(c))return "Unknown command: "+c;
                if(!server.ClientConnected)return "Headset app not connected; nothing sent.";
                dashQueue.Enqueue(c);return "Sent to headset: "+c;
            }
            string OpenView()
            {
                string exe=options.GetValueOrDefault("scrcpy",System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"..","tools","scrcpy","scrcpy.exe")));
                if(!System.IO.File.Exists(exe))return "Headset view tool not found at "+exe;
                lock(gate)
                {
                    if(viewer!=null&&!viewer.HasExited)return "Headset view is already open.";
                    var info=new ProcessStartInfo(exe,"--select-usb --no-control --no-audio --max-size=1920 --max-fps=30 --window-title=\"Participant view (headset)\""){UseShellExecute=false};
                    string adbPath=StreamServer.FindAdb();if(adbPath!=null)info.Environment["ADB"]=adbPath;// same adb as the stream: no server restarts
                    try{viewer=Process.Start(info);}catch(Exception e){return "Could not start the headset view: "+e.Message;}
                }
                Note("Headset view opened (read-only, not recorded).");return "Headset view opening in a separate window.";
            }
            int dashPort=int.Parse(options.GetValueOrDefault("dashboard","47801"),CultureInfo.InvariantCulture);
            using var dashboard=new Dashboard(dashPort,System.IO.Path.Combine(AppContext.BaseDirectory,"dashboard.html"),StatusJson,DashCommand,OpenView);
            string url="http://localhost:"+dashPort+"/";Console.WriteLine("Researcher dashboard: "+url);log.Event(clock.ElapsedTicks,"dashboard",new{url});
            if(options.GetValueOrDefault("browser","yes")!="no")try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception){}
            double lastPrint=0,lastKeys=0,lastFile=0;bool keys=!Console.IsInputRedirected;
            double seconds=double.Parse(options.GetValueOrDefault("seconds","0"),CultureInfo.InvariantCulture);
            // Commands can also be appended as lines to a file (one per line, e.g. "calibrate"), so a helper
            // or another program can drive the session while the researcher wears the headset.
            string commandFile=System.IO.Path.GetFullPath(options.GetValueOrDefault("commands","stream-commands.txt"));
            long commandOffset=System.IO.File.Exists(commandFile)?new System.IO.FileInfo(commandFile).Length:0;
            Console.WriteLine("Command file: "+commandFile+" (append lines: "+string.Join(", ",new HashSet<string>(Keys.Values))+").");
            void Send(long now,string command,string source)
            {
                if(!server.ClientConnected){Console.WriteLine();Console.WriteLine("Quest app not connected; '"+command+"' not sent.");return;}
                server.Command(command);log.Event(now,"command",new{command,source});Console.WriteLine();Console.WriteLine("-> "+command+" ("+source+")");
            }
            session.Run(seconds,s=>
            {
                long now=clock.ElapsedTicks;double t=now/(double)Stopwatch.Frequency;
                server.Frame(s,now);
                if(t-lastFile>=0.1)
                {
                    lastFile=t;
                    try
                    {
                        long length=System.IO.File.Exists(commandFile)?new System.IO.FileInfo(commandFile).Length:0;
                        if(length<commandOffset)commandOffset=0;// file replaced
                        if(length>commandOffset)
                        {
                            using var stream=new System.IO.FileStream(commandFile,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.ReadWrite|System.IO.FileShare.Delete);
                            stream.Seek(commandOffset,System.IO.SeekOrigin.Begin);string text=new System.IO.StreamReader(stream).ReadToEnd();commandOffset=length;
                            foreach(var raw in text.Split('\n'))
                            {
                                string c=raw.Trim().ToLowerInvariant();if(c.Length==0)continue;
                                if(Allowed(c))Send(now,c,"file");
                                else{Console.WriteLine();Console.WriteLine("Ignored unknown command in file: "+c);}
                            }
                        }
                    }
                    catch(System.IO.IOException){}
                }
                while(dashQueue.TryDequeue(out var dc))Send(now,dc,"dashboard");
                while(inbox.TryDequeue(out var line))
                {
                    if(line.StartsWith("S,"))continue;// latest status is shown on the status line
                    if(line.StartsWith("J,")){lock(gate){questJson=line.Substring(2);questTicks=now;}continue;}// dashboard snapshot; the headset logs its own state
                    string text=line.StartsWith("L,")?line.Substring(2):line;Note(text);
                    log.Event(now,"quest",new{line});Console.WriteLine();Console.WriteLine("[Quest] "+text);
                }
                if(t-lastBattery>=10){lastBattery=t;var info=session.DescribeTracker();float? b=info?.BatteryPercent;lock(gate)battery=b==null?-1:(int)Math.Round(b.Value);}
                lock(gate){pcTracker=s==null?"missing":s.Result.ToString();pcValid=s!=null&&s.Raw.Valid&&s.Result==Valve.VR.ETrackingResult.Running_OK;}
                if(keys&&t-lastKeys>=0.05)
                {
                    lastKeys=t;
                    while(Console.KeyAvailable)
                    {
                        var key=Console.ReadKey(true).Key;
                        if(key is ConsoleKey.Q or ConsoleKey.Escape)return false;
                        if(key==ConsoleKey.A){string r=StreamServer.Reverse(adb,port);log.Event(now,"adbReverse",new{port,result=r});Console.WriteLine();Console.WriteLine(r);continue;}
                        if(Keys.TryGetValue(key,out var command))Send(now,command,"key");
                    }
                }
                if(t-lastPrint>=0.25)
                {
                    lastPrint=t;string tr=s==null?"tracker MISSING":(s.Result+(s.Raw.Valid?"":" INVALID"));
                    Console.Write("\r"+("PC: "+tr+" | sent "+server.Sent+" dropped "+server.Dropped+" | Quest: "+server.LastStatus).PadRight(150).Substring(0,150));
                }
                return true;
            });
            Console.WriteLine();
            log.Event(clock.ElapsedTicks,"streamSummary",new{sent=server.Sent,dropped=server.Dropped,connections=server.Connections});
            return 0;
        }
        static int Main(string[] args)
        {
            if(args.Length==0||args[0] is "-h" or "--help"){Console.WriteLine(Usage);return 0;}
            string mode=args[0];var options=new Dictionary<string,string>();
            for(int i=1;i<args.Length;i++){if(!args[i].StartsWith("--")||i+1>=args.Length){Console.Error.WriteLine("Bad argument: "+args[i]+"\n\n"+Usage);return 2;}options[args[i][2..]]=args[++i];}
            string serial=options.GetValueOrDefault("serial");string outDir=options.GetValueOrDefault("out","logs");
            double hz=double.Parse(options.GetValueOrDefault("hz","250"),CultureInfo.InvariantCulture);
            double seconds=double.Parse(options.GetValueOrDefault("seconds","0"),CultureInfo.InvariantCulture);
            double Positive(string key,string fallback){double v=double.Parse(options.GetValueOrDefault(key,fallback),CultureInfo.InvariantCulture);if(v<=0)throw new ArgumentException("--"+key+" must be > 0");return v;}
            double slideCm=Positive("slide-cm","30"),liftCm=Positive("lift-cm","20"),turnDeg=Positive("turn-deg","90");
            if(mode is not ("list" or "live" or "test" or "stream")){Console.Error.WriteLine("Unknown mode: "+mode+"\n\n"+Usage);return 2;}
            var clock=Stopwatch.StartNew();OpenVrTracker vr;
            try{vr=OpenVrTracker.Open(clock);}catch(Exception e){Console.Error.WriteLine(e.Message);return 3;}
            using(vr)
            {
                var devices=vr.Devices();
                if(mode=="list")
                {
                    Console.WriteLine("HMD present: "+vr.HmdPresentAtStart+"   runtime: "+vr.RuntimePath);
                    foreach(var d in devices)
                        Console.WriteLine("["+d.Index+"] "+d.DeviceClass+"  connected="+d.Connected+"  serial="+d.Serial+"  model="+d.Model+"  maker="+d.Manufacturer
                            +"  system="+d.TrackingSystem+"  type="+d.ControllerType+"  registered="+d.RegisteredDeviceType+"  role="+(d.SteamVRTrackerRole??d.ControllerRole)
                            +(d.BatteryPercent==null?"":"  battery="+d.BatteryPercent.Value.ToString("0")+"%"));
                    return 0;
                }
                var tracker=vr.FindTracker(serial,out string problem);
                if(tracker==null){Console.Error.WriteLine(problem);return 4;}
                Console.WriteLine("Clinician tracker: serial "+tracker.Serial+", "+tracker.Model+" ("+tracker.TrackingSystem+"), OpenVR index "+tracker.Index);
                var metadata=new{
                    schema=1,tool="ClinicianTrackerProbe",version=typeof(Program).Assembly.GetName().Version?.ToString(3),mode,
                    startUtc=DateTime.UtcNow.ToString("o"),stopwatchFrequency=Stopwatch.Frequency,pollHz=hz,
                    openvr=new{runtimePath=vr.RuntimePath,hmdPresentAtStart=vr.HmdPresentAtStart,systemInterface=OpenVR.IVRSystem_Version,applicationType="VRApplication_Background"},
                    tracker,devices,conventions=ProbeLog.Conventions(),
                    scope="Clinician tracker only. No participant identifiers or participant tracking data are recorded by this tool."};
                using var log=new ProbeLog(outDir,mode,metadata);
                var session=new ProbeSession(vr,log,clock,tracker,hz);
                log.Event(clock.ElapsedTicks,"start",new{mode,serial=tracker.Serial});
                Console.WriteLine("Logging to "+log.Folder);
                int code=0;
                if(mode=="live")
                {
                    Console.WriteLine("Ctrl+C to stop.");double last=0;
                    session.Run(seconds,s=>{double now=clock.Elapsed.TotalSeconds;if(now-last>=0.1){last=now;Console.Write("\r"+(s==null?"tracker missing in SteamVR".PadRight(100):Live.Line(s))+"  ");}return true;});
                    Console.WriteLine();
                }
                else if(mode=="stream")code=Stream(session,log,clock,tracker,options);
                else
                {
                    var test=new MovementTest(slideCm,liftCm,turnDeg);
                    Console.WriteLine("Hardware test 1 takes about "+test.TotalSeconds+" s. Follow the prompts; a beep marks each step.");
                    Console.WriteLine("Stated movement: slide "+slideCm+" cm, lift "+liftCm+" cm, turn "+turnDeg+" deg.");
                    if(!Console.IsInputRedirected){Console.Write("Press Enter to start...");Console.ReadLine();}
                    session.Run(0,s=>test.OnTick(s,clock.Elapsed.TotalSeconds));
                    if(session.StopReason=="complete")
                    {
                        var summary=test.Summarise(tracker,out bool passed,out var lines);log.WriteJson("summary.json",summary);
                        Console.WriteLine();Console.WriteLine("=== Hardware test 1 result: "+(passed?"PASS":"NOT PASSED")+" ===");
                        foreach(var l in lines)Console.WriteLine(l);
                        code=passed?0:1;
                    }
                    else{Console.WriteLine("Test interrupted ("+session.StopReason+"); no summary written.");code=1;}
                }
                log.Event(clock.ElapsedTicks,"stop",new{reason=session.StopReason});
                log.Close(session.StopReason,clock.Elapsed.TotalSeconds);
                Console.WriteLine("Saved "+log.Samples+" samples and "+log.Events+" events to "+log.Folder);
                return code;
            }
        }
    }
}
