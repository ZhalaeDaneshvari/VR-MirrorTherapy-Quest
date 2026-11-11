using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace QuestResearch.ClinicianTracker
{
    // Clinician-tracker records only. Kept apart from participant movement data by folder and schema.
    // Default folder is ./logs (ignored by Git); nothing here is uploaded anywhere.
    public sealed class ProbeLog:IDisposable
    {
        public static readonly JsonSerializerOptions Json=new JsonSerializerOptions{
            PropertyNamingPolicy=JsonNamingPolicy.CamelCase,DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull,IncludeFields=true,
            NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
        static readonly JsonSerializerOptions Indented=new JsonSerializerOptions(Json){WriteIndented=true};
        readonly StreamWriter writer;readonly Stopwatch flushClock=Stopwatch.StartNew();
        public string Folder{get;}public long Samples{get;private set;}public long Events{get;private set;}
        public ProbeLog(string root,string mode,object metadata)
        {
            Folder=Path.GetFullPath(Path.Combine(root,DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'")+"-"+mode));
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder,"session.json"),JsonSerializer.Serialize(metadata,Indented)+"\n",new UTF8Encoding(false));
            writer=new StreamWriter(new FileStream(Path.Combine(Folder,"tracker.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false),1<<16);
        }
        public static object SampleRecord(TrackerSample s)=>new{
            type="sample",seq=s.Seq,hostTicks=s.HostTicks,hostSeconds=s.HostSeconds,utcUnixMs=s.UtcUnixMs,
            vsyncFrame=s.HasVsync?(long)s.VsyncFrame:-1,secondsSinceVsync=s.HasVsync?s.SecondsSinceVsync:-1,
            device=new{index=s.DeviceIndex,serial=s.Serial},connected=s.Connected,trackingResult=s.Result.ToString(),
            raw=new{valid=s.Raw.Valid,position=s.Raw.Position,rotation=s.Raw.Rotation,velocity=s.Raw.Velocity,angularVelocity=s.Raw.AngularVelocity,changed=s.RawChanged,unchangedSeconds=s.RawUnchangedSeconds},
            standing=new{valid=s.Standing.Valid,position=s.Standing.Position,rotation=s.Standing.Rotation}};
        public void Sample(TrackerSample s){Line(SampleRecord(s));Samples++;}
        public void Event(long ticks,string name,object detail=null)
        {
            Line(new{type="event",hostTicks=ticks,hostSeconds=ticks/(double)Stopwatch.Frequency,utcUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),@event=name,detail});
            Events++;writer.Flush();
        }
        void Line(object record)
        {
            writer.WriteLine(JsonSerializer.Serialize(record,Json));
            if(flushClock.ElapsedMilliseconds>=1000){writer.Flush();flushClock.Restart();}
        }
        public void WriteJson(string name,object value)=>File.WriteAllText(Path.Combine(Folder,name),JsonSerializer.Serialize(value,Indented)+"\n",new UTF8Encoding(false));
        public void Close(string reason,double seconds)
        {
            writer.Flush();writer.Dispose();
            WriteJson("closed.json",new{status="closed",reason,samples=Samples,events=Events,durationSeconds=seconds});
        }
        public void Dispose()=>writer.Dispose();
        public static Dictionary<string,object> Conventions()=>new Dictionary<string,object>{
            ["frame"]="OpenVR: right-handed, +Y up, -Z forward; positions in metres; quaternions x,y,z,w. No conversion to Unity applied.",
            ["raw"]="TrackingUniverseRawAndUncalibrated: the tracker driver's own frame, independent of the headset's play-space calibration.",
            ["standing"]="TrackingUniverseStanding: SteamVR play space as calibrated for the current headset (Quest Link). Recorded for comparison only.",
            ["prediction"]="Poses requested with 0 s prediction: vrserver's estimate at call time. OpenVR provides no sensor exposure timestamp to client applications.",
            ["hostTicks"]="Stopwatch (QueryPerformanceCounter) ticks since probe start, taken immediately before the pose call; frequency in metadata.",
            ["changed"]="raw.changed=false means the raw matrix is bit-identical to the previous sample; unchangedSeconds is time since it last changed. vrserver extrapolates between driver updates, so a changing pose does NOT show that the tracker delivered a new measurement; the driver's own update rate is not observable through OpenVR. A frozen pose (large unchangedSeconds) does indicate a stalled source.",
            ["invalid"]="Numbers with valid=false are what OpenVR returned and are not observations.",
            ["vsync"]="vsyncFrame/secondsSinceVsync come from IVRSystem.GetTimeSinceLastVsync; -1 when unavailable (observed for this background client even with Quest Link active)."};
    }
}
