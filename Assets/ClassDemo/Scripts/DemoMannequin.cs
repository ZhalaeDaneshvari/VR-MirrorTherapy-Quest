using UnityEngine;

namespace ClassEmbodimentDemo
{
    // Deliberately schematic body, not a validated anatomical avatar or clinical IK solver.
    public sealed class DemoMannequin
    {
        public Transform Root { get; }
        public Transform Head { get; }
        readonly Transform[] upper = new Transform[2], lower = new Transform[2], palms = new Transform[2];
        readonly Vector3[] elbows = new Vector3[2], wrists = new Vector3[2];
        const float UpperLength = .30f, LowerLength = .28f;

        public DemoMannequin(string name, Vector3 position, Material body, Material skin, Material chair)
        {
            Root = new GameObject(name).transform;
            Root.position = position;
            Part("Torso", PrimitiveType.Capsule, new Vector3(0,1.13f,0), new Vector3(.43f,.29f,.25f), body);
            Part("Shoulders", PrimitiveType.Sphere, new Vector3(0,1.36f,0), new Vector3(.55f,.17f,.24f), body);
            Part("Hips", PrimitiveType.Sphere, new Vector3(0,.83f,0), new Vector3(.39f,.22f,.27f), body);
            Part("Neck", PrimitiveType.Cylinder, new Vector3(0,1.48f,0), new Vector3(.09f,.055f,.09f), skin);
            Head = Part("Head", PrimitiveType.Sphere, new Vector3(0,1.66f,0), new Vector3(.19f,.24f,.20f), skin);
            Part("Seat", PrimitiveType.Cube, new Vector3(0,.69f,0), new Vector3(.52f,.07f,.48f), chair);
            Part("Chair back", PrimitiveType.Cube, new Vector3(0,.89f,-.23f), new Vector3(.50f,.30f,.06f), chair);
            for (int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                var thigh=Part("Thigh",PrimitiveType.Capsule,Vector3.zero,Vector3.one,body);
                Segment(thigh,new Vector3(side*.13f,.82f,.04f),new Vector3(side*.15f,.73f,.40f),.14f);
                var shin=Part("Shin",PrimitiveType.Capsule,Vector3.zero,Vector3.one,body);
                Segment(shin,new Vector3(side*.15f,.73f,.40f),new Vector3(side*.15f,.13f,.43f),.10f);
                Part("Foot",PrimitiveType.Cube,new Vector3(side*.15f,.075f,.50f),new Vector3(.12f,.10f,.25f),chair);
                upper[i]=Part(i==0?"Left upper arm":"Right upper arm",PrimitiveType.Capsule,Vector3.zero,Vector3.one,skin);
                lower[i]=Part(i==0?"Left forearm":"Right forearm",PrimitiveType.Capsule,Vector3.zero,Vector3.one,skin);
                palms[i]=new GameObject(i==0?"Left hand":"Right hand").transform;
                palms[i].SetParent(Root,false);
                var palm=Part("Palm",PrimitiveType.Cube,Vector3.zero,new Vector3(.083f,.033f,.095f),skin);
                palm.SetParent(palms[i],false);
                for(int f=0;f<4;f++)
                {
                    var finger=Part("Finger "+f,PrimitiveType.Capsule,Vector3.zero,Vector3.one,skin);
                    finger.SetParent(palms[i],false);
                    float length=f==0||f==3?.064f:.08f;
                    Segment(finger,new Vector3((f-1.5f)*.020f,0,.038f),new Vector3((f-1.5f)*.020f,0,.038f+length),.015f);
                }
                var thumb=Part("Thumb",PrimitiveType.Capsule,Vector3.zero,Vector3.one,skin);
                thumb.SetParent(palms[i],false);
                Segment(thumb,new Vector3(-side*.035f,0,0),new Vector3(-side*.078f,0,.035f),.021f);
            }
        }

        Transform Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(Root,false);
            go.transform.localPosition=position; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            Object.Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        public void SetPose(int index, Pose pose)
        {
            float side=index==0?-1:1;
            Vector3 shoulder=new Vector3(side*.245f,1.40f,0);
            Vector3 delta=pose.position-shoulder;
            float distance=Mathf.Clamp(delta.magnitude,.04f,UpperLength+LowerLength-.002f);
            Vector3 direction=delta.sqrMagnitude>.00001f?delta.normalized:Vector3.forward;
            Vector3 wrist=shoulder+direction*distance;
            // Analytic two-bone position IK; fixed outward pole makes the demonstration predictable.
            Vector3 pole=new Vector3(side,-.65f,-.35f);
            Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
            if(bend.sqrMagnitude<.001f) bend=Vector3.ProjectOnPlane(Vector3.up,direction).normalized;
            float along=(UpperLength*UpperLength-LowerLength*LowerLength+distance*distance)/(2*distance);
            Vector3 elbow=shoulder+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,UpperLength*UpperLength-along*along));
            elbows[index]=elbow; wrists[index]=wrist;
            Segment(upper[index],shoulder,elbow,.085f); Segment(lower[index],elbow,wrist,.068f);
            palms[index].localPosition=wrist; palms[index].localRotation=pose.rotation;
        }

        public Vector3 ForearmPoint(int side, float fraction) => Root.TransformPoint(Vector3.Lerp(elbows[side],wrists[side],fraction)+Vector3.up*.043f);

        static void Segment(Transform part, Vector3 from, Vector3 to, float diameter)
        {
            part.localPosition=(from+to)*.5f;
            part.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);
            part.localScale=new Vector3(diameter,Vector3.Distance(from,to)*.5f,diameter);
        }
    }
}
