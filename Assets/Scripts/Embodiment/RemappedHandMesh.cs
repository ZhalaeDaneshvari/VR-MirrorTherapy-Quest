using UnityEngine;

namespace QuestResearch.Embodiment
{
    // Bone references are resolved and serialized by the editor, never globally searched at runtime.
    // This renderer reads displayed targets only; it does not subscribe to XR hand tracking.
    public sealed class RemappedHandMesh : MonoBehaviour
    {
        [SerializeField] Transform[] joints;
        [SerializeField] SkinnedMeshRenderer[] meshes;
        public Transform[] Joints => joints;
        public void Configure(Transform[] bones, SkinnedMeshRenderer[] renderers)
        {
            joints = bones;
            meshes = renderers;
        }
        public void Apply(DisplayedHandPose pose, Transform trackingSpace, bool headTracked)
        {
            bool valid = headTracked;
            for (int i = 0; i < joints.Length; i++) valid &= pose.Valid[i];
            // A connected skin cannot hide one invalid joint without inventing a surface.
            // Hide the whole mesh on partial data instead of retaining stale finger geometry.
            foreach (var mesh in meshes) mesh.enabled = valid;
            if (!valid) return;
            for (int i = 0; i < joints.Length; i++)
                joints[i].SetPositionAndRotation(trackingSpace.TransformPoint(pose.Joints[i].position),
                    trackingSpace.rotation * pose.Joints[i].rotation);
        }
    }
}
