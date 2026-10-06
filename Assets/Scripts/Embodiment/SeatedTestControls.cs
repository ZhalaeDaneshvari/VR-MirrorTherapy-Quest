using UnityEngine;
using UnityEngine.XR.Hands;
using QuestResearch.Hands;

namespace QuestResearch.Embodiment
{
    // Temporary participant-accessible setup controls for engineering tests.
    // Selection uses HEAD gaze plus a physical thumb/index pinch, never remapped poses.
    // No interaction package, tracked controllers, physics raycasts or global lookup needed.
    public sealed class SeatedTestControls : MonoBehaviour
    {
        [SerializeField] SeatedSetup setup;
        [SerializeField] HandTrackingSource source;
        [SerializeField] Transform head;
        [SerializeField] Material idleMaterial, hoverMaterial;
        [SerializeField] Font font;
        Transform panel;
        TextMesh title;
        readonly Renderer[] buttons = new Renderer[3];
        readonly Vector3[] centres = { new Vector3(0,0,0), new Vector3(-.115f,-.09f,0), new Vector3(.115f,-.09f,0) };
        readonly Vector2[] sizes = { new Vector2(.40f,.065f), new Vector2(.21f,.065f), new Vector2(.21f,.065f) };
        readonly PinchLatch leftPinch = new PinchLatch(), rightPinch = new PinchLatch();
        double alignAt = -1;
        bool placed;
        public void Configure(SeatedSetup alignment, HandTrackingSource tracking, Transform camera, Material idle, Material hover, Font textFont)
        { setup=alignment;source=tracking;head=camera;idleMaterial=idle;hoverMaterial=hover;font=textFont; }
        void Start()
        {
            panel = new GameObject("Testing controls - gaze and pinch").transform;
            panel.gameObject.SetActive(false);
            title = Label("Setup controls",new Vector3(0,.09f,0),.0035f);
            string[] labels = { "REALIGN (3 sec)", "Table -2 cm", "Table +2 cm" };
            for(int i=0;i<3;i++)
            {
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name=labels[i];cube.transform.SetParent(panel,false);
                cube.transform.localPosition=centres[i]+Vector3.forward*.005f;
                cube.transform.localScale=new Vector3(sizes[i].x,sizes[i].y,.008f);
                Destroy(cube.GetComponent<Collider>());
                buttons[i]=cube.GetComponent<Renderer>();buttons[i].sharedMaterial=idleMaterial;
                Label(labels[i],centres[i]-Vector3.forward*.002f,.0045f);
            }
            Label("Look at a button, then pinch thumb + index",new Vector3(0,-.15f,0),.0032f);
        }
        TextMesh Label(string text,Vector3 position,float size)
        {
            var label=new GameObject(text).AddComponent<TextMesh>();label.transform.SetParent(panel,false);
            label.transform.localPosition=position;label.text=text;label.font=font;label.fontSize=64;
            label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
            label.GetComponent<MeshRenderer>().sharedMaterial=font.material;return label;
        }
        void Update()
        {
            if(panel==null)return;
            if(!setup.HeadTracked || !Application.isFocused)
            {
                panel.gameObject.SetActive(false);placed=false;alignAt=-1;
                leftPinch.Reset();rightPinch.Reset();return;
            }
            if(!placed)
            {
                Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.5f)return;
                panel.SetPositionAndRotation(head.position+forward*.65f+Vector3.up*.12f,Quaternion.LookRotation(forward,Vector3.up));
                panel.gameObject.SetActive(true);placed=true;
            }
            bool click=leftPinch.Update(source.Left)|rightPinch.Update(source.Right);
            int hovered=HitButton(panel.InverseTransformPoint(head.position),panel.InverseTransformDirection(head.forward),centres,sizes);
            for(int i=0;i<3;i++)buttons[i].sharedMaterial=i==hovered?hoverMaterial:idleMaterial;
            double now=Time.realtimeSinceStartupAsDouble;
            if(alignAt>=0)
            {
                title.text="Look straight ahead: "+Mathf.CeilToInt((float)(alignAt-now));
                if(now>=alignAt){alignAt=-1;setup.Align();}
                return;
            }
            title.text=setup.Parameters.movementCondition+" / table "+(setup.Parameters.tableHeight*100).ToString("F0")+" cm\n"+setup.Message+"\n"+(setup.SurfaceFitWarning.Length>0?setup.SurfaceFitWarning:setup.Parameters.tableAlignmentConfirmed?"Table confirmed":"Table uncalibrated");
            if(click && hovered==0)alignAt=now+3;
            if(click && hovered==1)setup.AdjustTableHeight(-.02f);
            if(click && hovered==2)setup.AdjustTableHeight(.02f);
        }
        public static int HitButton(Vector3 origin,Vector3 direction,Vector3[] centres,Vector2[] sizes)
        {
            if(direction.z<=.001f)return -1;
            float distance=-origin.z/direction.z;if(distance<0)return -1;
            Vector3 point=origin+direction*distance;
            for(int i=0;i<centres.Length;i++)
                if(Mathf.Abs(point.x-centres[i].x)<=sizes[i].x*.5f && Mathf.Abs(point.y-centres[i].y)<=sizes[i].y*.5f)return i;
            return -1;
        }
        void OnApplicationPause(bool paused){if(paused){placed=false;alignAt=-1;leftPinch.Reset();rightPinch.Reset();}}
        void OnDestroy(){if(panel!=null)Destroy(panel.gameObject);}
    }

    // Release-to-arm hysteresis prevents held or newly reacquired pinches from clicking.
    public sealed class PinchLatch
    {
        bool armed;
        public void Reset(){armed=false;}
        public bool Update(TrackingHandFrame frame)
        {
            int thumb=XRHandJointID.ThumbTip.ToIndex(), index=XRHandJointID.IndexTip.ToIndex();
            if(!frame.JointValid[thumb] || !frame.JointValid[index]){Reset();return false;}
            return UpdateDistance(Vector3.Distance(frame.JointPoses[thumb].position,frame.JointPoses[index].position));
        }
        public bool UpdateDistance(float distance)
        {
            if(distance>.04f){armed=true;return false;}
            if(armed && distance<.025f){armed=false;return true;}
            return false;
        }
    }
}
