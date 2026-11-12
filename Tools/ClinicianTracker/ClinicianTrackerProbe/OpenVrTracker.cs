using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Valve.VR;
namespace QuestResearch.ClinicianTracker
{
    public sealed class DeviceInfo
    {
        public uint Index{get;set;}public string DeviceClass{get;set;}public bool Connected{get;set;}
        public string Serial{get;set;}public string Model{get;set;}public string Manufacturer{get;set;}
        public string TrackingSystem{get;set;}public string ControllerType{get;set;}public string RegisteredDeviceType{get;set;}
        public string RenderModel{get;set;}public string DriverVersion{get;set;}public string ConnectedDongle{get;set;}
        public string ControllerRole{get;set;}public string SteamVRTrackerRole{get;set;}
        public bool? Wireless{get;set;}public float? BatteryPercent{get;set;}
    }
    public sealed class PoseReading
    {
        public bool Valid;public double[] Position,Rotation,Velocity,AngularVelocity;
    }
    public sealed class TrackerSample
    {
        public long Seq,HostTicks,UtcUnixMs;public double HostSeconds;
        public bool HasVsync;public ulong VsyncFrame;public float SecondsSinceVsync;
        public uint DeviceIndex;public string Serial;public bool Connected;public ETrackingResult Result;
        public PoseReading Raw,Standing;public bool RawChanged;public double RawUnchangedSeconds;
    }
    // Read-only OpenVR client. Background type: never starts SteamVR, never takes scene focus,
    // and exits when SteamVR asks it to.
    public sealed class OpenVrTracker:IDisposable
    {
        public const ETrackingUniverseOrigin RawUniverse=ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated;
        public const ETrackingUniverseOrigin StandingUniverse=ETrackingUniverseOrigin.TrackingUniverseStanding;
        readonly CVRSystem vr;
        readonly TrackedDevicePose_t[] raw=new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        readonly TrackedDevicePose_t[] standing=new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        readonly Stopwatch clock;HmdMatrix34_t lastRaw;bool haveLast;long lastChangeTicks;long seq;
        public bool HmdPresentAtStart{get;}public string RuntimePath{get;}
        OpenVrTracker(CVRSystem system,Stopwatch shared)
        {
            vr=system;clock=shared;
            try{HmdPresentAtStart=OpenVR.IsHmdPresent();}catch(Exception){HmdPresentAtStart=false;}
            try{RuntimePath=OpenVR.RuntimePath();}catch(Exception){RuntimePath=null;}
        }
        public static OpenVrTracker Open(Stopwatch clock)
        {
            var error=EVRInitError.None;var system=OpenVR.Init(ref error,EVRApplicationType.VRApplication_Background);
            if(error!=EVRInitError.None||system==null)
                throw new InvalidOperationException("OpenVR init failed: "+error+" ("+OpenVR.GetStringForHmdError(error)+"). Is SteamVR running?");
            return new OpenVrTracker(system,clock);
        }
        public void Dispose()=>OpenVR.Shutdown();
        string Text(uint i,ETrackedDeviceProperty p)
        {
            var error=ETrackedPropertyError.TrackedProp_Success;var sb=new StringBuilder(256);
            uint n=vr.GetStringTrackedDeviceProperty(i,p,sb,(uint)sb.Capacity,ref error);
            if(error==ETrackedPropertyError.TrackedProp_BufferTooSmall){sb=new StringBuilder((int)n);error=ETrackedPropertyError.TrackedProp_Success;vr.GetStringTrackedDeviceProperty(i,p,sb,n,ref error);}
            return error==ETrackedPropertyError.TrackedProp_Success?sb.ToString():null;
        }
        float? Float(uint i,ETrackedDeviceProperty p){var e=ETrackedPropertyError.TrackedProp_Success;float v=vr.GetFloatTrackedDeviceProperty(i,p,ref e);return e==ETrackedPropertyError.TrackedProp_Success?v:null;}
        bool? Bool(uint i,ETrackedDeviceProperty p){var e=ETrackedPropertyError.TrackedProp_Success;bool v=vr.GetBoolTrackedDeviceProperty(i,p,ref e);return e==ETrackedPropertyError.TrackedProp_Success?v:null;}
        static string Setting(string section,string key)
        {
            try{var e=EVRSettingsError.None;var sb=new StringBuilder(256);OpenVR.Settings.GetString(section,key,sb,(uint)sb.Capacity,ref e);return e==EVRSettingsError.None&&sb.Length>0?sb.ToString():null;}
            catch(Exception){return null;}
        }
        public DeviceInfo Describe(uint i)
        {
            var type=Text(i,ETrackedDeviceProperty.Prop_RegisteredDeviceType_String);
            bool? providesBattery=Bool(i,ETrackedDeviceProperty.Prop_DeviceProvidesBatteryStatus_Bool);
            return new DeviceInfo{
                Index=i,DeviceClass=vr.GetTrackedDeviceClass(i).ToString().Replace("TrackedDeviceClass_",""),Connected=vr.IsTrackedDeviceConnected(i),
                Serial=Text(i,ETrackedDeviceProperty.Prop_SerialNumber_String),Model=Text(i,ETrackedDeviceProperty.Prop_ModelNumber_String),
                Manufacturer=Text(i,ETrackedDeviceProperty.Prop_ManufacturerName_String),TrackingSystem=Text(i,ETrackedDeviceProperty.Prop_TrackingSystemName_String),
                ControllerType=Text(i,ETrackedDeviceProperty.Prop_ControllerType_String),RegisteredDeviceType=type,
                RenderModel=Text(i,ETrackedDeviceProperty.Prop_RenderModelName_String),DriverVersion=Text(i,ETrackedDeviceProperty.Prop_DriverVersion_String),
                ConnectedDongle=Text(i,ETrackedDeviceProperty.Prop_ConnectedWirelessDongle_String),
                ControllerRole=vr.GetControllerRoleForTrackedDeviceIndex(i).ToString().Replace("TrackedControllerRole_",""),
                SteamVRTrackerRole=type==null?null:Setting("trackers","/devices/"+type),
                Wireless=Bool(i,ETrackedDeviceProperty.Prop_DeviceIsWireless_Bool),
                BatteryPercent=providesBattery==true?Float(i,ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float)*100:null};
        }
        public List<DeviceInfo> Devices()
        {
            var list=new List<DeviceInfo>();
            for(uint i=0;i<OpenVR.k_unMaxTrackedDeviceCount;i++)if(vr.GetTrackedDeviceClass(i)!=ETrackedDeviceClass.Invalid)list.Add(Describe(i));
            return list;
        }
        // Identity comes from the serial number, never from enumeration order or controller role.
        public DeviceInfo FindTracker(string serial,out string problem)
        {
            problem=null;var trackers=Devices().FindAll(d=>d.DeviceClass=="GenericTracker");
            if(serial!=null){var match=trackers.Find(d=>d.Serial==serial);if(match==null)problem="No generic tracker with serial "+serial+" is registered in SteamVR.";return match;}
            if(trackers.Count==1)return trackers[0];
            problem=trackers.Count==0?"SteamVR reports no generic tracker. Check VIVE Hub shows the tracker as ready and SteamVR shows its icon."
                :"Several trackers found ("+string.Join(", ",trackers.ConvertAll(d=>d.Serial))+"). Pass --serial to choose the clinician tracker.";
            return null;
        }
        static PoseReading Reading(TrackedDevicePose_t p)=>new PoseReading{
            Valid=p.bPoseIsValid,Position=PoseMath.Position(p.mDeviceToAbsoluteTracking),Rotation=PoseMath.Rotation(p.mDeviceToAbsoluteTracking),
            Velocity=PoseMath.Vector(p.vVelocity),AngularVelocity=PoseMath.Vector(p.vAngularVelocity)};
        static bool Same(HmdMatrix34_t a,HmdMatrix34_t b)=>a.m0==b.m0&&a.m1==b.m1&&a.m2==b.m2&&a.m3==b.m3&&a.m4==b.m4&&a.m5==b.m5&&a.m6==b.m6&&a.m7==b.m7&&a.m8==b.m8&&a.m9==b.m9&&a.m10==b.m10&&a.m11==b.m11;
        // Zero prediction: vrserver's pose estimate at the moment of the call. OpenVR exposes no
        // sensor exposure timestamp to client applications, so host receipt time is what we record.
        public TrackerSample Read(uint index,string serial)
        {
            long ticks=clock.ElapsedTicks;long utc=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            vr.GetDeviceToAbsoluteTrackingPose(RawUniverse,0,raw);
            vr.GetDeviceToAbsoluteTrackingPose(StandingUniverse,0,standing);
            float sinceVsync=0;ulong frame=0;bool vsync=vr.GetTimeSinceLastVsync(ref sinceVsync,ref frame);
            var r=raw[index];bool changed=!haveLast||!Same(r.mDeviceToAbsoluteTracking,lastRaw);
            if(changed){lastRaw=r.mDeviceToAbsoluteTracking;haveLast=true;lastChangeTicks=ticks;}
            return new TrackerSample{
                Seq=++seq,HostTicks=ticks,HostSeconds=ticks/(double)Stopwatch.Frequency,UtcUnixMs=utc,
                HasVsync=vsync,VsyncFrame=frame,SecondsSinceVsync=sinceVsync,DeviceIndex=index,Serial=serial,
                Connected=r.bDeviceIsConnected,Result=r.eTrackingResult,Raw=Reading(r),Standing=Reading(standing[index]),
                RawChanged=changed,RawUnchangedSeconds=(ticks-lastChangeTicks)/(double)Stopwatch.Frequency};
        }
        public bool NextEvent(out VREvent_t e){e=new VREvent_t();return vr.PollNextEvent(ref e,(uint)Marshal.SizeOf(typeof(VREvent_t)));}
        public void AcknowledgeQuit()=>vr.AcknowledgeQuit_Exiting();
    }
}
