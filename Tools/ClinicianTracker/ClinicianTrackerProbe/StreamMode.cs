using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Valve.VR;
namespace QuestResearch.ClinicianTracker
{
    // Stream mode for pilot sessions. The Quest link, researcher dashboard and command inputs stay up across SteamVR
    // restarts and tracker dropouts; OpenVR is reconnected automatically and each connection gets its own PC log.
    // While SteamVR or the tracker is unavailable the Quest receives "tracker missing" frames and hides the hand.
    static class StreamMode
    {
        internal static readonly Dictionary<ConsoleKey,string> Keys=new Dictionary<ConsoleKey,string>{
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
        public static int Run(Dictionary<string,string> options,string serial,string outDir,double hz)
        {
            int port=int.Parse(options.GetValueOrDefault("port","47800"),CultureInfo.InvariantCulture);
            string hand=options.GetValueOrDefault("hand","right");if(hand is not ("right" or "left"))throw new ArgumentException("--hand must be right or left");
            string adb=options.GetValueOrDefault("adb");
            string Clean(string v)=>(v??"").Replace(",", " ");
            var clock=Stopwatch.StartNew();
            using var server=new StreamServer(port,"H,1,,,"+Stopwatch.Frequency+","+hand,clock);// header completed once the tracker is found
            var inbox=new ConcurrentQueue<string>();server.Received+=inbox.Enqueue;
            string pendingReverse=StreamServer.Reverse(adb,port);Console.WriteLine(pendingReverse);
            Console.WriteLine("Listening on 127.0.0.1:"+port+". Start the clinician test app on the Quest. Keys: C V K 0 1 2 X A Q.");

            var gate=new object();var messages=new List<(string t,string text)>();string questJson=null;long questTicks=0;
            string steam="starting",pcTracker="starting",trackerSerial=serial??"";bool pcValid=false;int battery=-1;
            var dashQueue=new ConcurrentQueue<string>();Process viewer=null;
            ProbeLog log=null;ProbeSession session=null;bool quit=false;
            void Note(string text){lock(gate){messages.Add((DateTime.Now.ToString("HH:mm:ss"),text));if(messages.Count>80)messages.RemoveAt(0);}}
            void Event(string name,object detail)=>log?.Event(clock.ElapsedTicks,name,detail);
            string StatusJson()
            {
                lock(gate)
                {
                    JsonElement? quest=null;
                    if(questJson!=null)try{quest=JsonDocument.Parse(questJson).RootElement.Clone();}catch(JsonException){}
                    return JsonSerializer.Serialize(new{pc=new{steamvr=steam,tracker=pcTracker,valid=pcValid,battery,serial=trackerSerial,sent=server.Sent,dropped=server.Dropped},
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
                string exe=options.GetValueOrDefault("scrcpy",Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","tools","scrcpy","scrcpy.exe")));
                if(!File.Exists(exe))return "Headset view tool not found at "+exe;
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
            using var dashboard=new Dashboard(dashPort,Path.Combine(AppContext.BaseDirectory,"dashboard.html"),StatusJson,DashCommand,OpenView);
            string url="http://localhost:"+dashPort+"/";Console.WriteLine("Researcher dashboard: "+url);
            if(options.GetValueOrDefault("browser","yes")!="no")try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception){}
            Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;quit=true;session?.Stop("stopped-by-user");};

            // Commands can also be appended as lines to a file (one per line, e.g. "calibrate").
            string commandFile=Path.GetFullPath(options.GetValueOrDefault("commands","stream-commands.txt"));
            long commandOffset=File.Exists(commandFile)?new FileInfo(commandFile).Length:0;
            Console.WriteLine("Command file: "+commandFile);
            void Send(long now,string command,string source)
            {
                if(!server.ClientConnected){Console.WriteLine();Console.WriteLine("Quest app not connected; '"+command+"' not sent.");return;}
                server.Command(command);Event("command",new{command,source});Note("Researcher: "+command+" ("+source+")");Console.WriteLine();Console.WriteLine("-> "+command+" ("+source+")");
            }
            double lastPrint=0,lastKeys=0,lastFile=0,lastBattery=-100;bool keys=!Console.IsInputRedirected;
            bool Tick(TrackerSample s)
            {
                long now=clock.ElapsedTicks;double t=now/(double)Stopwatch.Frequency;
                server.Frame(s,now);
                if(t-lastFile>=0.1)
                {
                    lastFile=t;
                    try
                    {
                        long length=File.Exists(commandFile)?new FileInfo(commandFile).Length:0;
                        if(length<commandOffset)commandOffset=0;// file replaced
                        if(length>commandOffset)
                        {
                            using var stream=new FileStream(commandFile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                            stream.Seek(commandOffset,SeekOrigin.Begin);string text=new StreamReader(stream).ReadToEnd();commandOffset=length;
                            foreach(var raw in text.Split('\n')){string c=raw.Trim().ToLowerInvariant();if(c.Length==0)continue;if(Allowed(c))Send(now,c,"file");}
                        }
                    }
                    catch(IOException){}
                }
                while(dashQueue.TryDequeue(out var dc))Send(now,dc,"dashboard");
                while(inbox.TryDequeue(out var line))
                {
                    if(line.StartsWith("S,"))continue;// latest status is shown on the status line
                    if(line.StartsWith("J,")){lock(gate){questJson=line.Substring(2);questTicks=now;}continue;}// the headset logs its own state
                    string text=line.StartsWith("L,")?line.Substring(2):line;Note(text);
                    Event("quest",new{line});Console.WriteLine();Console.WriteLine("[Quest] "+text);
                }
                if(keys&&t-lastKeys>=0.05)
                {
                    lastKeys=t;
                    while(Console.KeyAvailable)
                    {
                        var key=Console.ReadKey(true).Key;
                        if(key is ConsoleKey.Q or ConsoleKey.Escape){quit=true;return false;}
                        if(key==ConsoleKey.A){string r=StreamServer.Reverse(adb,port);Event("adbReverse",new{port,result=r});Note(r);Console.WriteLine();Console.WriteLine(r);continue;}
                        if(Keys.TryGetValue(key,out var command))Send(now,command,"key");
                    }
                }
                if(session!=null&&t-lastBattery>=10){lastBattery=t;float? b=session.DescribeTracker()?.BatteryPercent;lock(gate)battery=b==null?-1:(int)Math.Round(b.Value);}
                if(session!=null)lock(gate){pcTracker=s==null?"missing":s.Result.ToString();pcValid=s!=null&&s.Raw.Valid&&s.Result==ETrackingResult.Running_OK;}
                if(t-lastPrint>=0.25)
                {
                    lastPrint=t;string tr;lock(gate)tr=session==null?pcTracker:s==null?"tracker MISSING":(s.Result+(s.Raw.Valid?"":" INVALID"));
                    Console.Write("\r"+("PC: "+tr+" | sent "+server.Sent+" dropped "+server.Dropped+" | Quest: "+server.LastStatus).PadRight(150).Substring(0,150));
                }
                return !quit;
            }
            void Idle(double seconds){double until=clock.Elapsed.TotalSeconds+seconds;while(!quit&&clock.Elapsed.TotalSeconds<until){if(!Tick(null))break;Thread.Sleep(4);}}

            while(!quit)
            {
                OpenVrTracker vr;
                try{vr=OpenVrTracker.Open(clock);}
                catch(Exception){lock(gate){steam="SteamVR not running";pcTracker="SteamVR not running";pcValid=false;}Idle(2);continue;}
                using(vr)
                {
                    var tracker=vr.FindTracker(serial,out string problem);
                    if(tracker==null){lock(gate){steam="running";pcTracker=problem;pcValid=false;}Idle(2);continue;}
                    lock(gate){steam="running";trackerSerial=tracker.Serial;}
                    server.SetHeader("H,1,"+Clean(tracker.Serial)+","+Clean(tracker.Model)+","+Stopwatch.Frequency+","+hand);
                    log=new ProbeLog(outDir,"stream",Program.Metadata(vr,tracker,"stream",hz));
                    session=new ProbeSession(vr,log,clock,tracker,hz);
                    log.Event(clock.ElapsedTicks,"start",new{mode="stream",serial=tracker.Serial});
                    if(pendingReverse!=null){log.Event(clock.ElapsedTicks,"adbReverse",new{port,result=pendingReverse});pendingReverse=null;}
                    log.Event(clock.ElapsedTicks,"dashboard",new{url});
                    Console.WriteLine();Console.WriteLine("SteamVR connected: tracker "+tracker.Serial+"; logging to "+log.Folder);Note("SteamVR connected: tracker "+tracker.Serial+".");
                    session.Run(0,Tick);
                    log.Event(clock.ElapsedTicks,"streamSummary",new{sent=server.Sent,dropped=server.Dropped,connections=server.Connections});
                    log.Event(clock.ElapsedTicks,"stop",new{reason=session.StopReason});
                    log.Close(session.StopReason,clock.Elapsed.TotalSeconds);
                    Console.WriteLine();Console.WriteLine("SteamVR session ended ("+session.StopReason+"); saved "+log.Samples+" samples to "+log.Folder);
                    if(session.StopReason=="steamvr-quit"){lock(gate){steam="SteamVR restarting";pcTracker="SteamVR not running";pcValid=false;}Note("SteamVR quit; waiting for it to come back.");}
                    log=null;session=null;
                }
                if(!quit)Idle(2);
            }
            return 0;
        }
    }
}
