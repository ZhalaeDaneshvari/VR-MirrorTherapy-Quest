using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.OpenXR;
using QuestResearch.Embodiment;
using QuestResearch.Hands;
using QuestResearch.Recording;

namespace QuestResearch.Clinician
{
    public enum ClinicianMode { Off, Congruent, Reflected }
    public enum ClinicianAlignmentState { None, Calibrating, Unverified, Verified, Invalid }

    // Optional clinician-hand test feature, present only in the separate clinician test scene/APK.
    // Off by default: nothing is shown until the researcher calibrates, confirms alignment with the marker and
    // selects a mode from the Windows probe. The clinician hand comes only from the tracker (serial-checked)
    // and the explicit calibration; participant hand tracking is read only while calibrating and while
    // confirming alignment, never to place the clinician hand. One tracker gives a rigid pose: the fingers use
    // the hand model's fixed bind pose (open hand). Nothing here senses fingers or physical contact.
    public sealed class ClinicianTrackerFeature : MonoBehaviour
    {
        [SerializeField] ClinicianTrackerLink link;
        [SerializeField] SeatedSetup setup;
        [SerializeField] HandTrackingSource hands;
        [SerializeField] SeatedAvatarDisplay avatar;
        [SerializeField] Transform trackingSpace,head;
        [SerializeField] RemappedHandMesh leftPrefab,rightPrefab;
        [SerializeField] Material skin;
        [SerializeField] float staleSeconds=.1f,lossGraceSeconds=.25f,calibrationSeconds=15,maxCalibrationSpeed=.25f;
        [SerializeField] bool showStatusInHeadset=true;
        public const string FingerPose="Fixed: hand model bind pose (open hand). Not tracked.";
        public ClinicianMode Mode{get;private set;}=ClinicianMode.Off;
        public ClinicianAlignmentState Alignment{get;private set;}=ClinicianAlignmentState.None;
        public string Status{get;private set;}="Clinician hand off";
        string alignmentNote="Not calibrated: press C on the PC.";
        ClinicianCalibration calibration;
        readonly RemappedHandMesh[] meshes=new RemappedHandMesh[2];
        readonly Pose[][] templates=new Pose[2][];
        readonly DisplayedHandPose shown=new DisplayedHandPose(),hidden=new DisplayedHandPose();
        Transform sleeve,marker;TextMesh label;readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<ClinicianCalibrationSolver.Sample> samples=new List<ClinicianCalibrationSolver.Sample>();
        double calibrationStart,lastObserved=double.NegativeInfinity,nextStatus;bool markerOn;
        long lastCalibrationTicks;Vector3 lastCalibrationPosition;
        MovementSessionWriter writer;string sessionFolder;
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();bool originHooked;
        static string CalibrationPath=>Path.Combine(Application.persistentDataPath,"ClinicianTracker","calibration.json");
        static string DataRoot=>Path.Combine(Application.persistentDataPath,"ClinicianTrackerData");
        static double Now=>ClinicianTrackerLink.Seconds(Stopwatch.GetTimestamp());
        int ClinicianSide=>link!=null&&link.ClinicianHand=="left"?0:1;

        public void Configure(ClinicianTrackerLink l,SeatedSetup s,HandTrackingSource h,SeatedAvatarDisplay a,Transform space,Transform camera,
            RemappedHandMesh left,RemappedHandMesh right,Material skinMaterial)
        {link=l;setup=s;hands=h;avatar=a;trackingSpace=space;head=camera;leftPrefab=left;rightPrefab=right;skin=skinMaterial;}

