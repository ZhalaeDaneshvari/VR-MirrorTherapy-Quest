using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

namespace QuestResearch.Embodiment
{
    public enum ParticipantHands { Both, LeftOnly, RightOnly }

    [Serializable]
    public sealed class SeatedParameters
    {
        public float tableHeight=.74f,tableWidth=1.10f,tableDepth=.65f,tableNearEdge=.18f;
        public float shoulderWidth=.38f,shoulderDrop=.22f,upperArmLength=.30f,forearmLength=.26f;
        public float armFullness=1.3f;
        public bool tableSupportEnabled=true;
        public bool tableContactEnabled=true;
        public MovementCondition movementCondition = MovementCondition.Normal;
        public bool showSourceHand = true;
        public ParticipantHands participantHands = ParticipantHands.Both;
        public bool UsesSide(int side)=>participantHands==ParticipantHands.Both || (side==0?participantHands==ParticipantHands.LeftOnly:participantHands==ParticipantHands.RightOnly);
        public bool tableAlignmentConfirmed=false;
        public bool requestAlignment=false;
        public bool Valid()
        {
            return Enum.IsDefined(typeof(ParticipantHands),participantHands) && Between(armFullness,.8f,1.8f) && Enum.IsDefined(typeof(MovementCondition), movementCondition) && Between(tableHeight,.45f,1.1f)&&Between(tableWidth,.6f,2f)&&Between(tableDepth,.35f,1.2f)
                &&Between(tableNearEdge,.05f,.65f)&&Between(shoulderWidth,.25f,.6f)&&Between(shoulderDrop,.12f,.4f)
                &&Between(upperArmLength,.18f,.45f)&&Between(forearmLength,.18f,.4f);
        }
        static bool Between(float v,float min,float max)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=min&&v<=max;
    }

    // Alignment is an explicit researcher action. Never make the body frame follow head yaw.
    public sealed class SeatedSetup : MonoBehaviour
    {
        [SerializeField] Transform trackingSpace,headCamera;
        [SerializeField] TextMesh label;
        [SerializeField] SeatedParameters parameters=new SeatedParameters();
        public SeatedParameters Parameters=>parameters;
        public Transform BodyFrame { get; private set; }
        public bool Aligned { get; private set; }
        public bool SurfacePreview {get;set;}=true;
        public bool ShowFitMarkers {get;set;}
        public float SeatedHeadHeight { get; private set; }
        public bool HeadTracked { get; private set; }
        public string SurfaceFitWarning { get; set; } = "";
        public string Message { get; private set; }="Waiting for researcher alignment";
        public event Action Changed;
        string settingsPath;
        DateTime lastWrite;
        double nextPoll,nextLabel;
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();
        XRInputSubsystem alignedInput;

