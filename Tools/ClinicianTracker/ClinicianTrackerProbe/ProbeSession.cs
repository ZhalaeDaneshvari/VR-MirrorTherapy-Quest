using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Valve.VR;
namespace QuestResearch.ClinicianTracker
{
    // Fixed-rate polling loop shared by live and test modes. Tracks the clinician tracker by serial,
    // logs every sample and every status transition, and stops cleanly on Ctrl+C or SteamVR quit.
    public sealed class ProbeSession
    {
        [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint ms);
        readonly OpenVrTracker vr;readonly ProbeLog log;readonly Stopwatch clock;readonly double hz;
        public string Serial{get;}public uint? Index{get;private set;}
        public string StopReason{get;private set;}
        volatile bool stop;string lastStatus;
        public ProbeSession(OpenVrTracker tracker,ProbeLog log,Stopwatch clock,DeviceInfo device,double hz)
        {
            vr=tracker;this.log=log;this.clock=clock;this.hz=hz;Serial=device.Serial;Index=device.Index;
            Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;Stop("stopped-by-user");};
        }
        public void Stop(string reason){StopReason??=reason;stop=true;}
        static string Status(TrackerSample s)=>"connected="+s.Connected+" valid="+s.Raw.Valid+" result="+s.Result;
        void Resolve(long ticks,string why)
        {
            uint? found=null;
            foreach(var d in vr.Devices())if(d.Serial==Serial&&d.DeviceClass=="GenericTracker"){found=d.Index;break;}
            if(found!=Index){log.Event(ticks,found==null?"trackerMissing":"trackerIndexChanged",new{serial=Serial,previousIndex=Index,index=found,cause=why});Index=found;}
        }
        void Events(long ticks)
        {
            while(vr.NextEvent(out VREvent_t e))
            {
                var type=(EVREventType)e.eventType;
                switch(type)
                {
                    case EVREventType.VREvent_Quit:case EVREventType.VREvent_DriverRequestedQuit:
                        log.Event(ticks,"steamvrQuit",new{type=type.ToString()});vr.AcknowledgeQuit();Stop("steamvr-quit");return;
                    case EVREventType.VREvent_TrackedDeviceActivated:case EVREventType.VREvent_TrackedDeviceDeactivated:case EVREventType.VREvent_TrackedDeviceUpdated:
                        log.Event(ticks,"openvrDevice",new{type=type.ToString(),index=e.trackedDeviceIndex});Resolve(ticks,type.ToString());break;
                    // SteamVR broadcasts RoleChanged (index -1) every ~2 s with Link; it carries no tracker identity change.
                    case EVREventType.VREvent_ChaperoneUniverseHasChanged:case EVREventType.VREvent_StandingZeroPoseReset:case EVREventType.VREvent_SeatedZeroPoseReset:
                        log.Event(ticks,"openvrUniverseChanged",new{type=type.ToString()});break;
                }
            }
        }
        // Calls onSample once per tick (null while the tracker is missing) until it returns false,
        // the duration ends or Stop is called.
        public void Run(double seconds,Func<TrackerSample,bool> onSample)
        {
            timeBeginPeriod(1);
            try
            {
                long period=(long)(Stopwatch.Frequency/hz);long next=clock.ElapsedTicks;long end=seconds>0?next+(long)(seconds*Stopwatch.Frequency):long.MaxValue;
                long lastResolve=next;
                while(!stop)
                {
                    long now=clock.ElapsedTicks;if(now>=end){Stop("duration-complete");break;}
                    Events(now);if(stop)break;
                    if(Index==null&&now-lastResolve>Stopwatch.Frequency){Resolve(now,"periodic");lastResolve=now;}
                    TrackerSample s=null;
                    if(Index!=null)
                    {
                        s=vr.Read(Index.Value,Serial);log.Sample(s);
                        string status=Status(s);if(status!=lastStatus){log.Event(s.HostTicks,"trackingStatus",new{previous=lastStatus,current=status});lastStatus=status;}
                    }
                    else if(lastStatus!="missing"){log.Event(now,"trackingStatus",new{previous=lastStatus,current="missing"});lastStatus="missing";}
                    if(!onSample(s)){Stop("complete");break;}// null sample: tracker not registered in SteamVR this tick
                    next+=period;long wait=next-clock.ElapsedTicks;
                    if(wait<-period*10)next=clock.ElapsedTicks;// fell far behind: resynchronise instead of bursting
                    while((wait=next-clock.ElapsedTicks)>0){if(wait>Stopwatch.Frequency/500)Thread.Sleep(1);else Thread.SpinWait(64);}
                }
            }
            finally{timeEndPeriod(1);}
        }
    }
}
