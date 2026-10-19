using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Hands;
using QuestResearch.Hands;
using QuestResearch.Embodiment;
using QuestResearch.BodyCalibration;
namespace QuestResearch.Recording
{
    public sealed class MovementRecorder:MonoBehaviour
    {
        const double Interval=1.0/30;
        MovementSessionWriter writer;HandTrackingSource hands;BodyTrackingProbe body;
        SeatedSetup setup;MovementRemapper mapping;SeatedAvatarDisplay avatar;Transform head,space;
        BodyArmTargets fittedArms;PilotWalkthrough pilot;double next,lastSample=-1;long sequence;
        public string Participant{get;private set;}="";public string Session{get;private set;}="";
        public string Error{get;private set;}="";public bool Recording{get;private set;}
        public bool Saving=>writer!=null&&!Recording&&!writer.Done;
        public string Status=>Error.Length>0?"DATA ERROR - session retained":Recording?Participant+" | RECORDING":Saving?Participant+" | Saving on headset...":Session.Length>0?Participant+" | Saved on headset; USB copies to laptop":"No participant recording yet";
        readonly Sample sample=new Sample();
        public void Configure(HandTrackingSource h,BodyTrackingProbe b,SeatedSetup s,MovementRemapper m,SeatedAvatarDisplay a,Transform camera,PilotWalkthrough p)
        {hands=h;body=b;setup=s;mapping=m;avatar=a;head=camera;space=m.TrackingSpace;pilot=p;fittedArms=a.GetComponent<BodyArmTargets>();avatar.FrameDisplayed+=CaptureRendered;}
        public static bool ValidParticipant(string id)=>id!=null&&id.Length==3&&id[0]=='P'&&int.TryParse(id.Substring(1),out int n)&&n>=1&&n<=20&&id=="P"+n.ToString("D2");
        public bool Begin(string id)
        {
            if(!ValidParticipant(id)||Recording||Saving)return false;
            Participant=id;Session=DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-"+Guid.NewGuid().ToString("N");Error="";
            var metadata=new Metadata{participant=id,session=Session,createdUtc=DateTime.UtcNow.ToString("O"),appVersion=Application.version,unityVersion=Application.unityVersion,deviceModel=SystemInfo.deviceModel,stopwatchFrequency=Stopwatch.Frequency,stopwatchStart=Stopwatch.GetTimestamp(),unityRealtimeAtStart=Time.realtimeSinceStartupAsDouble};
            for(int i=0;i<26;i++)metadata.handJointNames[i]=XRHandJointIDUtility.FromIndex(i).ToString();
            for(int i=0;i<8;i++)metadata.bodyJointNames[i]=BodyTrackingProbe.JointIds[i].ToString();
            try{writer=new MovementSessionWriter(Path.Combine(Application.persistentDataPath,"MovementData",id,Session),JsonUtility.ToJson(metadata,true));}
            catch(Exception ex){Error="Cannot start recording: "+ex.Message;UnityEngine.Debug.LogError(Error);return false;}
            sequence=0;next=Time.realtimeSinceStartupAsDouble;lastSample=-1;Recording=true;Event("session_started");return true;
        }
        public void End(string reason)
        {
            if(!Recording)return;Event("session_ended_"+reason);Recording=false;writer.Complete(reason);
        }
        public void Event(string name)
        {
            if(!Recording)return;
            Write(JsonUtility.ToJson(new EventRow{participant=Participant,session=Session,kind=name,monotonicSeconds=Time.realtimeSinceStartupAsDouble,stopwatchTicks=Stopwatch.GetTimestamp(),utc=DateTime.UtcNow.ToString("O"),stage=pilot.CurrentPage.ToString(),skinTone=pilot.SkinToneName,movement=setup.Parameters.movementCondition.ToString(),parameters=setup.Parameters,bodyFrameWorld=Read(setup.BodyFrame,true)}));
        }
        void Write(string line){if(!writer.Append(line)){Error=writer.Error??"Recording closed unexpectedly";Recording=false;writer.Complete("write_failure");}}
        void Update(){if(writer!=null&&writer.Error!=null){Error=writer.Error;Recording=false;}}
        void CaptureRendered(){Capture(true);}
        void LateUpdate(){if(Recording&&Time.realtimeSinceStartupAsDouble-lastSample>.1)Capture(false);}
        void Capture(bool afterAvatar)
        {
            double now=Time.realtimeSinceStartupAsDouble;if(!Recording||now<next)return;
            next+=Interval;if(next<now-Interval)next=now+Interval;
            sample.sample=++sequence;sample.participant=Participant;sample.session=Session;
            sample.monotonicSeconds=now;sample.stopwatchTicks=Stopwatch.GetTimestamp();sample.deltaSeconds=lastSample<0?0:now-lastSample;lastSample=now;
            sample.frame=Time.frameCount;sample.capturePhase=afterAvatar?"after_avatar_before_render":"late_update_no_recent_render";
            sample.displaySnapshotCurrent=afterAvatar;sample.focused=Application.isFocused;sample.headTracked=setup.HeadTracked;
            sample.stage=pilot.CurrentPage.ToString();sample.skinTone=pilot.SkinToneName;sample.movement=setup.Parameters.movementCondition.ToString();sample.parameters=setup.Parameters;
            sample.headInTracking=ReadIn(head,space,setup.HeadTracked);sample.headGazeOrigin=sample.headInTracking.position;sample.headGazeDirection=sample.headInTracking.valid?Quaternion.Inverse(space.rotation)*head.forward:Vector3.zero;
            sample.trackingSpaceWorld=Read(space,true);sample.bodyFrameWorld=Read(setup.BodyFrame,setup.Aligned);sample.aligned=setup.Aligned;
            ReadPhysical(hands.Left,sample.physicalLeft,now);ReadPhysical(hands.Right,sample.physicalRight,now);
            ReadDisplayed(mapping.Left,sample.mappedLeft,true);ReadDisplayed(mapping.Right,sample.mappedRight,true);
            ReadDisplayed(avatar.DisplayedLeft,sample.displayedLeft,afterAvatar);ReadDisplayed(avatar.DisplayedRight,sample.displayedRight,afterAvatar);
            sample.leftHandVisible=afterAvatar&&avatar.HandVisible(0);sample.rightHandVisible=afterAvatar&&avatar.HandVisible(1);
            sample.bodyStatus=body.Status;sample.bodyCalibration=body.Calibration;sample.bodyConfidence=float.IsFinite(body.Confidence)?body.Confidence:-1;sample.bodyAgeSeconds=Age(now,body.ReceivedAt);sample.bodyProviderTime=double.IsFinite(body.ProviderTime)?body.ProviderTime:0;sample.originRevision=body.OriginRevision;sample.bodyFidelity=body.Fidelity;sample.skeletonRevision=body.SkeletonRevision;sample.bodyFitValid=fittedArms!=null&&fittedArms.HasFit;
            for(int i=0;i<8;i++){sample.estimatedBody[i]=Read(body.Poses[i],body.Valid[i]&&sample.bodyAgeSeconds>=0&&sample.bodyAgeSeconds<.25);sample.bodyLocationFlags[i]=body.LocationFlags[i];}
            for(int i=0;i<2;i++)
            {
                sample.fittedArmLengths[i]=fittedArms==null?Vector2.zero:fittedArms.FittedLengths(i);
                sample.armVisible[i]=afterAvatar&&avatar.ArmVisible[i];sample.handTableLift[i]=avatar.HandTableLift[i];sample.armSurfaceLift[i]=avatar.ArmSurfaceLift[i];sample.palmFlattenDegrees[i]=avatar.PalmFlattenDegrees[i];
                for(int j=0;j<3;j++)sample.fittedArmBodyPoints[i*3+j]=sample.armVisible[i]?avatar.FittedArmPoints[i*3+j]:Vector3.zero;
            }
            Write(JsonUtility.ToJson(sample));
        }
        static double Age(double now,double received)=>double.IsFinite(received)?Math.Max(0,now-received):-1;
        static PoseValue Read(Pose p,bool valid)
        {
            valid &= float.IsFinite(p.position.x)&&float.IsFinite(p.position.y)&&float.IsFinite(p.position.z)
                &&float.IsFinite(p.rotation.x)&&float.IsFinite(p.rotation.y)&&float.IsFinite(p.rotation.z)&&float.IsFinite(p.rotation.w);
            return new PoseValue{valid=valid,position=valid?p.position:Vector3.zero,rotation=valid?p.rotation:Quaternion.identity};
        }
        static PoseValue Read(Transform t,bool valid)=>Read(t!=null?new Pose(t.position,t.rotation):Pose.identity,valid&&t!=null);
        static PoseValue ReadIn(Transform t,Transform frame,bool valid)=>Read(new Pose(frame.InverseTransformPoint(t.position),Quaternion.Inverse(frame.rotation)*t.rotation),valid);
        static void ReadPhysical(TrackingHandFrame source,HandRow target,double now)
        {
            target.status=source.Availability.ToString();target.source=source.ReportedSource?.ToString()??"Unavailable";target.sequence=source.Sequence;target.ageSeconds=Age(now,source.ReceivedAt);target.receivedAt=double.IsFinite(source.ReceivedAt)?source.ReceivedAt:-1;target.updatePhase=source.UpdatePhase.ToString();
            for(int i=0;i<26;i++)target.joints[i]=Read(source.JointPoses[i],source.JointValid[i]);
        }
        static void ReadDisplayed(DisplayedHandPose source,DisplayedRow target,bool current)
        {
            target.sourceHand=source==null?"Unavailable":source.SourceHand.ToString();target.reflected=source!=null&&source.Reflected;target.inputSequence=source==null?0:source.InputSequence;
            for(int i=0;i<26;i++)target.joints[i]=Read(source==null?Pose.identity:source.Joints[i],current&&source!=null&&source.Valid[i]);
        }
        void OnApplicationPause(bool paused){if(paused)End("headset_or_app_paused");}
        void OnDestroy(){if(avatar!=null)avatar.FrameDisplayed-=CaptureRendered;End("app_closed");writer?.Wait(2000);}
        [Serializable] public struct PoseValue{public bool valid;public Vector3 position;public Quaternion rotation;}
        [Serializable] sealed class Metadata
        {
            public int schemaVersion=1;public string participant,session,createdUtc,appVersion,unityVersion,deviceModel;
            public int targetSamplesPerSecond=30;public long stopwatchFrequency,stopwatchStart;public double unityRealtimeAtStart;
            public string coordinates="Unity metres; quaternion x,y,z,w; hand/head/body provider poses in XR tracking space. Fitted arm points in participant body frame. World frame transforms included per sample.";
            public bool eyeTrackingAvailable=false;public string gazeSource="Head direction only; Quest 3 has no eye tracking. Not fixation, pupil, or blink data.";
            public string handSource="OpenXR XR Hands; validity is not optical observation/confidence";public string bodySource="Meta body estimates, not directly measured elbows/shoulders";
            public string[] handJointNames=new string[26],bodyJointNames=new string[8];
        }
        [Serializable] sealed class HandRow{public string status,source,updatePhase;public ulong sequence;public double ageSeconds,receivedAt;public PoseValue[] joints=new PoseValue[26];}
        [Serializable] sealed class DisplayedRow{public string sourceHand;public bool reflected;public ulong inputSequence;public PoseValue[] joints=new PoseValue[26];}
        [Serializable] sealed class EventRow{public string type="event",participant,session,kind,utc,stage,skinTone,movement;public double monotonicSeconds;public long stopwatchTicks;public SeatedParameters parameters;public PoseValue bodyFrameWorld;}
        [Serializable] sealed class Sample
        {
            public string type="sample",participant,session,stage,skinTone,movement,capturePhase,bodyStatus,bodyCalibration,bodyFidelity;
            public long sample,stopwatchTicks;public int frame,originRevision;public double monotonicSeconds,deltaSeconds,bodyAgeSeconds,bodyProviderTime;
            public bool bodyFitValid;public uint skeletonRevision;public int[] bodyLocationFlags=new int[8];public Vector2[] fittedArmLengths=new Vector2[2];
            public bool focused,headTracked,aligned,displaySnapshotCurrent,leftHandVisible,rightHandVisible,eyeTrackingAvailable=false;
            public PoseValue headInTracking,trackingSpaceWorld,bodyFrameWorld;public Vector3 headGazeOrigin,headGazeDirection;public SeatedParameters parameters;
            public HandRow physicalLeft=new HandRow(),physicalRight=new HandRow();
            public DisplayedRow mappedLeft=new DisplayedRow(),mappedRight=new DisplayedRow(),displayedLeft=new DisplayedRow(),displayedRight=new DisplayedRow();
            public PoseValue[] estimatedBody=new PoseValue[8];public float bodyConfidence;
            public bool[] armVisible=new bool[2];public Vector3[] fittedArmBodyPoints=new Vector3[6];public float[] handTableLift=new float[2],armSurfaceLift=new float[2],palmFlattenDegrees=new float[2];
        }
    }
}