        void Start()
        {
            var glove=Owned(new Material(skin){color=new Color(.55f,.68f,.82f),name="Clinician glove"});
            var cloth=Owned(new Material(skin){color=new Color(.13f,.17f,.30f),name="Clinician sleeve"});
            var markerMaterial=Owned(new Material(skin){color=new Color(.95f,.2f,.75f),name="Clinician alignment marker"});
            for(int side=0;side<2;side++)
            {
                var mesh=Instantiate(side==0?leftPrefab:rightPrefab,transform);mesh.name="Clinician "+(side==0?"left":"right")+" hand (tracker, fixed fingers)";
                foreach(var r in mesh.GetComponentsInChildren<SkinnedMeshRenderer>(true)){var m=r.sharedMaterials;for(int i=0;i<m.Length;i++)m[i]=glove;r.sharedMaterials=m;}
                // Template: bind-pose joints relative to the wrist, captured before any pose is applied.
                var joints=mesh.Joints;var wrist=joints[XRHandJointID.Wrist.ToIndex()];var inverse=Quaternion.Inverse(wrist.rotation);
                templates[side]=new Pose[joints.Length];
                for(int i=0;i<joints.Length;i++)templates[side][i]=new Pose(inverse*(joints[i].position-wrist.position),inverse*joints[i].rotation);
                meshes[side]=mesh;mesh.Apply(hidden,trackingSpace,false);
            }
            sleeve=Primitive(PrimitiveType.Cylinder,"Clinician sleeve",cloth,new Vector3(.06f,.07f,.06f));
            marker=Primitive(PrimitiveType.Sphere,"Clinician tracker alignment marker",markerMaterial,Vector3.one*.02f);
            var stick=Primitive(PrimitiveType.Cube,"Marker forward (tracker -Z in OpenVR)",markerMaterial,new Vector3(.004f,.004f,.06f));
            stick.SetParent(marker,false);stick.localScale=new Vector3(.2f,.2f,3f);stick.localPosition=new Vector3(0,0,1.5f);
            if(showStatusInHeadset&&head!=null)
            {
                var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var item=new GameObject("Clinician status (researcher test)");item.transform.SetParent(head,false);item.transform.localPosition=new Vector3(0,-.21f,.65f);
                label=item.AddComponent<TextMesh>();label.font=font;item.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                label.fontSize=48;label.characterSize=.0035f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(.85f,.9f,1f);
            }
            Load();
            link.Command+=OnCommand;link.ConnectionChanged+=OnConnection;
        }
        T Owned<T>(T item) where T:UnityEngine.Object{owned.Add(item);return item;}
        Transform Primitive(PrimitiveType type,string name,Material material,Vector3 scale)
        {
            var item=GameObject.CreatePrimitive(type);item.name=name;Destroy(item.GetComponent<Collider>());
            item.GetComponent<Renderer>().sharedMaterial=material;item.transform.SetParent(transform,false);item.transform.localScale=scale;item.SetActive(false);
            return item.transform;
        }

