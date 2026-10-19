using UnityEngine;
namespace QuestResearch.Embodiment
{
    // Small reusable mesh: clockwise fill, no allocations or texture rebuilding during gaze.
    public sealed class GazeProgressRing : MonoBehaviour
    {
        const int Segments=48;
        readonly Vector3[] vertices=new Vector3[(Segments+1)*2];
        Mesh mesh;Material material;
        public void Initialize(Shader shader)
        {
            mesh=new Mesh{name="Gaze dwell progress"};mesh.MarkDynamic();
            int[] indices=new int[Segments*6];
            for(int i=0;i<Segments;i++){int v=i*2,t=i*6;indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v+1;indices[t+4]=v+3;indices[t+5]=v+2;}
            mesh.vertices=vertices;mesh.triangles=indices;
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(shader){color=new Color(.2f,1f,.85f),renderQueue=4030};
            gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            gameObject.SetActive(false);
        }
        public void SetProgress(Vector3 position,float progress,bool visible)
        {
            gameObject.SetActive(visible);if(!visible)return;
            transform.localPosition=position;
            for(int i=0;i<=Segments;i++)
            {
                float angle=2*Mathf.PI*i/Segments*Mathf.Clamp01(progress);
                var direction=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
                vertices[i*2]=direction*.012f;vertices[i*2+1]=direction*.017f;
            }
            mesh.vertices=vertices;mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
