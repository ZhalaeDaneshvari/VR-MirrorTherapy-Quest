using UnityEngine;
using UnityEngine.InputSystem;

namespace ClassEmbodimentDemo
{
    // Presentation-only orchestrator. No XR input, calibration, physical touch detection or research logging.
    public sealed class ClassEmbodimentDemo : MonoBehaviour
    {
        MovementCondition condition=MovementCondition.LeftToRight;
        DemoMannequin source, displayed;
        Camera sourceCamera, displayCamera;
        Transform sourceTouch, displayedTouch;
        bool animate=true, touch, crossTouch=true, firstPerson;
        bool oppositeOnly=true;
        int touchSide;
        float lookPitch=38f, lookYaw;
        float time, touchTime, manualHeight=.98f;
        Pose left, right;
        readonly System.Collections.Generic.List<Material> materials=new System.Collections.Generic.List<Material>();

        void Start()
        {
            var body=Material("Clothing",new Color(.19f,.34f,.44f));
            var skin=Material("Virtual skin",new Color(.85f,.64f,.43f));
            var sourceShirt=Material("Source shirt",new Color(.12f,.43f,.57f));
            var eyeWhite=Material("Eye whites",new Color(.88f,.89f,.85f));
            var chair=Material("Chair",new Color(.13f,.17f,.22f));
            source=new DemoMannequin("SIMULATED physical body",new Vector3(-2,0,0),sourceShirt,skin,chair,eyeWhite);
            displayed=new DemoMannequin("DISPLAYED virtual body",new Vector3(2,0,0),body,skin,chair,eyeWhite);
            SetLayer(source.Root,8); SetLayer(displayed.Root,9);
            sourceCamera=MakeCamera("Source observation camera",new Vector3(-2,1.85f,3.1f),new Vector3(-2,1.06f,.1f));
            displayCamera=MakeCamera("Avatar observation camera",new Vector3(2,1.85f,3.1f),new Vector3(2,1.06f,.1f));
            sourceCamera.cullingMask=1<<8; displayCamera.cullingMask=1<<9;
            var light=new GameObject("Demo directional light").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.15f; light.transform.rotation=Quaternion.Euler(40,155,0);
            RenderSettings.ambientLight=new Color(.50f,.53f,.60f);
            var floorMat=Material("Floor",new Color(.21f,.25f,.30f));
            var lineMat=Material("Midline",new Color(.92f,.73f,.25f));
            foreach(float x in new[]{-2f,2f})
            {
                Primitive("Floor",PrimitiveType.Cube,new Vector3(x,-.035f,0),new Vector3(3,.06f,4),floorMat).gameObject.layer=x<0?8:9;
                Primitive("Body midline reference",PrimitiveType.Cube,new Vector3(x,.001f,.5f),new Vector3(.012f,.006f,2.3f),lineMat).gameObject.layer=x<0?8:9;
            }
            var touchMat=Material("Visual stimulus",new Color(1,.22f,.38f));
            sourceTouch=Primitive("SIMULATED physical touch instruction",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.065f,touchMat);
            displayedTouch=Primitive("Virtual touch indicator",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.065f,touchMat);
        }

        // Isolate the two teaching views using otherwise unused demo-only layers.
        static void SetLayer(Transform root,int layer){root.gameObject.layer=layer;foreach(Transform child in root)SetLayer(child,layer);}

