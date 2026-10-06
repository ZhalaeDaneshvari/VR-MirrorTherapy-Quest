using System;
using UnityEngine;
using UnityEngine.XR.Hands;
using QuestResearch.Hands;

namespace QuestResearch.Embodiment
{
    // Output targets are deliberately separate from the provider snapshots, even for 1:1 mapping.
    public sealed class DisplayedHandPose
    {
        public readonly Pose[] Joints=new Pose[XRHandJointID.EndMarker.ToIndex()];
        public readonly bool[] Valid=new bool[XRHandJointID.EndMarker.ToIndex()];
        public ulong InputSequence { get; internal set; }
        public void CopyFrom(TrackingHandFrame input)
        {
            Array.Copy(input.JointPoses,Joints,Joints.Length);
            Array.Copy(input.JointValid,Valid,Valid.Length);
            InputSequence=input.Sequence;
        }
        public void Invalidate(){Array.Clear(Valid,0,Valid.Length);}
    }
    public sealed class NormalHandMapping : MonoBehaviour
    {
        [SerializeField] HandTrackingSource source;
        public DisplayedHandPose Left { get; }=new DisplayedHandPose();
        public DisplayedHandPose Right { get; }=new DisplayedHandPose();
        public event Action Changed;
        public void Configure(HandTrackingSource input){source=input;}
        void OnEnable(){if(source!=null){source.Changed+=Map;Map();}}
        void Map(){Left.CopyFrom(source.Left);Right.CopyFrom(source.Right);Changed?.Invoke();}
        void OnDisable(){if(source!=null)source.Changed-=Map;Left.Invalidate();Right.Invalidate();Changed?.Invoke();}
    }
}
