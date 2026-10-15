using System;
using System.IO;
using UnityEngine;
using QuestResearch.Hands;
using QuestResearch.BodyCalibration;

namespace QuestResearch.Embodiment
{
    // Guided engineering demonstration, not a clinical protocol or research-data recorder.
    // All interaction uses physical hands; conditions only affect the avatar mapping.
    public sealed class PilotWalkthrough : MonoBehaviour
    {
        public enum Page { Welcome, Table, Fit, Ready, Settings, Running, Aligning, HeightEntry }
        [SerializeField] SeatedSetup setup;
        [SerializeField] HandTrackingSource hands;
        [SerializeField] BodyTrackingProbe body;
        [SerializeField] Transform head;
        [SerializeField] Material idleMaterial, hoverMaterial;
        [SerializeField] Font font;
        Transform panel,card,quickButton;
        Material cardMaterial;
        TextMesh heading,instructions,status,quickText;
        readonly Transform[] buttons=new Transform[16];
        readonly TextMesh[] texts=new TextMesh[16];
        readonly Renderer[] renderers=new Renderer[16];
        readonly bool[] available=new bool[16];
        readonly Vector3[] centres=new Vector3[16];
        readonly Vector2[] sizes=new Vector2[16];
        bool inches;
        string heightEntry="";
        readonly Vector3[] quickCentre={new Vector3(.30f,-.12f,0)};
        readonly Vector2[] quickSize={new Vector2(.19f,.065f)};
        readonly PinchLatch left=new PinchLatch(),right=new PinchLatch();
        Page page=Page.Welcome;
        bool placed,wasFocused=true,demoActive;
        float knownHeight;
        string affected="Not specified",notice="";
        double alignAt=-1,stableSince=-1,nextText;
        StreamWriter events;
        public Page CurrentPage=>page;
        public bool AlignmentUIHidden=>page==Page.Aligning && !panel.gameObject.activeSelf;
        public void Configure(SeatedSetup s,HandTrackingSource h,BodyTrackingProbe b,Transform camera,Material idle,Material hover,Font f)
        {setup=s;hands=h;body=b;head=camera;idleMaterial=idle;hoverMaterial=hover;font=f;}
        void Start()
        {
            BuildUI();
            if(setup!=null)setup.BeginParticipantSetup();
            try
            {
                events=new StreamWriter(Path.Combine(Application.persistentDataPath,"pilot-events-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".jsonl"),false);
                events.AutoFlush=true;Record("app_start");
            }
            catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException){notice="Demo event log unavailable";Debug.LogWarning("[Pilot] "+ex.Message);}
        }
        public void BuildUI()
        {
            panel=new GameObject("Pilot walkthrough UI").transform;
            card=new GameObject("Guided setup card").transform;card.SetParent(panel,false);
            var backdrop=Button("Calibration card background",new Vector3(0,-.04f,.025f),new Vector2(.76f,.76f),card);
            cardMaterial=new Material(idleMaterial){color=new Color(.055f,.075f,.09f)};
            backdrop.GetComponent<Renderer>().sharedMaterial=cardMaterial;
            heading=Label("",new Vector3(0,.245f,-.008f),.0044f,card);
            instructions=Label("",new Vector3(0,.095f,-.008f),.0034f,card);
            status=Label("",new Vector3(0,-.37f,-.008f),.0028f,card);
            Layout(false);
            for(int i=0;i<buttons.Length;i++)
            {buttons[i]=Button("Pilot action "+i,centres[i],sizes[i],card);renderers[i]=buttons[i].GetComponent<Renderer>();texts[i]=Label("",centres[i]-Vector3.forward*.008f,.0032f,card);}
            quickButton=Button("Pilot settings",new Vector3(.30f,-.12f,0),new Vector2(.19f,.065f),panel);
            quickText=Label("Settings",new Vector3(.30f,-.12f,-.008f),.0034f,quickButton);
            quickText.transform.localPosition=new Vector3(0,0,-.008f);
            // Buttons have metre-sized transforms; text is a sibling so it is not stretched by cube scale.
            quickText.transform.SetParent(panel,false);quickText.transform.localPosition=new Vector3(.30f,-.12f,-.008f);
            Show(Page.Welcome);panel.gameObject.SetActive(false);
        }
        Transform Button(string name,Vector3 position,Vector2 size,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position+Vector3.forward*.004f;go.transform.localScale=new Vector3(size.x,size.y,.008f);
            Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=idleMaterial;return go.transform;
        }
        TextMesh Label(string text,Vector3 position,float size,Transform parent)
        {
            var t=new GameObject("Pilot label").AddComponent<TextMesh>();t.transform.SetParent(parent,false);t.transform.localPosition=position;
            t.text=text;t.font=font;t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
            t.GetComponent<Renderer>().sharedMaterial=font.material;return t;
        }
        public void Show(Page next)
        {
            page=next;left.Reset();right.Reset();nextText=0;
            if(card==null)return;
            card.gameObject.SetActive(next!=Page.Running && next!=Page.Aligning);
            quickButton.gameObject.SetActive(next==Page.Running);quickText.gameObject.SetActive(next==Page.Running);
            panel.gameObject.SetActive(next!=Page.Aligning);
            Layout(next==Page.HeightEntry);
            RefreshText();
        }
        void Layout(bool keypad)
        {
            for(int i=0;i<16;i++)
            {
                centres[i]=keypad?new Vector3((i%4-1.5f)*.155f,-.065f-(i/4)*.085f,0):new Vector3(i%2==0?-.155f:.155f,-.12f-(i/2)*.085f,0);
                sizes[i]=new Vector2(keypad?.14f:.29f,.065f);
                if(buttons[i]!=null){buttons[i].localPosition=centres[i]+Vector3.forward*.004f;buttons[i].localScale=new Vector3(sizes[i].x,sizes[i].y,.008f);texts[i].transform.localPosition=centres[i]-Vector3.forward*.008f;}
            }
        }
        public static bool TryTableHeight(string text,bool inInches,out float metres)
        {
            metres=0;
            if(!float.TryParse(text,System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out float value))return false;
            metres=value*(inInches?.0254f:.01f);
            return float.IsFinite(metres)&&metres>=.45f&&metres<=1.1f;
        }
        public void BeginAlignment()
        {
            demoActive=false;Record("alignment_requested");
            // Hide ALL controls for the whole alignment interval. Do not require looking at a countdown.
            alignAt=Time.realtimeSinceStartupAsDouble+4;
            Show(Page.Aligning);
        }
        void Place()
        {
            var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f)return;
            panel.SetPositionAndRotation(head.position+forward*.9f,Quaternion.LookRotation(forward,Vector3.up));placed=true;
        }
        bool HandsReady=>hands!=null && hands.Left.Availability==HandAvailability.Available && hands.Right.Availability==HandAvailability.Available;
        bool BodyReady=>body!=null && body.Valid[1]&&body.Valid[2]&&body.Valid[5]&&body.Valid[6] && Time.realtimeSinceStartupAsDouble-body.ReceivedAt<.25;
        bool FitReady=>setup!=null && setup.Aligned&&setup.Parameters.tableAlignmentConfirmed&&HandsReady&&BodyReady&&stableSince>=0&&Time.realtimeSinceStartupAsDouble-stableSince>=1;
        void Update()
        {
            if(panel==null)return;
            if(!Application.isFocused || !setup.HeadTracked)
            {
                panel.gameObject.SetActive(false);placed=false;stableSince=-1;left.Reset();right.Reset();
                if(wasFocused){Record("tracking_or_focus_lost");demoActive=false;page=Page.Welcome;alignAt=-1;wasFocused=false;}
                return;
            }
            if(!wasFocused){wasFocused=true;setup.BeginParticipantSetup();Show(Page.Welcome);}
            if(HandsReady&&BodyReady){if(stableSince<0)stableSince=Time.realtimeSinceStartupAsDouble;}else stableSince=-1;
            if(page==Page.Aligning)
            {
                if(Time.realtimeSinceStartupAsDouble>=alignAt)
                {
                    alignAt=-1;setup.Align();Record(setup.Aligned?"alignment_complete":"alignment_failed");
                    placed=false;Show(setup.Aligned?Page.Table:Page.Welcome);
                }
                return;
            }
            if(!placed)Place();panel.gameObject.SetActive(true);
            if(page!=Page.Welcome && !setup.Aligned){demoActive=false;Show(Page.Welcome);}
            if(Time.realtimeSinceStartupAsDouble>=nextText){nextText=Time.realtimeSinceStartupAsDouble+.2;RefreshText();}
            bool click=left.Update(hands.Left)|right.Update(hands.Right);
            var origin=panel.InverseTransformPoint(head.position);var direction=panel.InverseTransformDirection(head.forward);
            if(page==Page.Running)
            {
                int hit=SeatedTestControls.HitButton(origin,direction,quickCentre,quickSize);
                quickButton.GetComponent<Renderer>().sharedMaterial=hit==0?hoverMaterial:idleMaterial;
                if(click&&hit==0){Record("demo_pause");Show(Page.Settings);}return;
            }
            int hovered=SeatedTestControls.HitButton(origin,direction,centres,sizes);
            for(int i=0;i<16;i++)renderers[i].sharedMaterial=i==hovered&&available[i]?hoverMaterial:idleMaterial;
            if(click&&hovered>=0&&available[hovered])Act(hovered);
        }
        void Set(int i,string label,bool enabled=true){texts[i].text=label;available[i]=enabled;texts[i].color=enabled?Color.white:Color.gray;buttons[i].gameObject.SetActive(label.Length>0);texts[i].gameObject.SetActive(label.Length>0);}
        void RefreshText()
        {
            if(heading==null)return;
            for(int i=0;i<16;i++)Set(i,"");
            float height=setup==null?.74f:setup.Parameters.tableHeight;
            string mode=setup==null?"Normal":setup.Parameters.movementCondition.ToString();
            status.text="Look at a button, then pinch thumb + index.\n"+notice;
            switch(page)
            {
                case Page.Welcome:
                    heading.text="1 / 4   Welcome";
                    instructions.text="Sit comfortably at a real table.\nKeep only the wearer's hands in view.\nBegin hides this menu for 4 seconds:\nlook straight ahead, with shoulders relaxed.\nHeight: "+(knownHeight>0?(knownHeight*100).ToString("F0")+" cm (standing height)":"automatic estimate - verify fit");
                    Set(0,"Height -5 cm");Set(1,"Height +5 cm");Set(2,"Use automatic");Set(3,"Begin alignment");break;
                case Page.Table:
                    heading.text="2 / 4   Fit the table";
                    instructions.text="Helper: measure floor to tabletop.\nSet height, then check the near edge.\nRest hands gently on the real table.\nThis is manual fitting, not surface sensing.\nHeight "+(height*100).ToString("F1")+" cm  |  Edge "+((setup==null?.18f:setup.Parameters.tableNearEdge)*100).ToString("F0")+" cm";
                    Set(0,"Enter table height");Set(1,inches?"Units: inches":"Units: cm");Set(2,"Edge closer 2 cm");Set(3,"Edge farther 2 cm");Set(4,"Realign (4 sec)");Set(5,"Confirm table");break;
                case Page.HeightEntry:
                    heading.text="Enter measured table height";
                    instructions.text="Floor to tabletop - "+(inches?"inches":"centimetres")+"\n"+(heightEntry.Length==0?"_":heightEntry)+"\n"+"Allowed: "+(inches?"17.8 - 43.3 in":"45 - 110 cm");
                    string[] keys={"1","2","3","Back","4","5","6","Clear","7","8","9",inches?"in":"cm",".","0","Save","Cancel"};
                    for(int i=0;i<16;i++)Set(i,keys[i]);break;
                case Page.Fit:
                    heading.text="3 / 4   Check embodiment";
                    instructions.text="Check both wrists, elbows and shoulders.\nRest, lift, and slowly turn each hand.\nUse only comfortable movement.\nShoulders/elbows are estimates, not a scan.\n"+(FitReady?"Tracking ready - confirm the visual fit.":"Waiting for both hands and body tracking...");
                    Set(0,"Arms slimmer");Set(1,"Arms fuller");Set(2,"Table rest "+(setup==null||setup.Parameters.tableContactEnabled?"ON":"OFF"));Set(3,"Adjust table");Set(4,"Realign (4 sec)");Set(5,"Fit looks correct",FitReady);break;
                case Page.Ready:
                    heading.text="4 / 4   Movement demo";
                    instructions.text="Choose a mapping, then start.\nMove one hand slowly, then the other.\nCompare physical and virtual sides.\nMode: "+mode+"\nAffected side: "+affected+" (demo label)";
                    Set(0,"Normal");Set(1,"Left to right");Set(2,"Right to left");Set(3,"Swap both sides");Set(4,"Settings");Set(5,"Start demo",FitReady);break;
                case Page.Settings:
                    heading.text="Pilot settings";
                    instructions.text="Mode: "+mode+"\nAffected side: "+affected+"\nRecalibration hides the menu for 4 seconds.\nClinician hand tracking is not available.\nDo not present this as an automated touch trial.";
                    Set(0,"New wearer");Set(1,"Realign (4 sec)");Set(2,"Fit table / arms");Set(3,"Affected: "+affected);Set(4,demoActive?"End demo":"Choose mapping");Set(5,demoActive?"Resume":"Close",setup!=null&&setup.Aligned);break;
            }
        }
        void Act(int i)
        {
            switch(page)
            {
                case Page.Welcome:
                    if(i<2)knownHeight=Mathf.Clamp((knownHeight>0?knownHeight:1.70f)+(i==0?-.05f:.05f),.5f,2.5f);
                    if(i==2)knownHeight=0;
                    if(i==3){setup.BeginParticipantSetup();body.BeginParticipant(knownHeight);BeginAlignment();}break;
                case Page.Table:
                    if(i==0){heightEntry="";Show(Page.HeightEntry);}
                    if(i==1)inches=!inches;
                    if(i==2||i==3)setup.AdjustTableDistance(i==2?-.02f:.02f);
                    if(i==4)BeginAlignment();
                    if(i==5){setup.ConfirmTable();Record("table_confirmed");Show(Page.Fit);}break;
                case Page.HeightEntry:
                    if(i==3 && heightEntry.Length>0)heightEntry=heightEntry.Substring(0,heightEntry.Length-1);
                    else if(i==7)heightEntry="";
                    else if(i==11){inches=!inches;heightEntry="";}
                    else if(i==15)Show(Page.Table);
                    else if(i==14)
                    {
                        if(TryTableHeight(heightEntry,inches,out float metres)){setup.AdjustTableHeight(metres-setup.Parameters.tableHeight);notice="Height entered - confirm the physical fit.";Record("table_height_entered");Show(Page.Table);}
                        else notice="Enter a valid height in the shown range.";
                    }
                    else if(heightEntry.Length<6)
                    {
                        string[] digits={"1","2","3","","4","5","6","","7","8","9","",".","0","",""};
                        if(digits[i]!="."||!heightEntry.Contains("."))heightEntry+=digits[i];
                    }
                    break;
                case Page.Fit:
                    if(i<2)setup.AdjustArmFullness(i==0?-.05f:.05f);
                    if(i==2)setup.SetTableContact(!setup.Parameters.tableContactEnabled);
                    if(i==3)Show(Page.Table);if(i==4)BeginAlignment();
                    if(i==5&&FitReady){Record("fit_confirmed");Show(Page.Ready);}break;
                case Page.Ready:
                    if(i<4){setup.SetMovement((MovementCondition)i,true);Record("mapping_selected");}
                    if(i==4)Show(Page.Settings);
                    if(i==5&&FitReady){demoActive=true;Record("demo_start");Show(Page.Running);}break;
                case Page.Settings:
                    if(i==0){demoActive=false;knownHeight=0;affected="Not specified";setup.BeginParticipantSetup();Record("new_wearer");Show(Page.Welcome);}
                    if(i==1)BeginAlignment();if(i==2){demoActive=false;Show(Page.Table);}
                    if(i==3)affected=affected=="Not specified"?"Left":affected=="Left"?"Right":"Not specified";
                    if(i==4){demoActive=false;Record("demo_end");Show(Page.Ready);}
                    if(i==5)Show(demoActive?Page.Running:Page.Ready);break;
            }
            RefreshText();
        }
        [Serializable] sealed class PilotEvent
        {
            public string kind,utc,version,affectedSide,movement;public double monotonicSeconds;
            public float tableHeight,tableNearEdge,standingHeight;public bool tableConfirmed,tableRest;
        }
        void Record(string kind)
        {
            if(setup==null)return;
            var p=setup.Parameters;
            try{events?.WriteLine(JsonUtility.ToJson(new PilotEvent{kind=kind,utc=DateTime.UtcNow.ToString("O"),version=Application.version,affectedSide=affected,movement=p.movementCondition.ToString(),monotonicSeconds=Time.realtimeSinceStartupAsDouble,tableHeight=p.tableHeight,tableNearEdge=p.tableNearEdge,standingHeight=knownHeight,tableConfirmed=p.tableAlignmentConfirmed,tableRest=p.tableContactEnabled}));}
            catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException){notice="Demo event log failed";Debug.LogWarning("[Pilot] "+ex.Message);events=null;}
        }
        void OnApplicationPause(bool paused){if(paused){Record("app_paused");demoActive=false;alignAt=-1;page=Page.Welcome;placed=false;wasFocused=false;if(panel!=null)panel.gameObject.SetActive(false);}}
        void OnDestroy(){Record("app_stop");events?.Dispose();if(panel!=null)Destroy(panel.gameObject);if(cardMaterial!=null)Destroy(cardMaterial);}
    }
}
