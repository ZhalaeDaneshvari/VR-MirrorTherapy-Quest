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
