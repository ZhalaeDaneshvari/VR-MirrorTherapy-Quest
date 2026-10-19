using UnityEngine;
using QuestResearch.BodyCalibration;

namespace QuestResearch.Embodiment
{
    // Display mapping for body estimates. Never changes the physical body/hand snapshots.
    public sealed class BodyArmTargets : MonoBehaviour
    {
        [SerializeField] BodyTrackingProbe source;
        [SerializeField] SeatedSetup setup;
        [SerializeField] Transform trackingSpace;
        public bool HasFit {get;private set;}
        readonly float[] upperLength=new float[2],lowerLength=new float[2];
        public Vector2 FittedLengths(int side)=>HasFit?new Vector2(upperLength[side],lowerLength[side]):Vector2.zero;
        public void ClearFit(){HasFit=false;}
        public bool CaptureFit()
        {
            HasFit=false;
            if(Time.realtimeSinceStartupAsDouble-source.ReceivedAt>.25)return false;
            for(int i=0;i<2;i++)
            {
                int o=i*4;
                if(!source.Valid[o+1]||!source.Valid[o+2]||!source.Valid[o+3])return false;
                upperLength[i]=Vector3.Distance(source.Poses[o+1].position,source.Poses[o+2].position);
                lowerLength[i]=Vector3.Distance(source.Poses[o+2].position,source.Poses[o+3].position);
                if(upperLength[i]<.18f||upperLength[i]>.45f||lowerLength[i]<.18f||lowerLength[i]>.4f)return false;
            }
            HasFit=true;
            Debug.Log($"[BodyFit] Estimated fixed lengths L={upperLength[0]:F3}/{lowerLength[0]:F3} R={upperLength[1]:F3}/{lowerLength[1]:F3}; not measured anatomy");
            return true;
        }
        public bool TryGetFitted(DisplayedHandPose hand,Vector3 wrist,out Vector3 shoulder,out Vector3 elbow)
        {
            if(!TryGet(hand,out shoulder,out elbow))return false;
            if(!HasFit)return true;
            int side=hand.SourceHand==PhysicalHand.Left?0:1;
            Vector3 pole=elbow-shoulder;
            // Preserve estimated segment lengths and tracked wrist. Never stretch a mesh
            // across disagreeing body/hand providers. Unreachable poses are withheld.
            if(!SeatedArmMath.TryElbow(shoulder,wrist,upperLength[side],lowerLength[side],pole,out elbow))return false;
            var p=setup.Parameters;
            if(setup.SurfacePreview&&TableContactMath.Inside(wrist,p)&&wrist.y>=p.tableHeight&&wrist.y<p.tableHeight+.10f)
            {
                // Change only elbow bend on the fixed-length IK circle; no vertical bone stretching.
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,.10f,wrist.y-p.tableHeight));
                if(SeatedArmMath.TrySupportedElbow(shoulder,wrist,upperLength[side],lowerLength[side],pole,p.tableHeight+.025f,weight,out var supported))elbow=supported;
            }
            return true;
        }
        public string Status => source.Status+" / "+source.Fidelity;
        public void Configure(BodyTrackingProbe input,SeatedSetup alignment,Transform space)
        {source=input;setup=alignment;trackingSpace=space;}
        public bool TryGet(DisplayedHandPose hand,out Vector3 shoulder,out Vector3 elbow)
        {
            shoulder=elbow=Vector3.zero;
            if(source==null||!source.isActiveAndEnabled||!setup.Aligned||!setup.HeadTracked||Time.realtimeSinceStartupAsDouble-source.ReceivedAt>.25)return false;
            int offset=hand.SourceHand==PhysicalHand.Left?0:4;
            // Upper-arm origin represents the shoulder joint; lower-arm origin the elbow.
            if(!source.Valid[offset+1]||!source.Valid[offset+2])return false;
            Pose frame=new Pose(trackingSpace.InverseTransformPoint(setup.BodyFrame.position),Quaternion.Inverse(trackingSpace.rotation)*setup.BodyFrame.rotation);
            shoulder=ToBody(source.Poses[offset+1],hand.Reflected,frame);
            elbow=ToBody(source.Poses[offset+2],hand.Reflected,frame);
            return IsFinite(shoulder)&&IsFinite(elbow);
        }
        public static Vector3 ToBody(Pose physical,bool reflected,Pose body)
        {
            Pose displayed=reflected?MovementReflection.Reflect(physical,body):physical;
            return Quaternion.Inverse(body.rotation)*(displayed.position-body.position);
        }
        static bool IsFinite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
    }
}