        // ---------- researcher commands (from the Windows probe) ----------
        void OnCommand(string command)
        {
            switch(command)
            {
                case "header":
                    Reply("Tracker "+link.Serial+" streaming; clinician hand: "+link.ClinicianHand+".");
                    if(calibration!=null&&calibration.trackerSerial!=link.Serial)Invalidate("A different tracker ("+link.Serial+") is connected; recalibrate (C).");
                    Record("tracker_header","serial "+link.Serial+", hand "+link.ClinicianHand);break;
                case "calibrate":BeginCalibration();break;
                case "verify":Verify();break;
                case "marker":markerOn=!markerOn;Reply("Alignment marker "+(markerOn?"on":"off")+".");break;
                case "clear":
                    calibration=null;Alignment=ClinicianAlignmentState.None;alignmentNote="Calibration cleared: press C.";
                    try{File.Delete(CalibrationPath);}catch(IOException){}Reply(alignmentNote);break;
                case "mode-off":SetMode(ClinicianMode.Off);break;
                case "mode-congruent":SetMode(ClinicianMode.Congruent);break;
                case "mode-reflected":SetMode(ClinicianMode.Reflected);break;
                default:Reply("Unknown command: "+command);break;
            }
        }
        void SetMode(ClinicianMode mode)
        {
            Mode=mode;
            Reply("Mode "+mode+(mode!=ClinicianMode.Off&&Alignment!=ClinicianAlignmentState.Verified?" (hidden until alignment is confirmed)":"")+".");
        }
        void Reply(string message){link.Send("L,"+message);Record("researcher_message",message);}
        void BeginCalibration()
        {
            samples.Clear();calibrationStart=Now;lastCalibrationTicks=0;Alignment=ClinicianAlignmentState.Calibrating;
            alignmentNote="Calibrating: tracker strapped on your "+link.ClinicianHand+" hand as the clinician will wear it. Move it slowly; turn and tilt.";
            Reply(alignmentNote);
        }
        void FinishCalibration()
        {
            if(ClinicianCalibrationSolver.TrySolve(samples,link.Serial,link.ClinicianHand,out var result,out var message))
            {
                calibration=result;Alignment=ClinicianAlignmentState.Unverified;
                alignmentNote="Calibrated: "+message+". Check the pink marker sits on the tracker, then press V.";
                Save();RecordCalibration("calibration_accepted",message);
            }
            else{Alignment=calibration!=null?ClinicianAlignmentState.Unverified:ClinicianAlignmentState.None;alignmentNote=message;RecordCalibration("calibration_rejected",message);}
            Reply(alignmentNote);
        }
        void Verify()
        {
            if(Alignment!=ClinicianAlignmentState.Unverified||calibration==null){Reply("Nothing to confirm: "+alignmentNote);return;}
            if(!Current(out var frame,out _)){Reply("Cannot confirm: tracker not tracking or stream stale.");return;}
            var evidence=Evidence(frame);
            if(evidence==null){Reply("Cannot confirm: touch the tracker with an index fingertip, or wear it on your calibrated hand, so the offset can be measured.");return;}
            Alignment=ClinicianAlignmentState.Verified;lastObserved=Now;alignmentNote="Alignment confirmed: "+evidence;
            RecordCalibration("alignment_confirmed",evidence);Reply(alignmentNote);
        }
        // Measured offsets between the calibrated tracker and the wearer's own tracked hand (both Quest space).
        string Evidence(ClinicianTrackerFrame frame)
        {
            var pose=new Pose(frame.Position,frame.Rotation);var tracker=calibration.TrackerInSpace(pose);var wrist=calibration.WristInSpace(pose);
            var parts=new List<string>();double now=Time.realtimeSinceStartupAsDouble;float tip=float.PositiveInfinity;
            for(int side=0;side<2;side++)
            {
                var h=side==0?hands.Left:hands.Right;if(h.Availability!=HandAvailability.Available||now-h.ReceivedAt>.15)continue;
                int index=XRHandJointID.IndexTip.ToIndex();if(h.JointValid[index])tip=Mathf.Min(tip,Vector3.Distance(h.JointPoses[index].position,tracker.position));
                int w=XRHandJointID.Wrist.ToIndex();
                if(side==ClinicianSide&&h.JointValid[w])parts.Add(F("predicted vs own {0} wrist {1:0.0} cm",link.ClinicianHand,Vector3.Distance(h.JointPoses[w].position,wrist.position)*100));
            }
            if(float.IsFinite(tip))parts.Add(F("nearest index tip to tracker centre {0:0.0} cm",tip*100));
            return parts.Count==0?null:string.Join("; ",parts);
        }
        static string F(string format,params object[] values)=>string.Format(CultureInfo.InvariantCulture,format,values);
        void Invalidate(string reason)
        {
            if(calibration==null&&Alignment==ClinicianAlignmentState.None)return;
            Alignment=ClinicianAlignmentState.Invalid;alignmentNote=reason;Reply(reason);
        }

