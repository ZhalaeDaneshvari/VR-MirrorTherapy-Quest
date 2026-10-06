using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

namespace QuestResearch.Embodiment
{
    [Serializable]
    public sealed class SeatedParameters
    {
        public float tableHeight=.74f,tableWidth=1.10f,tableDepth=.65f,tableNearEdge=.18f;
        public float shoulderWidth=.38f,shoulderDrop=.22f,upperArmLength=.30f,forearmLength=.26f;
        public bool tableAlignmentConfirmed=false;
        public bool requestAlignment=false;
        public bool Valid()
        {
            return Between(tableHeight,.45f,1.1f)&&Between(tableWidth,.6f,2f)&&Between(tableDepth,.35f,1.2f)
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
        public float SeatedHeadHeight { get; private set; }
        public bool HeadTracked { get; private set; }
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
                label.text="M3  SEATED EMBODIMENT - NORMAL MAPPING\n"+Message+"\nTable height "+(parameters.tableHeight*100).ToString("F0")+" cm / near edge "+(parameters.tableNearEdge*100).ToString("F0")+" cm\n"+(parameters.tableAlignmentConfirmed?"Table placement confirmed by researcher":"TABLE UNCALIBRATED - align to a REAL table first")+"\nElbows/shoulders are estimates. No clinician-hand tracking yet.";
            }
        }
        void Load(bool allowAlignment)
        {
            try
            {
                var candidate=JsonUtility.FromJson<SeatedParameters>(File.ReadAllText(settingsPath));
                if(candidate==null||!candidate.Valid())throw new ArgumentException("Setup dimensions are outside supported ranges.");
                bool requested=candidate.requestAlignment;
                candidate.requestAlignment=false;parameters=candidate;
                if(allowAlignment&&requested)Align();
                else Changed?.Invoke();
                Save();
            }
            catch(Exception e) when(e is IOException||e is ArgumentException)
            {
                Message="Setup rejected; retaining previous parameters";
                Debug.LogWarning("[Seated] "+Message+": "+e.Message);
                if(File.Exists(settingsPath))lastWrite=File.GetLastWriteTimeUtc(settingsPath);
            }
        }
        void Save()
        {
            parameters.requestAlignment=false;
            File.WriteAllText(settingsPath,JsonUtility.ToJson(parameters,true));lastWrite=File.GetLastWriteTimeUtc(settingsPath);
        }
        public void Align()
        {
            inputs.Clear();SubsystemManager.GetSubsystems(inputs);
            if(!HeadTracked||!inputs.Exists(x=>x.running&&x.GetTrackingOriginMode()==TrackingOriginModeFlags.Floor))
            {Message="Alignment rejected: valid head tracking and Floor origin required";Debug.LogWarning("[Seated] "+Message);return;}
            Vector3 head=trackingSpace.InverseTransformPoint(headCamera.position);
            Vector3 forward=Vector3.ProjectOnPlane(trackingSpace.InverseTransformDirection(headCamera.forward),Vector3.up);
            if(forward.sqrMagnitude<.1f||head.y<.65f||head.y>1.65f)
            {Message="Sit comfortably and look straight ahead, then request alignment";Debug.LogWarning("[Seated] "+Message);return;}
            BodyFrame.localPosition=new Vector3(head.x,0,head.z);
            BodyFrame.localRotation=Quaternion.LookRotation(forward.normalized,Vector3.up);
            if(alignedInput!=null)alignedInput.trackingOriginUpdated-=OriginChanged;
            alignedInput=inputs.Find(x=>x.running);
            if(alignedInput!=null)alignedInput.trackingOriginUpdated+=OriginChanged;
            SeatedHeadHeight=head.y;Aligned=true;
            Message="Body aligned / normal left-to-left and right-to-right";
            Debug.Log($"[Seated] Alignment accepted t={Time.realtimeSinceStartupAsDouble:F3} headHeight={head.y:F3} bodyPosition={BodyFrame.localPosition} yaw={BodyFrame.localEulerAngles.y:F1} tableConfirmed={parameters.tableAlignmentConfirmed}");
            Changed?.Invoke();
        }
        void OriginChanged(XRInputSubsystem input){Aligned=false;Message="Tracking origin changed; researcher re-alignment required";Changed?.Invoke();}
        void OnDestroy(){if(alignedInput!=null)alignedInput.trackingOriginUpdated-=OriginChanged;}
        void OnApplicationPause(bool paused){if(paused){Aligned=false;Message="Resume requires researcher re-alignment";Changed?.Invoke();}}
    }
}