        public void Configure(Transform space,Transform head,TextMesh text){trackingSpace=space;headCamera=head;label=text;}
        void Awake()
        {
            BodyFrame=new GameObject("Participant body frame (fixed after alignment)").transform;
            BodyFrame.SetParent(trackingSpace,false);
            settingsPath=Path.Combine(Application.persistentDataPath,"seated-setup.json");
            Directory.CreateDirectory(Application.persistentDataPath);
            if(File.Exists(settingsPath))Load(false);
            else Save();
            Debug.Log("[Seated] Settings file: "+settingsPath+". Awaiting explicit alignment.");
        }
        void Update()
        {
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracked=head.TryGetFeatureValue(CommonUsages.isTracked,out bool value)&&value;
            if(HeadTracked!=tracked){HeadTracked=tracked;Changed?.Invoke();}
            double now=Time.realtimeSinceStartupAsDouble;
            if(now>=nextPoll)
            {
                nextPoll=now+.5;
                try{if(File.Exists(settingsPath)&&File.GetLastWriteTimeUtc(settingsPath)!=lastWrite)Load(true);}
                catch(IOException e){Message="Setup file not ready: "+e.GetType().Name;}
            }
            if(now>=nextLabel&&label!=null)
            {
                nextLabel=now+.25;
                label.text="MOVEMENT: "+parameters.movementCondition+(parameters.movementCondition==MovementCondition.Normal?" / both tracked hands":parameters.showSourceHand?" / source visible":" / opposite only")+"\n"+Message+"\nTable height "+(parameters.tableHeight*100).ToString("F0")+" cm / near edge "+(parameters.tableNearEdge*100).ToString("F0")+" cm\n"+(parameters.tableAlignmentConfirmed?"Table placement confirmed by researcher":"TABLE UNCALIBRATED - align to a REAL table first")+"\nElbows/shoulders are estimates. No clinician-hand tracking yet.";
            }
        }
        void Load(bool allowAlignment)
        {
            try
            {
                string json=File.ReadAllText(settingsPath);
                var candidate=JsonUtility.FromJson<SeatedParameters>(json);
                // Upgrade older files without losing the wearer's chosen table height.
                if(candidate!=null && !json.Contains("\"armFullness\""))candidate.armFullness=1.3f;
                if(candidate!=null && !json.Contains("\"tableSupportEnabled\""))candidate.tableSupportEnabled=true;
                if(candidate!=null && !json.Contains("\"tableContactEnabled\""))candidate.tableContactEnabled=true;
                if(candidate==null||!candidate.Valid())throw new ArgumentException("Setup dimensions are outside supported ranges.");
                bool requested=candidate.requestAlignment;
                candidate.requestAlignment=false;
                bool conditionChanged = candidate.movementCondition != parameters.movementCondition || candidate.showSourceHand != parameters.showSourceHand;
                parameters=candidate;
                // Consume this file version before side effects, even if acknowledgement cannot be saved.
                lastWrite=File.GetLastWriteTimeUtc(settingsPath);
                if(conditionChanged) Debug.Log($"[Movement] t={Time.realtimeSinceStartupAsDouble:F3} condition={parameters.movementCondition} showSource={parameters.showSourceHand}");
                if(allowAlignment&&requested)Align();
                else Changed?.Invoke();
                Save();
            }
            catch(Exception e) when(e is IOException||e is ArgumentException||e is UnauthorizedAccessException)
            {
                Message="Setup rejected; retaining previous parameters";
                Debug.LogWarning("[Seated] "+Message+": "+e.Message);
                if(File.Exists(settingsPath))lastWrite=File.GetLastWriteTimeUtc(settingsPath);
            }
        }
        void Save()
        {
            parameters.requestAlignment=false;
            try
            {
                File.WriteAllText(settingsPath,JsonUtility.ToJson(parameters,true));
                lastWrite=File.GetLastWriteTimeUtc(settingsPath);
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Seated] Settings applied, but acknowledgement could not be saved: "+e.Message);
            }
        }
        public void SetTableContact(bool enabled)
        {parameters.tableContactEnabled=enabled;Changed?.Invoke();Save();Debug.Log("[TableRest] Visual assist="+enabled);}
        public void SetParticipantHands(ParticipantHands value)
        {
            if(!Enum.IsDefined(typeof(ParticipantHands),value))throw new ArgumentOutOfRangeException(nameof(value));
            parameters.participantHands=value;parameters.tableAlignmentConfirmed=false;
            Changed?.Invoke();Save();
        }
        public void SetMovement(MovementCondition condition,bool showSource)
        {
            if(!Enum.IsDefined(typeof(MovementCondition),condition))throw new ArgumentOutOfRangeException(nameof(condition));
            parameters.movementCondition=condition;parameters.showSourceHand=showSource;
            Debug.Log($"[Movement] t={Time.realtimeSinceStartupAsDouble:F3} condition={condition} showSource={showSource}; testing control, not a trial event.");
            Changed?.Invoke();Save();
        }
        public void AdjustTableHeight(float metres)
        {
            SurfacePreview=true;
            parameters.tableHeight=Mathf.Clamp(parameters.tableHeight+metres,.45f,1.1f);
            parameters.tableAlignmentConfirmed=false;
            Debug.Log($"[Seated] Table height adjusted to {parameters.tableHeight:F3} m; still uncalibrated.");
            Changed?.Invoke();Save();
        }
        public void BeginParticipantSetup()
        {
            Aligned=false;SurfacePreview=false;ShowFitMarkers=false;parameters.tableAlignmentConfirmed=false;
            parameters.participantHands=ParticipantHands.Both;
            parameters.movementCondition=MovementCondition.Normal;parameters.showSourceHand=true;
            Message="New wearer: alignment and table confirmation required";
            Changed?.Invoke();Save();
        }
        public void ConfirmTable()
        {
            if(!Aligned || !HeadTracked)return;
            parameters.tableAlignmentConfirmed=true;Message="Table placement confirmed by wearer/helper";
            Changed?.Invoke();Save();
        }
        public void AdjustTableDistance(float metres)
        {
            parameters.tableNearEdge=Mathf.Clamp(parameters.tableNearEdge+metres,.05f,.65f);
            parameters.tableAlignmentConfirmed=false;Changed?.Invoke();Save();
        }
        public void AdjustArmFullness(float amount)
        {parameters.armFullness=Mathf.Clamp(parameters.armFullness+amount,.8f,1.8f);Changed?.Invoke();Save();}
        // Floor is required because table heights are measured from the physical floor.
        // Re-request it after resume/recenter; never substitute a device-relative origin.
        public bool PrepareFloorOrigin()
        {
            inputs.Clear();SubsystemManager.GetSubsystems(inputs);
            foreach(var input in inputs)
            {
                if(!input.running)continue;
                var mode=input.GetTrackingOriginMode();
                if(mode==TrackingOriginModeFlags.Floor)return true;
                var supported=input.GetSupportedTrackingOriginModes();
                bool accepted=(supported & TrackingOriginModeFlags.Floor)!=0 && input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                Debug.Log($"[Seated] Floor request: previous={mode} supported={supported} accepted={accepted} current={input.GetTrackingOriginMode()}");
                if(input.GetTrackingOriginMode()==TrackingOriginModeFlags.Floor)return true;
            }
            return false;
        }
        public void Align()
        {
            Aligned=false;parameters.tableAlignmentConfirmed=false;
            inputs.Clear();SubsystemManager.GetSubsystems(inputs);
            var floorInput=inputs.Find(x=>x.running&&x.GetTrackingOriginMode()==TrackingOriginModeFlags.Floor);
            if(!HeadTracked || floorInput==null)
            {
                Message=!HeadTracked ? "Head tracking lost. Look around, then retry." : "Floor tracking unavailable. Set floor level in Quest\nBoundary settings, return here and retry alignment.";
                foreach(var input in inputs)Debug.LogWarning($"[Seated] Alignment origin: running={input.running} mode={input.GetTrackingOriginMode()} supported={input.GetSupportedTrackingOriginModes()} headTracked={HeadTracked}");
                Debug.LogWarning("[Seated] "+Message);Changed?.Invoke();return;
            }
            Vector3 head=trackingSpace.InverseTransformPoint(headCamera.position);
            Vector3 forward=Vector3.ProjectOnPlane(trackingSpace.InverseTransformDirection(headCamera.forward),Vector3.up);
            if(forward.sqrMagnitude<.1f||head.y<.65f||head.y>1.65f)
            {Message="Sit comfortably and look straight ahead, then request alignment";Debug.LogWarning("[Seated] "+Message);return;}
            parameters.tableAlignmentConfirmed=false;
            BodyFrame.localPosition=new Vector3(head.x,0,head.z);
            BodyFrame.localRotation=Quaternion.LookRotation(forward.normalized,Vector3.up);
            if(alignedInput!=null)alignedInput.trackingOriginUpdated-=OriginChanged;
            alignedInput=floorInput;
            if(alignedInput!=null)alignedInput.trackingOriginUpdated+=OriginChanged;
            SeatedHeadHeight=head.y;Aligned=true;
            Message="Body aligned / movement condition shown above";
            Debug.Log($"[Seated] Alignment accepted t={Time.realtimeSinceStartupAsDouble:F3} headHeight={head.y:F3} bodyPosition={BodyFrame.localPosition} yaw={BodyFrame.localEulerAngles.y:F1} tableConfirmed={parameters.tableAlignmentConfirmed}");
            Changed?.Invoke();
        }
        void OriginChanged(XRInputSubsystem input){Aligned=false;Message="Tracking origin changed; researcher re-alignment required";Changed?.Invoke();}
        void OnDestroy(){if(alignedInput!=null)alignedInput.trackingOriginUpdated-=OriginChanged;}
        void OnApplicationPause(bool paused){if(paused){Aligned=false;Message="Resume requires researcher re-alignment";Changed?.Invoke();}}
    }
}
