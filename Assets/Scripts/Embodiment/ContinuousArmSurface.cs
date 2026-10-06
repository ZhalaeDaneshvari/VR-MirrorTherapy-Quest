using UnityEngine;
using UnityEngine.Rendering;

namespace QuestResearch.Embodiment
{
    // A single tapered surface replaces disconnected cylinders. The centreline is still IK,
    // not a measured forearm surface or a biomechanical model.
    public sealed class ContinuousArmSurface : MonoBehaviour
    {
        const int Rings = 17, Sides = 20;
        readonly Vector3[] vertices = new Vector3[Rings * Sides];
        readonly Vector3[] normals = new Vector3[Rings * Sides];
        Mesh mesh;
        public void Initialize(Material material)
        {
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
        public void Apply(Vector3 shoulder, Vector3 elbow, Vector3 wrist, Vector3 wristAcross)
        {
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
                    float angle = s * Mathf.PI * 2 / Sides;
                    int i = r * Sides + s;
                    vertices[i] = centre + radius * (across * Mathf.Cos(angle) + depth * Mathf.Sin(angle) * flat);
                    normals[i] = (across * Mathf.Cos(angle) + depth * Mathf.Sin(angle) / flat).normalized;
                }
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds();
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
