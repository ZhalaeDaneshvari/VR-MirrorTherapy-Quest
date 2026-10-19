using System;
using UnityEngine;
using UnityEngine.XR.Hands;
using QuestResearch.Hands;

namespace QuestResearch.Embodiment
{
    public enum MovementCondition { Normal, LeftToRight, RightToLeft, SwapBoth }
    public enum PhysicalHand { Left, Right }

    // Mutable provider snapshots and displayed targets never share pose arrays.
    public sealed class DisplayedHandPose
    {
        public readonly Pose[] Joints = new Pose[XRHandJointID.EndMarker.ToIndex()];
        public readonly bool[] Valid = new bool[XRHandJointID.EndMarker.ToIndex()];
        public ulong InputSequence { get; internal set; }
        public PhysicalHand SourceHand { get; internal set; }
        public bool Reflected { get; internal set; }
        public void CopyFrom(TrackingHandFrame input)
        {
            InputSequence=input.Sequence;
            Array.Copy(input.JointPoses, Joints, Joints.Length);
            Array.Copy(input.JointValid, Valid, Valid.Length);
            InputSequence = input.Sequence;
            Reflected = false;
        }
        public void Invalidate() { Array.Clear(Valid, 0, Valid.Length); }
    }

    public static class MovementReflection
    {
        // bodyInTracking is a rigid metre-scale pose, not an arbitrary world axis.
        // S=diag(-1,1,1). Position: b + B S B^-1 (p-b).
        // Orientation: B S (B^-1 R) S. Both reflections are necessary:
        // one reflection alone has determinant -1 and is not a quaternion rotation.
        public static Pose Reflect(Pose input, Pose bodyInTracking)
        {
            Quaternion inverse = Quaternion.Inverse(bodyInTracking.rotation);
            Vector3 local = inverse * (input.position - bodyInTracking.position);
            local.x = -local.x;
            Quaternion rotation = inverse * input.rotation;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;
            forward.x = -forward.x;
            up.x = -up.x;
            return new Pose(bodyInTracking.position + bodyInTracking.rotation * local,
                bodyInTracking.rotation * Quaternion.LookRotation(forward, up));
        }

        static void ReflectInto(TrackingHandFrame input,DisplayedHandPose target,PhysicalHand source,Pose body)
        {
            target.CopyFrom(input);target.SourceHand=source;target.Reflected=true;
            for(int i=0;i<target.Joints.Length;i++)if(target.Valid[i])target.Joints[i]=Reflect(input.JointPoses[i],body);
        }
        public static void Map(TrackingHandFrame left, TrackingHandFrame right,
            DisplayedHandPose outputLeft, DisplayedHandPose outputRight,
            MovementCondition condition, bool showSource, bool aligned, Pose body)
        {
            outputLeft.CopyFrom(left); outputLeft.SourceHand = PhysicalHand.Left;
            outputRight.CopyFrom(right); outputRight.SourceHand = PhysicalHand.Right;
            if (condition == MovementCondition.Normal) return;
            // Never fall back silently to congruent motion when the mirror frame is invalid.
            if (!aligned) { outputLeft.Invalidate(); outputRight.Invalidate(); return; }
            if(condition==MovementCondition.SwapBoth)
            {
                ReflectInto(right,outputLeft,PhysicalHand.Right,body);
                ReflectInto(left,outputRight,PhysicalHand.Left,body);
                return; // Both outputs are independent destinations; source visibility does not apply.
            }
            var input = condition == MovementCondition.LeftToRight ? left : right;
            var target = condition == MovementCondition.LeftToRight ? outputRight : outputLeft;
            target.CopyFrom(input);
            target.SourceHand = condition == MovementCondition.LeftToRight ? PhysicalHand.Left : PhysicalHand.Right;
            target.Reflected = true;
            for (int i = 0; i < target.Joints.Length; i++)
                if (target.Valid[i]) target.Joints[i] = Reflect(input.JointPoses[i], body);
            if (!showSource)
                (condition == MovementCondition.LeftToRight ? outputLeft : outputRight).Invalidate();
        }
    }

    public sealed class MovementRemapper : MonoBehaviour
    {
        [SerializeField] HandTrackingSource source;
        [SerializeField] SeatedSetup setup;
        [SerializeField] Transform trackingSpace;
        public DisplayedHandPose Left { get; } = new DisplayedHandPose();
        public DisplayedHandPose Right { get; } = new DisplayedHandPose();
        public Transform TrackingSpace=>trackingSpace;
        public event Action Changed;
        public void Configure(HandTrackingSource input) { source = input; }
        public void ConfigureFrame(SeatedSetup alignment, Transform space) { setup = alignment; trackingSpace = space; }
        void OnEnable()
        {
            if (source != null) source.Changed += Map;
            if (setup != null) setup.Changed += Map;
            Map();
        }
        void Map()
        {
            if (source == null) return;
            Pose frame = Pose.identity;
            bool aligned = setup != null && setup.Aligned && setup.HeadTracked;
            if (aligned)
                frame = new Pose(trackingSpace.InverseTransformPoint(setup.BodyFrame.position),
                    Quaternion.Inverse(trackingSpace.rotation) * setup.BodyFrame.rotation);
            MovementReflection.Map(source.Left, source.Right, Left, Right,
                setup == null ? MovementCondition.Normal : setup.Parameters.movementCondition,
                setup == null || setup.Parameters.showSourceHand, aligned, frame);
            Changed?.Invoke();
        }
        void OnDisable()
        {
            if (source != null) source.Changed -= Map;
            if (setup != null) setup.Changed -= Map;
            Left.Invalidate(); Right.Invalidate(); Changed?.Invoke();
        }
    }
}
