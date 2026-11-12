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
  ClinicianTrackerProbe stream [--serial S] [--hz 250] [--out logs] [--port 47800] [--adb <adb.exe>|none] [--hand right|left]
                               [--dashboard 47801] [--browser yes|no] [--scrcpy <scrcpy.exe>] [--commands <file>]

list    shows every SteamVR device and its identity.
live    prints the clinician tracker pose and logs it until Ctrl+C (or --seconds).
test    guided ~1 minute movement test (hardware test 1); writes summary.json. Pass the
        distances/angle you will actually measure out so the report can compare them.
stream  sends the pose to the Quest clinician test app over USB (adb reverse), serves the researcher dashboard
        and survives SteamVR restarts. Keys: C calibrate  V confirm  K marker  0 off  1 congruent  2 reflected
        X clear calibration  A redo adb reverse  Q quit";
        internal static object Metadata(OpenVrTracker vr,DeviceInfo tracker,string mode,double hz)=>new{
            schema=1,tool="ClinicianTrackerProbe",version=typeof(Program).Assembly.GetName().Version?.ToString(3),mode,
            startUtc=DateTime.UtcNow.ToString("o"),stopwatchFrequency=Stopwatch.Frequency,pollHz=hz,
            openvr=new{runtimePath=vr.RuntimePath,hmdPresentAtStart=vr.HmdPresentAtStart,systemInterface=OpenVR.IVRSystem_Version,applicationType="VRApplication_Background"},
            tracker,devices=vr.Devices(),conventions=ProbeLog.Conventions(),
            scope="Clinician tracker only. No participant identifiers or participant tracking data are recorded by this tool."};
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
            if(mode=="stream")return StreamMode.Run(options,serial,outDir,hz);
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
                var metadata=Metadata(vr,tracker,mode,hz);
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