        // ---------- per-frame pipeline ----------
        bool Current(out ClinicianTrackerFrame frame,out double age)
        {
            age=double.PositiveInfinity;frame=default;
            if(!link.Connected||!link.TryLatest(out frame))return false;
            age=ClinicianTrackerLink.Seconds(Stopwatch.GetTimestamp()-frame.ReceivedTicks);
            return age<=staleSeconds&&frame.Running;
        }
        void OnEnable(){Application.onBeforeRender+=Render;}
        void OnDisable(){Application.onBeforeRender-=Render;StopRecording("component-disabled");Hide();}
        void OnApplicationPause(bool paused){if(paused){StopRecording("app-paused");Hide();}else if(link!=null&&link.Connected)StartRecording();}
        void OnApplicationQuit(){StopRecording("app-quit");writer?.Wait(500);}
        void OnDestroy()
        {
            if(link!=null){link.Command-=OnCommand;link.ConnectionChanged-=OnConnection;}
            foreach(var i in inputs)i.trackingOriginUpdated-=OriginChanged;
            foreach(var o in owned)if(o!=null)Destroy(o);
        }
        void OnConnection(bool connected){if(connected)StartRecording();else{Record("link_disconnected","");StopRecording("link-disconnected");}}
        void OriginChanged(XRInputSubsystem _){Invalidate("Quest tracking origin changed (recentre/boundary); recalibrate (C).");}
        void HookOrigin()
        {
            if(originHooked)return;SubsystemManager.GetSubsystems(inputs);if(inputs.Count==0)return;
            foreach(var i in inputs)i.trackingOriginUpdated+=OriginChanged;originHooked=true;
        }
        void Hide(){foreach(var m in meshes)if(m!=null)m.Apply(hidden,trackingSpace,false);if(sleeve!=null)sleeve.gameObject.SetActive(false);if(marker!=null)marker.gameObject.SetActive(false);}