        Material Material(string name,Color color)
        {
            var m=new Material(Shader.Find("Standard")){name=name,color=color}; m.SetFloat("_Glossiness",.18f); materials.Add(m);return m;
        }
        Transform Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(transform,false);
            go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            Destroy(go.GetComponent<Collider>());return go.transform;
        }
        Camera MakeCamera(string name,Vector3 position,Vector3 target)
        {
            var cam=new GameObject(name).AddComponent<Camera>();cam.transform.SetParent(transform,false);
            cam.transform.position=position;cam.transform.LookAt(target);cam.fieldOfView=42;cam.nearClipPlane=.03f;cam.farClipPlane=20;
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.065f,.085f,.115f);return cam;
        }

        void Update()
        {
            if(source==null)return;
            float dt=Time.deltaTime;
            if(animate)time+=dt;
            if(!animate && Keyboard.current!=null)
            {
                if(Keyboard.current.upArrowKey.isPressed)manualHeight+=dt*.25f;
                if(Keyboard.current.downArrowKey.isPressed)manualHeight-=dt*.25f;
                manualHeight=Mathf.Clamp(manualHeight,.91f,1.61f);
            }
            float wave=(Mathf.Sin(time*1.1f)+1)*.5f;
            float y=animate?Mathf.Lerp(.94f,1.58f,wave):manualHeight;
            var resting=DemoMovementMapping.RestingPose(true);
            // Asymmetric inputs make source selection visible. Congruent mode animates left only.
            bool driveRight=condition==MovementCondition.RightToLeft;
            left=driveRight?new Pose(DemoMovementMapping.Reflect(resting.position),Quaternion.identity):new Pose(new Vector3(-.36f,y,.31f),Quaternion.Euler(-15*wave,0,-12*wave));
            right=driveRight?new Pose(new Vector3(.36f,y,.31f),Quaternion.Euler(-15*wave,0,12*wave)):resting;
            DemoMovementMapping.Map(condition,left,right,out var outputLeft,out var outputRight,oppositeOnly);
            source.SetPose(0,left);source.SetPose(1,right);displayed.SetPose(0,outputLeft);displayed.SetPose(1,outputRight);
            if(touch)touchTime+=dt;
            float fraction=.2f+.6f*Mathf.PingPong(touchTime*.35f,1);
            sourceTouch.gameObject.SetActive(touch);displayedTouch.gameObject.SetActive(touch);
            sourceTouch.position=source.ForearmPoint(touchSide,fraction);
            displayedTouch.position=displayed.ForearmPoint(crossTouch?1-touchSide:touchSide,fraction);
            sourceTouch.gameObject.layer=8; displayedTouch.gameObject.layer=9;
            float uiScale=Screen.width/1280f;
            float top=104f*uiScale/Screen.height, bottom=220f*uiScale/Screen.height;
            sourceCamera.enabled=!firstPerson;
            sourceCamera.rect=new Rect(0,bottom,.5f,Mathf.Max(.1f,1-top-bottom));
            displayCamera.rect=new Rect(firstPerson?0:.5f,bottom,firstPerson?1:.5f,Mathf.Max(.1f,1-top-bottom));
            displayed.Head.gameObject.SetActive(!firstPerson);
            if(firstPerson)
            {
                displayCamera.transform.position=new Vector3(2,1.68f,.07f);
                if(Mouse.current!=null && Mouse.current.rightButton.isPressed)
                {
                    Vector2 delta=Mouse.current.delta.ReadValue();
                    lookYaw=Mathf.Clamp(lookYaw+delta.x*.10f,-65,65);
                    lookPitch=Mathf.Clamp(lookPitch-delta.y*.10f,-15,75);
                }
                displayCamera.transform.rotation=Quaternion.Euler(lookPitch,lookYaw,0);displayCamera.fieldOfView=85;
            }
            else
            {
                displayCamera.transform.position=new Vector3(2,1.85f,3.1f);
                displayCamera.transform.LookAt(new Vector3(2,1.06f,.1f));displayCamera.fieldOfView=42;
            }
        }

        void OnGUI()
        {
            if(source==null)return;
            float scale=Screen.width/1280f, w=1280f, h=Screen.height/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUI.skin.label.fontSize=15; GUI.skin.button.fontSize=15; GUI.skin.toggle.fontSize=15;
            var title=new GUIStyle(GUI.skin.label){fontSize=21,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            var label=new GUIStyle(GUI.skin.label){fontSize=14,alignment=TextAnchor.MiddleCenter,wordWrap=true};
            GUI.Box(new Rect(0,0,w,104),GUIContent.none);
            GUI.Label(new Rect(0,5,w,27),"CROSS-LIMB EMBODIMENT  /  CLASS DEMONSTRATION",title);
            GUI.Label(new Rect(0,33,w,23),"SIMULATED INPUT — No Quest tracking, physical touch sensing, or validated embodiment",label);
            GUI.Label(new Rect(0,56,firstPerson?w:w*.5f,24),firstPerson?"FIRST-PERSON VIRTUAL BODY  •  Hold right mouse button to look around":"SIMULATED PHYSICAL BODY  •  blue shirt",label);
            if(!firstPerson) GUI.Label(new Rect(w*.5f,56,w*.5f,24),"DISPLAYED VIRTUAL BODY  •  dark shirt",label);
            GUI.Label(new Rect(0,80,w,22),firstPerson?"Your LEFT is screen-left; your RIGHT is screen-right. Desktop preview, not stereoscopic VR.":"FRONT VIEW: participant RIGHT = screen-left  |  participant LEFT = screen-right",label);
            GUILayout.BeginArea(new Rect(8,h-214,w-16,210),GUI.skin.box);
            GUILayout.BeginHorizontal();GUILayout.Label("Movement",GUILayout.Width(80));
            if(GUILayout.Button("Normal / congruent"))condition=MovementCondition.Congruent;
            if(GUILayout.Button("LEFT → RIGHT"))condition=MovementCondition.LeftToRight;
            if(GUILayout.Button("RIGHT → LEFT"))condition=MovementCondition.RightToLeft;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            oppositeOnly=GUILayout.Toggle(oppositeOnly,"Opposite arm ONLY (off = both arms)",GUILayout.Width(350));
            GUILayout.Label(condition==MovementCondition.Congruent?"Normal: physical left → virtual left; physical right → virtual right.":(oppositeOnly?"Only the opposite virtual arm moves; the unused arm stays at rest.":"One source drives BOTH virtual arms."));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            animate=GUILayout.Toggle(animate,"Animate simulated source",GUILayout.Width(205));
            GUILayout.Label("Manual height",GUILayout.Width(90));manualHeight=GUILayout.HorizontalSlider(manualHeight,.91f,1.61f,GUILayout.MinWidth(80));
            firstPerson=GUILayout.Toggle(firstPerson,"First-person avatar view",GUILayout.Width(205));
            if(GUILayout.Button("Recenter view",GUILayout.Width(125))){lookPitch=38;lookYaw=0;}
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(touch?"Stop visual touch":"Start visual touch",GUILayout.Width(155))){touch=!touch;touchTime=0;}
            if(GUILayout.Button("Physical side: "+(touchSide==0?"LEFT":"RIGHT"),GUILayout.Width(165))){touchSide=1-touchSide;touchTime=0;}
            crossTouch=GUILayout.Toggle(crossTouch,"Cross-limb touch (off = congruent)");
            GUILayout.EndHorizontal();
            GUILayout.Label("Touch: simulated "+(touchSide==0?"LEFT":"RIGHT")+" forearm → virtual "+((crossTouch?1-touchSide:touchSide)==0?"LEFT":"RIGHT")+" forearm. Pink marker = visual stimulus; fixed repeating forearm stroke.");
            GUILayout.Label("Uncheck animation to use the height slider or ↑ / ↓. All LEFT/RIGHT labels are anatomical (the participant’s sides). No scientific data recorded.");
            GUILayout.EndArea();
        }
        void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);}
    }
}
