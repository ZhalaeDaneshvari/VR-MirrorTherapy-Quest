using UnityEngine;

namespace ClassEmbodimentDemo
{
    public enum MovementCondition { Congruent, LeftToRight, RightToLeft }

    // All poses here are in a simulated participant body frame: +X right, +Y up, +Z forward.
    // This demo frame is authored, not calibrated from a real participant.
    public static class DemoMovementMapping
    {
        public static Vector3 Reflect(Vector3 point) => new Vector3(-point.x, point.y, point.z);

        // S R S, where S = diag(-1,1,1). Two reflections yield a proper rotation.
        // Valid for our symmetric procedural hands with explicitly mirrored thumb geometry;
        // arbitrary avatar skeletons require separate anatomical bone-basis calibration.
        public static Quaternion Reflect(Quaternion rotation) =>
            Quaternion.LookRotation(Reflect(rotation * Vector3.forward), Reflect(rotation * Vector3.up));

        public static Pose RestingPose(bool right) => new Pose(new Vector3(right ? .35f : -.35f, .98f, .29f), Quaternion.identity);

        public static void Map(MovementCondition condition, Pose left, Pose right, out Pose displayedLeft, out Pose displayedRight, bool oppositeOnly = false)
        {
            displayedLeft = left;
            displayedRight = right;
            // Opposite-only parks the unused virtual arm in a fixed neutral pose; it does not copy input.
            if (condition == MovementCondition.LeftToRight)
                {
                displayedRight = new Pose(Reflect(left.position), Reflect(left.rotation));
                if (oppositeOnly) displayedLeft = RestingPose(false);
            }
            if (condition == MovementCondition.RightToLeft)
                {
                displayedLeft = new Pose(Reflect(right.position), Reflect(right.rotation));
                if (oppositeOnly) displayedRight = RestingPose(true);
            }
        }
    }
}