        [BeforeRenderOrder(10002)]
        void Render()
        {
            if(meshes[0]==null)return;HookOrigin();
            double now=Now;bool ok=Current(out var frame,out double age);
            bool headTracked=setup!=null&&setup.HeadTracked,present=OpenXRUtility.IsUserPresent,bodyAligned=setup!=null&&setup.Aligned;
            // The tracker's frame may move when it relocalises after a loss, so any unobserved gap needs a fresh check.
            if(ok)
            {
                if(Alignment==ClinicianAlignmentState.Verified&&now-lastObserved>lossGraceSeconds)
                {Alignment=ClinicianAlignmentState.Unverified;alignmentNote="Tracker tracking was lost; check the marker and press V, or recalibrate (C).";Reply(alignmentNote);}
                lastObserved=now;
            }
            if(Alignment==ClinicianAlignmentState.Calibrating)Calibrate(ok,frame,now);
            Pose? tracker=null,wrist=null;
            if(ok&&calibration!=null&&Alignment!=ClinicianAlignmentState.Invalid&&Alignment!=ClinicianAlignmentState.Calibrating)
            {var raw=new Pose(frame.Position,frame.Rotation);tracker=calibration.TrackerInSpace(raw);wrist=calibration.WristInSpace(raw);}
            string reason=HiddenReason(ok,age,headTracked,present,bodyAligned);
            bool reflected=Mode==ClinicianMode.Reflected;int side=reflected?1-ClinicianSide:ClinicianSide;
            float proximity=float.PositiveInfinity,progress=0;int proximitySide=-1;
            if(reason==null&&wrist.HasValue)
            {
                Pose body=Pose.identity;
                if(reflected)body=new Pose(trackingSpace.InverseTransformPoint(setup.BodyFrame.position),Quaternion.Inverse(trackingSpace.rotation)*setup.BodyFrame.rotation);
                var template=templates[ClinicianSide];var w=wrist.Value;
                for(int i=0;i<template.Length;i++)
                {
                    var joint=new Pose(w.position+w.rotation*template[i].position,w.rotation*template[i].rotation);
                    shown.Joints[i]=reflected?MovementReflection.Reflect(joint,body):joint;shown.Valid[i]=true;
                }
                shown.SourceHand=(PhysicalHand)ClinicianSide;shown.Reflected=reflected;
                meshes[side].Apply(shown,trackingSpace,headTracked);meshes[1-side].Apply(hidden,trackingSpace,false);
                PlaceSleeve();
                Proximity(out proximitySide,out progress,out proximity);
            }
            else{meshes[0].Apply(hidden,trackingSpace,false);meshes[1].Apply(hidden,trackingSpace,false);sleeve.gameObject.SetActive(false);}
            bool showMarker=tracker.HasValue&&(markerOn||Alignment==ClinicianAlignmentState.Unverified);
            marker.gameObject.SetActive(showMarker);
            if(showMarker)marker.SetPositionAndRotation(trackingSpace.TransformPoint(tracker.Value.position),trackingSpace.rotation*tracker.Value.rotation);
            if(now>=nextStatus)
            {
                nextStatus=now+.25;
                Status=(reason==null?"Clinician "+Mode.ToString().ToUpperInvariant()+" shown"+(proximitySide>=0?F(" | palm over displayed {0} forearm {1:0}% elbow-wrist, {2:0.0} cm",(PhysicalHand)proximitySide,progress*100,proximity*100):"")
                    :"Clinician hidden: "+reason)+" | "+Alignment+(link.Connected?F(" | link {0:0} ms old, RTT {1:0.0} ms",age*1000,link.RoundTripMs):" | link down");
                link.Send("S,"+Status);
                if(label!=null)label.text=Status.Replace(" | ","\n")+(Alignment==ClinicianAlignmentState.Verified?"":"\n"+alignmentNote);
            }
            if(writer!=null)Sample(frame,age,ok,tracker,wrist,reason,reflected,side,headTracked,present,bodyAligned,proximitySide,progress,proximity);
        }
        string HiddenReason(bool ok,double age,bool headTracked,bool present,bool bodyAligned)
        {
            if(Mode==ClinicianMode.Off)return "mode off";
            if(!link.Connected)return "no link to PC";
            if(!ok)return double.IsInfinity(age)?"no tracker frames":age>staleSeconds?F("stream stale ({0:0} ms)",age*1000):"tracker not tracking";
            if(calibration==null||Alignment!=ClinicianAlignmentState.Verified)return "alignment "+Alignment.ToString().ToLowerInvariant();
            if(!headTracked||!present)return "headset not tracking/worn";
            if(Mode==ClinicianMode.Reflected&&!bodyAligned)return "participant midline not calibrated";// never fall back to congruent
            return null;
        }
        void Calibrate(bool ok,ClinicianTrackerFrame frame,double now)
        {
            double remaining=calibrationSeconds-(now-calibrationStart);
            alignmentNote=F("Calibrating {0:0} s: move your {1} hand (tracker strapped on) slowly; turn and tilt it. {2} samples",Math.Max(0,remaining),link.ClinicianHand,samples.Count);
            if(remaining<=0){FinishCalibration();return;}
            if(!ok)return;
            var h=ClinicianSide==0?hands.Left:hands.Right;int w=XRHandJointID.Wrist.ToIndex();
            if(h.Availability!=HandAvailability.Available||!h.JointValid[w]||Time.realtimeSinceStartupAsDouble-h.ReceivedAt>.05)return;
            // Skip fast motion: hand tracking and the USB stream have different latencies.
            if(lastCalibrationTicks!=0)
            {
                double dt=ClinicianTrackerLink.Seconds(frame.ReceivedTicks-lastCalibrationTicks);
                if(dt>0&&Vector3.Distance(frame.Position,lastCalibrationPosition)/dt>maxCalibrationSpeed){lastCalibrationTicks=frame.ReceivedTicks;lastCalibrationPosition=frame.Position;return;}
            }
            if(frame.ReceivedTicks==lastCalibrationTicks)return;
            lastCalibrationTicks=frame.ReceivedTicks;lastCalibrationPosition=frame.Position;
            samples.Add(new ClinicianCalibrationSolver.Sample{trackerPosition=frame.Position,trackerRotation=frame.Rotation,wristPosition=h.JointPoses[w].position,wristRotation=h.JointPoses[w].rotation});
        }
        void PlaceSleeve()
        {
            // Covers the open wrist of the shared hand mesh; follows the displayed (possibly reflected) hand.
            var w=trackingSpace.TransformPoint(shown.Joints[XRHandJointID.Wrist.ToIndex()].position);
            var knuckle=trackingSpace.TransformPoint(shown.Joints[XRHandJointID.MiddleProximal.ToIndex()].position);
            var back=(w-knuckle).normalized;if(back.sqrMagnitude<.5f){sleeve.gameObject.SetActive(false);return;}
            sleeve.gameObject.SetActive(true);sleeve.SetPositionAndRotation(w+back*.06f,Quaternion.FromToRotation(Vector3.up,back));
        }
        // Distance from the displayed clinician palm to the displayed top-of-forearm line. Display geometry only:
        // it is not evidence of physical contact.
        void Proximity(out int side,out float progress,out float distance)
        {
            side=-1;progress=0;distance=float.PositiveInfinity;if(avatar==null)return;
            var palm=trackingSpace.TransformPoint(shown.Joints[XRHandJointID.Palm.ToIndex()].position);
            for(int s=0;s<2;s++)for(int k=0;k<=20;k++)
                if(avatar.TryTouchPoint(s,k/20f,out var point)){float d=Vector3.Distance(point,palm);if(d<distance){distance=d;side=s;progress=k/20f;}}
        }

