using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Hands;

namespace QuestResearch.Embodiment
{
    // Bone references are resolved and serialized by the editor, never globally searched at runtime.
    // This renderer reads displayed targets only; it does not subscribe to XR hand tracking.
    public sealed class RemappedHandMesh : MonoBehaviour
    {
        [SerializeField] Transform[] joints;
        [SerializeField] SkinnedMeshRenderer[] meshes;
        [SerializeField] Vector3[] wristSeamPoints, wristSeamNormals;
        public Vector3[] WristSeamPoints => wristSeamPoints;
        public Vector3[] WristSeamNormals => wristSeamNormals;
        [SerializeField] int[] seamVertexIndices;
        Vector3[] bindPoints, bindNormals;
        BoneWeight[] weights;
        Matrix4x4[] bindPoses, skinMatrices;
        Transform[] skinBones;
        public int[] SeamVertexIndices => seamVertexIndices;
        public void ConfigureSeam(Vector3[] points, Vector3[] normals, int[] indices)
        { wristSeamPoints=points;wristSeamNormals=normals;seamVertexIndices=indices; }
        void UpdateSeam()
        {
            // CPU-skin only the small boundary ring using the same four influences as
            // the renderer. Never read back the whole GPU-skinned hand per frame.
            if(bindPoints==null)
            {
                var mesh=meshes[0].sharedMesh;
                var vertices=mesh.vertices;var normals=mesh.normals;var allWeights=mesh.boneWeights;
                bindPoints=new Vector3[seamVertexIndices.Length];bindNormals=new Vector3[bindPoints.Length];weights=new BoneWeight[bindPoints.Length];
                for(int i=0;i<bindPoints.Length;i++){int index=seamVertexIndices[i];bindPoints[i]=vertices[index];bindNormals[i]=normals[index];weights[i]=allWeights[index];}
                bindPoses=mesh.bindposes;skinBones=meshes[0].bones;skinMatrices=new Matrix4x4[bindPoses.Length];
            }
            for(int i=0;i<skinMatrices.Length;i++)skinMatrices[i]=skinBones[i].localToWorldMatrix*bindPoses[i];
            var wrist=joints[UnityEngine.XR.Hands.XRHandJointID.Wrist.ToIndex()];
            for(int i=0;i<bindPoints.Length;i++)
            {
                var w=weights[i];Vector3 position=Vector3.zero,normal=Vector3.zero;
                Accumulate(w.boneIndex0,w.weight0,bindPoints[i],bindNormals[i],ref position,ref normal);
                Accumulate(w.boneIndex1,w.weight1,bindPoints[i],bindNormals[i],ref position,ref normal);
                Accumulate(w.boneIndex2,w.weight2,bindPoints[i],bindNormals[i],ref position,ref normal);
                Accumulate(w.boneIndex3,w.weight3,bindPoints[i],bindNormals[i],ref position,ref normal);
                wristSeamPoints[i]=wrist.InverseTransformPoint(position);
                wristSeamNormals[i]=wrist.InverseTransformDirection(normal).normalized;
            }
        }
        void Accumulate(int bone,float weight,Vector3 point,Vector3 direction,ref Vector3 position,ref Vector3 normal)
        {
            if(weight<=0)return;
            position+=weight*skinMatrices[bone].MultiplyPoint3x4(point);
            normal+=weight*skinMatrices[bone].MultiplyVector(direction);
        }
        public float HighestOverTable {get;private set;}=float.NegativeInfinity;
        Mesh contactMesh;
        readonly List<Vector3> contactVertices=new List<Vector3>();
        // Query actual deformed skin, including fingertips, rather than joint centres.
        public float LowestOverTable(Transform body,SeatedParameters table)
        {
            HighestOverTable=float.NegativeInfinity;
            if(!Visible)return float.PositiveInfinity;
            if(contactMesh==null)contactMesh=new Mesh{name="Reusable hand contact query"};
            float lowest=float.PositiveInfinity;
            foreach(var renderer in meshes)
            {
                renderer.BakeMesh(contactMesh);contactMesh.GetVertices(contactVertices);
                Matrix4x4 toBody=body.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                foreach(var vertex in contactVertices)
                {
                    var p=toBody.MultiplyPoint3x4(vertex);
                    if(TableContactMath.Inside(p,table)){lowest=Mathf.Min(lowest,p.y);HighestOverTable=Mathf.Max(HighestOverTable,p.y);}
                }
            }
            return lowest;
        }
        void OnDestroy(){if(contactMesh!=null)Destroy(contactMesh);}
        public bool Visible { get; private set; }
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
            Visible=valid;
            foreach (var mesh in meshes) mesh.enabled = valid;
            if (!valid) return;
            for (int i = 0; i < joints.Length; i++)
                joints[i].SetPositionAndRotation(trackingSpace.TransformPoint(pose.Joints[i].position),
                    trackingSpace.rotation * pose.Joints[i].rotation);
            UpdateSeam();
        }
    }
}
