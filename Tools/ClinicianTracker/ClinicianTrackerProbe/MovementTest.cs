using System;
using System.Collections.Generic;
using System.Linq;
using Valve.VR;
namespace QuestResearch.ClinicianTracker
{
    // Hardware test 1: timed, spoken-free prompts with beeps so the tester can watch the tracker.
    // Checks only that real, correctly-oriented movement reaches the probe; it is not an accuracy test.
    public sealed class MovementTest
    {
        sealed class Phase{public string Key,Prompt;public double Seconds;public readonly List<TrackerSample> Samples=new List<TrackerSample>();public double Start;}
        sealed class Rest{public bool Found,Settled;public int Count;public double[] Position,Rotation;public double RmsMm,MaxMm;}
        const double RestWindow=2.0,SettledMetres=0.01;
        readonly Phase[] phases={
            new Phase{Key="prepare",Seconds=6,Prompt="Put the tracker flat on the table, cameras facing the room. Take your hands away."},
            new Phase{Key="still",Seconds=6,Prompt="Hands off. Measuring resting noise."},
            new Phase{Key="slide",Seconds=10,Prompt="Slide it about 30 cm sideways along the table. Let go and keep still."},
            new Phase{Key="lift",Seconds=10,Prompt="Lift it about 20 cm straight up and hold it still in the air."},
            new Phase{Key="turn",Seconds=12,Prompt="Put it back on the table and turn it about 90 degrees in place, like a dial. Let go."},
            new Phase{Key="cover",Seconds=10,Prompt="Cover the tracker completely (cupped hands, a cloth or a box) and keep it covered."},
            new Phase{Key="uncover",Seconds=10,Prompt="Uncover it and leave it still on the table."}};
        int current=-1;double start=double.NaN;double lastPrint;
        public double TotalSeconds=>phases.Sum(p=>p.Seconds);
        public bool OnTick(TrackerSample s,double now)
        {
            if(double.IsNaN(start))start=now;
            double t=now-start,acc=0;int index=phases.Length;
            for(int i=0;i<phases.Length;i++){if(t<acc+phases[i].Seconds){index=i;break;}acc+=phases[i].Seconds;}
            if(index>=phases.Length){Console.WriteLine();return false;}
            if(index!=current)
            {
                current=index;var p=phases[index];p.Start=now;
                Console.WriteLine();Console.WriteLine();
                Console.WriteLine("=== "+(index+1)+"/"+phases.Length+"  "+p.Key.ToUpperInvariant()+" ("+p.Seconds+" s) ===");
                Console.WriteLine(p.Prompt);
                try{Console.Beep(index==0?660:880,250);}catch(Exception){}
            }
            var ph=phases[current];if(s!=null)ph.Samples.Add(s);
            if(now-lastPrint>=0.25)
            {
                lastPrint=now;double left=acc+ph.Seconds-t;
                Console.Write("\r  "+left.ToString("0")+" s left  "+(s==null?"tracker missing in SteamVR".PadRight(60):Live.Line(s))+"   ");
            }
            return true;
        }
        static Rest RestOf(Phase p)
        {
            var window=p.Samples.Where(s=>s.Raw.Valid&&s.HostSeconds>=p.Start+p.Seconds-RestWindow).ToList();
            var r=new Rest{Count=window.Count};if(window.Count<10)return r;
            var positions=window.Select(s=>s.Raw.Position).ToList();
            r.Found=true;r.Position=PoseMath.Mean(positions);r.Rotation=PoseMath.MeanRotation(window.Select(s=>s.Raw.Rotation).ToList());
            r.RmsMm=PoseMath.Rms(positions,r.Position)*1000;r.MaxMm=PoseMath.MaxDeviation(positions,r.Position)*1000;r.Settled=r.MaxMm/1000<SettledMetres;
            return r;
        }
        static object Rate(Phase p)
        {
            if(p.Samples.Count<2)return new{samples=p.Samples.Count};
            double span=p.Samples[^1].HostSeconds-p.Samples[0].HostSeconds;
            var gaps=p.Samples.Zip(p.Samples.Skip(1),(a,b)=>(b.HostSeconds-a.HostSeconds)*1000).ToList();
            return new{samples=p.Samples.Count,pollHz=Math.Round((p.Samples.Count-1)/span,1),distinctRawPosesPerSecond=Math.Round(p.Samples.Count(s=>s.RawChanged)/span,1),
                maxPollGapMs=Math.Round(gaps.Max(),2),maxUnchangedSeconds=Math.Round(p.Samples.Max(s=>s.RawUnchangedSeconds),3)};
        }
        static double ValidFraction(Phase p)=>p.Samples.Count==0?0:p.Samples.Count(s=>s.Raw.Valid&&s.Connected)/(double)p.Samples.Count;
        static double R(double v,int digits=1)=>Math.Round(v,digits);
        public object Summarise(DeviceInfo device,out bool passed,out List<string> lines)
        {
            Phase P(string key)=>phases.First(p=>p.Key==key);
            Rest still=RestOf(P("still")),slide=RestOf(P("slide")),lift=RestOf(P("lift")),turn=RestOf(P("turn")),uncover=RestOf(P("uncover"));
            var checks=new List<object>();var output=new List<string>();lines=output;bool all=true;
            void Check(string name,bool? ok,string evidence,string expectation)
            {
                checks.Add(new{name,result=ok==null?"inconclusive":ok.Value?"pass":"fail",evidence,expectation});
                output.Add((ok==null?"[ ?? ]":ok.Value?"[PASS]":"[FAIL]")+" "+name+": "+evidence+"  (expect "+expectation+")");
                if(ok!=true)all=false;
            }
            Check("identity",device.Serial!=null&&device.DeviceClass=="GenericTracker",
                "serial "+device.Serial+", model "+device.Model+", tracking system "+device.TrackingSystem,"one generic tracker with a serial number");
            double sv=ValidFraction(P("still"));
            Check("valid while resting",still.Found?sv>=0.99:(bool?)false,
                R(sv*100)+"% valid; noise "+(still.Found?R(still.RmsMm,2)+" mm RMS, "+R(still.MaxMm,2)+" mm max":"n/a"),">=99% valid");
            bool? Settled(params Rest[] r)=>r.All(x=>x.Found&&x.Settled)?true:(bool?)null;
            if(still.Found&&slide.Found)
            {
                double h=PoseMath.Horizontal(slide.Position,still.Position),v=slide.Position[1]-still.Position[1];
                Check("slide seen as horizontal movement",Settled(still,slide)==null?null:h>=0.10&&Math.Abs(v)<0.05,
                    R(h*100)+" cm horizontal, "+R(v*100)+" cm vertical","~30 cm horizontal (>=10), |vertical| < 5 cm");
            }else Check("slide seen as horizontal movement",null,"no settled valid rest pose","~30 cm horizontal");
            if(slide.Found&&lift.Found)
            {
                double v=lift.Position[1]-slide.Position[1];
                Check("lift seen as +Y (up)",Settled(slide,lift)==null?null:v>=0.08,R(v*100)+" cm vertical, "+R(PoseMath.Horizontal(lift.Position,slide.Position)*100)+" cm horizontal","~+20 cm (>=+8)");
            }else Check("lift seen as +Y (up)",null,"no settled valid rest pose","~+20 cm");
            if(slide.Found&&turn.Found)
            {
                double a=PoseMath.AngleDegrees(slide.Rotation,turn.Rotation),tilt=PoseMath.AxisTiltFromVerticalDegrees(slide.Rotation,turn.Rotation);
                Check("turn seen as rotation about vertical",Settled(slide,turn)==null?null:a>=45&&tilt<=30,R(a)+" deg, axis "+R(tilt)+" deg from vertical","~90 deg (>=45) about a near-vertical axis (<=30 deg)");
            }else Check("turn seen as rotation about vertical",null,"no settled valid rest pose","~90 deg");
            var cover=P("cover");var results=cover.Samples.Select(s=>s.Result.ToString()+(s.Raw.Valid?"":"/invalid")).Distinct().ToList();
            var firstLoss=cover.Samples.FirstOrDefault(s=>!s.Raw.Valid||!s.Connected||s.Result!=ETrackingResult.Running_OK);
            double drift=turn.Found?cover.Samples.Where(s=>s.Raw.Valid).Select(s=>PoseMath.Distance(s.Raw.Position,turn.Position)).DefaultIfEmpty(0).Max():double.NaN;
            var un=P("uncover");var firstOk=un.Samples.FirstOrDefault(s=>s.Raw.Valid&&s.Connected&&s.Result==ETrackingResult.Running_OK);
            lines.Add("[INFO] covered: statuses "+string.Join(", ",results)+(firstLoss==null?"; no loss reported":"; first loss "+R(firstLoss.HostSeconds-cover.Start,2)+" s after prompt")
                +"; max displacement while still valid "+(double.IsNaN(drift)?"n/a":R(drift*100)+" cm (includes any physical movement while covering)"));
            // Invalid poses arrive as zeros, so a tracker that never recovers must fail the test, not pass quietly.
            Check("recovers after uncovering",firstOk!=null,
                firstOk==null?"no valid Running_OK sample in "+un.Seconds+" s":"Running_OK "+R(Math.Max(0,firstOk.HostSeconds-un.Start),2)+" s after prompt"
                +(uncover.Found&&turn.Found?"; rest position differs from pre-cover by "+R(PoseMath.Distance(uncover.Position,turn.Position)*100)+" cm":""),
                "valid Running_OK again within "+un.Seconds+" s");
            var rates=phases.ToDictionary(p=>p.Key,Rate);var stillRate=Rate(P("still"));
            lines.Add("[INFO] still-phase timing: "+System.Text.Json.JsonSerializer.Serialize(stillRate,ProbeLog.Json));
            passed=all;
            return new{schema=1,test="movement",passed,checks,
                covered=new{statuses=results,firstLossSeconds=firstLoss==null?(double?)null:firstLoss.HostSeconds-cover.Start,maxValidDriftMetres=double.IsNaN(drift)?(double?)null:drift},
                uncovered=new{firstRunningOkSeconds=firstOk==null?(double?)null:Math.Max(0,firstOk.HostSeconds-un.Start),
                    restOffsetFromPreCoverMetres=uncover.Found&&turn.Found?PoseMath.Distance(uncover.Position,turn.Position):(double?)null},
                rests=new{still,slide,lift,turn,uncover},timing=rates,
                note="Plausibility thresholds only detect missing or mis-oriented movement; they are not accuracy or latency acceptance criteria."};
        }
    }
}