        // ---------- persistence and recording (separate from participant MovementData) ----------
        void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CalibrationPath));string temp=CalibrationPath+".tmp";
                File.WriteAllText(temp,JsonUtility.ToJson(calibration,true)+"\n");
                if(File.Exists(CalibrationPath))File.Replace(temp,CalibrationPath,null);else File.Move(temp,CalibrationPath);
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){Reply("Calibration not saved: "+e.GetType().Name);}
        }
        void Load()
        {
            try
            {
                if(!File.Exists(CalibrationPath))return;
                var loaded=JsonUtility.FromJson<ClinicianCalibration>(File.ReadAllText(CalibrationPath));
                if(loaded==null||loaded.schema!=1||string.IsNullOrEmpty(loaded.revision))return;
                calibration=loaded;Alignment=ClinicianAlignmentState.Unverified;
                alignmentNote="Saved calibration loaded; check the marker and press V, or recalibrate (C).";
            }
            catch(Exception e) when(e is IOException||e is ArgumentException){alignmentNote="Saved calibration unreadable; recalibrate (C).";}
        }
        [Serializable] sealed class Metadata
        {
            public int schema=1;public string kind="clinician_tracker",appVersion,unityVersion,device,startUtc,fingerPose=FingerPose;
            public double stopwatchFrequency;public float staleSeconds,lossGraceSeconds;
            public string rawConvention="OpenVR as streamed by the Windows probe: right-handed, +Y up, -Z forward, metres, quaternion x,y,z,w; tracker driver frame.";
            public string questConvention="calibrated and displayed poses: Quest XR-origin tracking space, Unity left-handed axes, metres.";
            public string timing="questTicks/receivedTicks: Quest Stopwatch. pcTicks: PC Stopwatch at the probe read. pcSampleAgeAtReceiptMs uses the lowest-RTT clock mapping; -1 before the first sync.";
            public string scope="Clinician tracker only. Participant movement is recorded separately by the pilot recorder. Proximity values are display geometry, not contact.";
        }
        [Serializable] sealed class SampleRecord
        {
            public string type="sample";public long questTicks;public int frame;
            public bool linkConnected;public long sequence,pcTicks,pcUtcMs,receivedTicks;public double ageMs,pcSampleAgeAtReceiptMs,roundTripMs;
            public bool trackerConnected,trackerValid;public int trackingResult;public Vector3 rawPosition;public Quaternion rawRotation;
            public string alignment,calibrationRevision;public bool calibratedValid;
            public Vector3 trackerPosition,wristPosition;public Quaternion trackerRotation,wristRotation;
            public string mode,hiddenReason,displayedSide;public bool displayed,reflected;public Vector3 displayedWristPosition;public Quaternion displayedWristRotation;
            public bool headTracked,userPresent,bodyAligned;
            public string proximitySide;public float proximityProgress,proximityMetres;
        }
        [Serializable] sealed class EventRecord{public string type="event",kind,message,utc,alignment,mode;public long questTicks;}
        [Serializable] sealed class CalibrationRecord{public string type="calibration",kind,message,utc;public long questTicks;public ClinicianCalibration calibration;}
        readonly SampleRecord sampleRecord=new SampleRecord();
        void StartRecording()
        {
            if(writer!=null)return;
            try
            {
                sessionFolder=Path.Combine(DataRoot,DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
                writer=new MovementSessionWriter(sessionFolder,JsonUtility.ToJson(new Metadata{appVersion=Application.identifier+" "+Application.version,unityVersion=Application.unityVersion,
                    device=SystemInfo.deviceModel,startUtc=DateTime.UtcNow.ToString("O"),stopwatchFrequency=Stopwatch.Frequency,staleSeconds=staleSeconds,lossGraceSeconds=lossGraceSeconds},true));
                Record("recording_started",sessionFolder);
                if(calibration!=null)RecordCalibration("calibration_in_use",alignmentNote);
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){writer=null;link.Send("L,Clinician recording failed to start: "+e.GetType().Name);}
        }
        void StopRecording(string reason){if(writer==null)return;Record("recording_stopped",reason);writer.Complete(reason);writer=null;}
        void Append(string json){if(writer!=null&&!writer.Append(json)){link.Send("L,Clinician recording stopped: "+writer.Error);writer=null;}}
        void Record(string kind,string message)=>Append(JsonUtility.ToJson(new EventRecord{kind=kind,message=message,utc=DateTime.UtcNow.ToString("O"),questTicks=Stopwatch.GetTimestamp(),alignment=Alignment.ToString(),mode=Mode.ToString()}));
        void RecordCalibration(string kind,string message)=>Append(JsonUtility.ToJson(new CalibrationRecord{kind=kind,message=message,utc=DateTime.UtcNow.ToString("O"),questTicks=Stopwatch.GetTimestamp(),calibration=calibration}));
        void Sample(ClinicianTrackerFrame f,double age,bool ok,Pose? tracker,Pose? wrist,string reason,bool reflected,int side,bool headTracked,bool present,bool bodyAligned,int proximitySide,float progress,float proximity)
        {
            var r=sampleRecord;bool have=link.TryLatest(out f);
            r.questTicks=Stopwatch.GetTimestamp();r.frame=Time.frameCount;r.linkConnected=link.Connected;
            r.sequence=have?f.Sequence:-1;r.pcTicks=f.PcTicks;r.pcUtcMs=f.PcUtcMs;r.receivedTicks=f.ReceivedTicks;r.ageMs=have?age*1000:-1;r.roundTripMs=link.RoundTripMs;
            double pcAtQuest=link.PcSampleQuestSeconds(f);r.pcSampleAgeAtReceiptMs=have&&!double.IsNaN(pcAtQuest)?(ClinicianTrackerLink.Seconds(f.ReceivedTicks)-pcAtQuest)*1000:-1;
            r.trackerConnected=f.TrackerConnected;r.trackerValid=f.Valid;r.trackingResult=have?f.Result:-1;r.rawPosition=f.RawPosition;r.rawRotation=f.RawRotation;
            r.alignment=Alignment.ToString();r.calibrationRevision=calibration?.revision;r.calibratedValid=wrist.HasValue;
            r.trackerPosition=tracker?.position??Vector3.zero;r.trackerRotation=tracker?.rotation??Quaternion.identity;
            r.wristPosition=wrist?.position??Vector3.zero;r.wristRotation=wrist?.rotation??Quaternion.identity;
            r.mode=Mode.ToString();r.displayed=reason==null&&wrist.HasValue;r.hiddenReason=reason??"";r.reflected=r.displayed&&reflected;
            r.displayedSide=r.displayed?((PhysicalHand)side).ToString():"";
            var dw=shown.Joints[XRHandJointID.Wrist.ToIndex()];r.displayedWristPosition=r.displayed?dw.position:Vector3.zero;r.displayedWristRotation=r.displayed?dw.rotation:Quaternion.identity;
            r.headTracked=headTracked;r.userPresent=present;r.bodyAligned=bodyAligned;
            r.proximitySide=proximitySide>=0?((PhysicalHand)proximitySide).ToString():"";r.proximityProgress=progress;r.proximityMetres=proximitySide>=0?proximity:-1;
            Append(JsonUtility.ToJson(r));
        }
    }
}
