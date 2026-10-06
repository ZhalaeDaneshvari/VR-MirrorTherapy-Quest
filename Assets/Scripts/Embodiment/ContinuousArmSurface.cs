using UnityEngine;
using UnityEngine.Rendering;

namespace QuestResearch.Embodiment
{
    // A single tapered surface replaces disconnected cylinders. The centreline is still IK,
    // not a measured forearm surface or a biomechanical model.
    public sealed class ContinuousArmSurface : MonoBehaviour
    {
        const int Rings = 17;
        int Sides;
        Vector3[] vertices, normals, seamPoints, seamNormals;
        Vector3 seamCentre;
        Mesh mesh;
        public void Initialize(Material material, Vector3[] wristPoints, Vector3[] wristNormals)
        {
            seamPoints=wristPoints;seamNormals=wristNormals;Sides=seamPoints.Length;
            vertices=new Vector3[Rings*Sides];normals=new Vector3[Rings*Sides];
            foreach(var point in seamPoints)seamCentre+=point;
            seamCentre/=Sides;
            mesh = new Mesh { name = "Procedural continuous arm" };
            mesh.MarkDynamic();
            var triangles = new int[(Rings - 1) * Sides * 6];
            int n = 0;
            for (int r = 0; r < Rings - 1; r++)
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * Sides + s, b = r * Sides + (s + 1) % Sides;
                    int c = a + Sides, d = b + Sides;
                    triangles[n++] = a; triangles[n++] = b; triangles[n++] = c;
                    triangles[n++] = b; triangles[n++] = d; triangles[n++] = c;
                }
            mesh.vertices = vertices; mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        public void Apply(Vector3 shoulder, Vector3 elbow, Vector3 wrist, Quaternion wristRotation)
        {
            seamCentre=Vector3.zero;
            foreach(var point in seamPoints)seamCentre+=point;
            seamCentre/=Sides;
            Vector3 wristAcross=wristRotation*Vector3.right;
            wrist+=wristRotation*seamCentre;
            Vector3 upper = (elbow - shoulder).normalized;
            Vector3 lower = (wrist - elbow).normalized;
            for (int r = 0; r < Rings; r++)
            {
                float t = r / (float)(Rings - 1);
                Vector3 centre, tangent;
                Vector3 bendStart = Vector3.Lerp(shoulder, elbow, .84f);
                Vector3 bendEnd = Vector3.Lerp(elbow, wrist, .16f);
                if (t < .42f) { centre = Vector3.Lerp(shoulder, bendStart, t / .42f); tangent = upper; }
                else if (t > .58f) { centre = Vector3.Lerp(bendEnd, wrist, (t - .58f) / .42f); tangent = lower; }
                else
                {
                    float u = (t - .42f) / .16f;
                    centre = (1-u)*(1-u)*bendStart + 2*(1-u)*u*elbow + u*u*bendEnd;
                    tangent = ((1-u)*(elbow-bendStart) + u*(bendEnd-elbow)).normalized;
                }
                Vector3 across = Vector3.ProjectOnPlane(wristAcross, tangent).normalized;
                if (across.sqrMagnitude < .1f) across = Vector3.Cross(tangent, Vector3.forward).normalized;
                if (across.sqrMagnitude < .1f) across = Vector3.Cross(tangent, Vector3.up).normalized;
                Vector3 depth = Vector3.Cross(tangent, across).normalized;
                float radius = t <= .5f ? Mathf.Lerp(.044f, .030f, t * 2) : Mathf.Lerp(.030f, .023f, (t - .5f) * 2) + .004f * Mathf.Sin((t - .5f) * 2 * Mathf.PI);
                float flat = Mathf.Lerp(.95f, .68f, t);
                for (int s = 0; s < Sides; s++)
                {
                    Vector3 profile=seamPoints[s]-seamCentre;
                    float angle = Mathf.Atan2(profile.y,profile.x);
                    int i = r * Sides + s;
                    float join=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,t));
                    Vector3 ellipse=radius*(across*Mathf.Cos(angle)+depth*Mathf.Sin(angle)*flat);
                    vertices[i]=centre+Vector3.Lerp(ellipse,wristRotation*profile,join);
                    Vector3 radial=(across*Mathf.Cos(angle)+depth*Mathf.Sin(angle)/flat).normalized;
                    normals[i]=Vector3.Lerp(radial,wristRotation*seamNormals[s],join).normalized;
                }
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds();
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
