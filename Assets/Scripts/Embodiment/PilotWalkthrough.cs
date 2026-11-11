using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using QuestResearch.Hands;
using QuestResearch.Recording;
using QuestResearch.BodyCalibration;

namespace QuestResearch.Embodiment
{
    // Guided engineering demonstration, not a clinical protocol or research-data recorder.
    // Head-directed dwell interaction stays independent of manipulated hand poses.
    public sealed class PilotWalkthrough : MonoBehaviour
    {
        public enum Page { Welcome, Participant, RecordingError, SkinTone, Calibration, Table, Fit, Ready, Settings, Running, Aligning, HeightEntry, SurfaceCapture, SurfaceTools, Comfort, MirrorIntro, FutureTouch, Finished, HandSetup }
        [SerializeField] SeatedSetup setup;
        [SerializeField] HandTrackingSource hands;
        [SerializeField] BodyTrackingProbe body;
        [SerializeField] Transform head;
        [SerializeField] Material idleMaterial, hoverMaterial;
        [SerializeField] Font font;
        Transform panel,card; Mesh roundedMesh;
        RestingSurfaceCalibration surfaceCapture;
        BodyArmTargets fittedArms;
        MovementRecorder recorder;int participantNumber=1,participantPage;TextMesh recordingLabel;
        public string SkinToneName=>DemoSkinPalette.Names[skinTone];
        TextMesh stageLabel; Transform gazeDot; Material dotMaterial;
        readonly GuidedDemoActivity activity=new GuidedDemoActivity();
        int skinTone=2,lastPrompt=-1; bool mirrorActivity;
        Page resumePage=Page.Ready;
        readonly Material[] swatches=new Material[5];
        Material cardMaterial,uiIdle,uiHover,uiText;
        GazeProgressRing progressRing;
        TextMesh heading,instructions,status;
        readonly Transform[] buttons=new Transform[16];
        readonly TextMesh[] texts=new TextMesh[16];
        readonly Renderer[] renderers=new Renderer[16];
        readonly bool[] available=new bool[16];
        readonly Vector3[] centres=new Vector3[16];
        readonly Vector2[] sizes=new Vector2[16];
        bool inches;
        string heightEntry="";
        Page page=Page.Welcome;
        bool placed,wasFocused=true;
        string notice="",alignmentFailure="";
        double alignAt=-1,stableSince=-1,nextText,nextFloorRetry;
        StreamWriter events;
        readonly PilotDwell dwell=new PilotDwell();
        double outsideSince=-1;
        int lastHovered=-1;
        public Page CurrentPage=>page;
        public bool AlignmentUIHidden=>page==Page.Aligning && !panel.gameObject.activeSelf;
        public void Configure(SeatedSetup s,HandTrackingSource h,BodyTrackingProbe b,Transform camera,Material idle,Material hover,Font f)
        {setup=s;hands=h;body=b;head=camera;idleMaterial=idle;hoverMaterial=hover;font=f;}
        void Start()
        {
            recorder=gameObject.AddComponent<MovementRecorder>();
            recorder.Configure(hands,body,setup,GetComponent<MovementRemapper>(),GetComponent<SeatedAvatarDisplay>(),head,this);
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
            var overlay=Resources.Load<Shader>("PilotOverlay");
            uiIdle=new Material(overlay){color=new Color(.13f,.20f,.26f),renderQueue=4010};
            uiHover=new Material(overlay){color=new Color(.05f,.42f,.45f),renderQueue=4010};
            uiText=new Material(overlay){mainTexture=font.material.mainTexture,renderQueue=4020};
            uiText.SetFloat("_AlphaOnly",1);
            Font.textureRebuilt+=RefreshFontTexture;
            panel=new GameObject("Pilot walkthrough UI").transform;
            progressRing=new GameObject("Gaze selection progress").AddComponent<GazeProgressRing>();
            progressRing.transform.SetParent(panel,false);progressRing.Initialize(overlay);
            card=new GameObject("Guided setup card").transform;card.SetParent(panel,false);
            var backdrop=Button("Calibration card background",new Vector3(0,0,.025f),new Vector2(1.12f,.86f),card);
            cardMaterial=new Material(overlay){color=new Color(.035f,.055f,.075f),renderQueue=4000};
            backdrop.GetComponent<Renderer>().sharedMaterial=cardMaterial;
            heading=Label("",new Vector3(0,.29f,-.008f),.006f,card);
            instructions=Label("",new Vector3(0,.13f,-.008f),.0042f,card);
            status=Label("",new Vector3(0,-.365f,-.008f),.0031f,card);
            Layout(false);
            for(int i=0;i<buttons.Length;i++)
            {buttons[i]=Button("Pilot action "+i,centres[i],sizes[i],card);renderers[i]=buttons[i].GetComponent<Renderer>();texts[i]=Label("",centres[i]-Vector3.forward*.008f,.0038f,card);}
            stageLabel=Label("MIRROR THERAPY  /  GUIDED DEMO",new Vector3(0,.375f,-.008f),.0028f,card);
            recordingLabel=Label("",new Vector3(0,-.407f,-.008f),.0027f,card);
            stageLabel.color=new Color(.35f,.86f,.83f);
            for(int i=0;i<5;i++)swatches[i]=new Material(overlay){color=DemoSkinPalette.Colours[i],renderQueue=4010};
            gazeDot=Button("Head gaze cursor",Vector3.zero,new Vector2(.007f,.007f),panel);
            dotMaterial=new Material(overlay){color=Color.white,renderQueue=4040};
            gazeDot.GetComponent<Renderer>().sharedMaterial=dotMaterial;
            Show(Page.Welcome);panel.gameObject.SetActive(false);
        }
        Transform Button(string name,Vector3 position,Vector2 size,Transform parent)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            if(roundedMesh==null)roundedMesh=CreateRoundedMesh();
            go.GetComponent<MeshFilter>().sharedMesh=roundedMesh;
            go.transform.localPosition=position+Vector3.forward*.004f;go.transform.localScale=new Vector3(size.x,size.y,.008f);
            go.GetComponent<Renderer>().sharedMaterial=uiIdle;return go.transform;
        }
        static Mesh CreateRoundedMesh()
        {
            const int segments=8;var vertices=new Vector3[1+4*(segments+1)];
            var triangles=new int[(vertices.Length-1)*3];
            for(int corner=0;corner<4;corner++)
            {
                float start=corner*90;
                Vector2 centre=new Vector2(corner==0||corner==3?.43f:-.43f,corner<2?.43f:-.43f);
                for(int step=0;step<=segments;step++)
                {float a=(start+step*90f/segments)*Mathf.Deg2Rad;vertices[1+corner*(segments+1)+step]=centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.07f;}
            }
            for(int i=0;i<vertices.Length-1;i++){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%(vertices.Length-1)+1;}
            var mesh=new Mesh{name="Guided UI rounded panel"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        TextMesh Label(string text,Vector3 position,float size,Transform parent)
        {
            var t=new GameObject("Pilot label").AddComponent<TextMesh>();t.transform.SetParent(parent,false);t.transform.localPosition=position;
            t.text=text;t.font=font;t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
            t.GetComponent<Renderer>().sharedMaterial=uiText;return t;
        }
        public void Show(Page next)
        {
            page=next;placed=false;dwell.Block(lastHovered);nextText=0;
            Record("stage_entered");
            if(card==null)return;
            progressRing.SetProgress(Vector3.zero,0,false);
            card.gameObject.SetActive(next!=Page.Aligning);
            panel.gameObject.SetActive(next!=Page.Aligning);
            gazeDot.gameObject.SetActive(false);
            bool compact=next==Page.Running;
            card.GetChild(0).localScale=new Vector3(1.12f,compact?.43f:.86f,.008f);
            card.GetChild(0).localPosition=new Vector3(0,compact?.12f:0,.029f);
            heading.transform.localPosition=new Vector3(0,compact?.265f:.29f,-.008f);
            heading.characterSize=compact?.0044f:.006f;
            instructions.transform.localPosition=new Vector3(0,compact?.145f:.13f,-.008f);
            status.transform.localPosition=new Vector3(0,compact?-.055f:-.365f,-.008f);
            stageLabel.gameObject.SetActive(!compact);
            recordingLabel.transform.localPosition=new Vector3(0,compact?-.085f:-.407f,-.008f);
            Layout(next==Page.HeightEntry||next==Page.Participant);
            RefreshText();
        }
        void Layout(bool keypad)
        {
            for(int i=0;i<16;i++)
            {
                centres[i]=keypad?new Vector3((i%4-1.5f)*.24f,-.025f-(i/4)*.085f,0):new Vector3(i%2==0?-.255f:.255f,-.09f-(i/2)*.095f,0);
                sizes[i]=new Vector2(keypad?.22f:.48f,.08f);
                if(page==Page.Running)centres[i]=new Vector3(i%2==0?-.255f:.255f,.025f,0);
                if(page==Page.SkinTone&&i<5){centres[i]=new Vector3((i-2)*.20f,-.055f,0);sizes[i]=new Vector2(.18f,.11f);}
                if(page==Page.SkinTone&&i==5){centres[i]=new Vector3(0,-.24f,0);sizes[i]=new Vector2(.48f,.08f);}
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
            if(fittedArms!=null)fittedArms.ClearFit();
            if(setup!=null)setup.SurfacePreview=false;
            notice="";alignmentFailure="";Record("alignment_requested");
            if(setup!=null)setup.PrepareFloorOrigin();
            nextFloorRetry=Time.realtimeSinceStartupAsDouble+1;
            // Hide ALL controls for the whole alignment interval. Do not require looking at a countdown.
            alignAt=Time.realtimeSinceStartupAsDouble+4;
            Show(Page.Aligning);
        }
        void Place()
        {
            bool aligned=setup!=null&&setup.Aligned;
            // Page transitions must not recapture the wearer's current gaze: dwelling on a
            // right-hand button would move the next page right, accumulating apparent drift.
            // After alignment, all pages share the fixed participant midline and seated height.
            Vector3 anchor=aligned?setup.BodyFrame.position+Vector3.up*setup.SeatedHeadHeight:head.position;
            var forward=Vector3.ProjectOnPlane(aligned?setup.BodyFrame.forward:head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f)return;
            // Keep the entire panel above the hand workspace rather than in front of the lap.
            Vector3 position=anchor+forward*1.4f+Vector3.up*.18f;
            if(aligned)position.y=Mathf.Max(position.y,setup.BodyFrame.position.y+setup.Parameters.tableHeight+.68f);
            panel.SetPositionAndRotation(position,Quaternion.LookRotation(forward,Vector3.up));placed=true;
        }
        ParticipantHands HandProfile=>setup==null?ParticipantHands.Both:setup.Parameters.participantHands;
        bool SingleHand=>HandProfile!=ParticipantHands.Both;
        string SourceSide=>HandProfile==ParticipantHands.LeftOnly?"LEFT":"RIGHT";
        string TargetSide=>HandProfile==ParticipantHands.LeftOnly?"RIGHT":"LEFT";
        bool Required(int side)=>setup==null||setup.Parameters.UsesSide(side);
        bool HandReady(TrackingHandFrame h)=>h.Availability==HandAvailability.Available && Time.realtimeSinceStartupAsDouble-h.ReceivedAt<.15;
        bool HandsReady=>hands!=null && (!Required(0)||HandReady(hands.Left)) && (!Required(1)||HandReady(hands.Right));
        bool BodyReady=>body!=null && (!Required(0)||(body.Valid[1]&&body.Valid[2])) && (!Required(1)||(body.Valid[5]&&body.Valid[6])) && Time.realtimeSinceStartupAsDouble-body.ReceivedAt<.25;
        bool FitReady=>setup!=null && setup.Aligned&&setup.Parameters.tableAlignmentConfirmed&&(fittedArms==null||fittedArms.HasFit)&&HandsReady&&BodyReady&&stableSince>=0&&Time.realtimeSinceStartupAsDouble-stableSince>=1;
        void Update()
        {
            if(panel==null)return;
            if(surfaceCapture==null)
            {
                fittedArms=GetComponent<BodyArmTargets>();
                surfaceCapture=gameObject.AddComponent<RestingSurfaceCalibration>();
                surfaceCapture.Initialize(setup,hands,GetComponent<SeatedAvatarDisplay>(),fittedArms);
            }
            if(!Application.isFocused || !setup.HeadTracked || !OpenXRUtility.IsUserPresent)
            {
                surfaceCapture.Cancel();panel.gameObject.SetActive(false);placed=false;stableSince=-1;
                if(wasFocused){Record("tracking_or_focus_lost");recorder?.End("tracking_or_focus_lost");page=Page.Welcome;alignAt=-1;wasFocused=false;}
                return;
            }
            if(!wasFocused){wasFocused=true;if(fittedArms!=null)fittedArms.ClearFit();setup.BeginParticipantSetup();Show(Page.Welcome);}
            setup.ShowFitMarkers=page==Page.Fit;
            if(page==Page.SurfaceCapture&&!surfaceCapture.Active)
            {notice=surfaceCapture.Message;Record(surfaceCapture.Succeeded?"surface_estimated":"surface_estimate_failed");Show(Page.Table);}
            if(HandsReady&&BodyReady){if(stableSince<0)stableSince=Time.realtimeSinceStartupAsDouble;}else stableSince=-1;
            if(page==Page.Aligning)
            {
                if(Time.realtimeSinceStartupAsDouble>=nextFloorRetry && Time.realtimeSinceStartupAsDouble<alignAt-1)
                {setup.PrepareFloorOrigin();nextFloorRetry=Time.realtimeSinceStartupAsDouble+1;}
                if(Time.realtimeSinceStartupAsDouble>=alignAt)
                {
                    alignAt=-1;setup.Align();Record(setup.Aligned?"alignment_complete":"alignment_failed");
                    alignmentFailure=setup.Aligned?"":setup.Message;
                    placed=false;
                    if(setup.Aligned){surfaceCapture.Begin();Show(Page.SurfaceCapture);}
                    else Show(Page.Calibration);
                }
                return;
            }
            // A launch on an unworn headset must not leave the welcome card behind the wearer.
            if(page==Page.Welcome && placed && Vector3.Angle(head.forward,panel.position-head.position)>40)
            {
                if(outsideSince<0)outsideSince=Time.realtimeSinceStartupAsDouble;
                if(Time.realtimeSinceStartupAsDouble-outsideSince>1){placed=false;outsideSince=-1;}
            }
            else outsideSince=-1;
            if(!placed)Place();panel.gameObject.SetActive(true);
            if(RequiresAlignment(page) && !setup.Aligned){notice=setup.Message;Show(Page.Calibration);}
            if(recorder!=null&&recorder.Error.Length>0&&page!=Page.RecordingError&&page!=Page.Welcome&&page!=Page.Participant)Show(Page.RecordingError);
            UpdateActivity();
            if(Time.realtimeSinceStartupAsDouble>=nextText){nextText=Time.realtimeSinceStartupAsDouble+.2;RefreshText();}
            var origin=panel.InverseTransformPoint(head.position);var direction=panel.InverseTransformDirection(head.forward);
            bool intersects=direction.z>.001f;
            Vector3 point=intersects?origin+direction*(-origin.z/direction.z):Vector3.zero;
            gazeDot.gameObject.SetActive(intersects&&Mathf.Abs(point.x)<.56f&&Mathf.Abs(point.y)<.43f);
            gazeDot.localPosition=new Vector3(point.x,point.y,-.035f);
            int hovered=SeatedTestControls.HitButton(origin,direction,centres,sizes);
            lastHovered=hovered>=0&&available[hovered]?hovered:-1;
            bool selected=dwell.Update(lastHovered,Time.unscaledDeltaTime);
            progressRing.SetProgress(lastHovered>=0?centres[lastHovered]+new Vector3(sizes[lastHovered].x*.5f-.024f,0,-.02f):Vector3.zero,dwell.Progress,lastHovered>=0);
            for(int i=0;i<16;i++)
            {
                bool highlighted=i==hovered&&available[i];
                renderers[i].sharedMaterial=highlighted?uiHover:page==Page.SkinTone&&i<5?swatches[i]:uiIdle;
                if(page==Page.SkinTone&&i<5)texts[i].color=highlighted||i>=3?Color.white:new Color(.08f,.06f,.05f);
            }
            if(lastHovered>=0)status.text=dwell.NeedsGazeExit?"Move the dot off the buttons, then aim to select.":"Selecting: "+texts[lastHovered].text.Replace("\n"," ")+"  |  "+Mathf.CeilToInt((1-dwell.Progress)*2)+" seconds";
            if(selected&&hovered>=0&&available[hovered])Act(hovered);
        }
        void Set(int i,string label,bool enabled=true){texts[i].text=label;available[i]=enabled&&label.Length>0;texts[i].color=enabled?Color.white:Color.gray;buttons[i].gameObject.SetActive(label.Length>0);texts[i].gameObject.SetActive(label.Length>0);}
        void RefreshText()
        {
            if(heading==null)return;
            for(int i=0;i<16;i++)Set(i,"");
            float height=setup==null?.74f:setup.Parameters.tableHeight;
            recordingLabel.text=recorder==null?"":recorder.Status;
            recordingLabel.color=recorder!=null&&recorder.Error.Length>0?new Color(1,.55f,.35f):new Color(.4f,.85f,.8f);
            stageLabel.text=StageCaption(page);
            for(int n=0;n<16;n++)renderers[n].sharedMaterial=page==Page.SkinTone&&n<5?swatches[n]:uiIdle;
            status.text="Aim the centre dot at a button. Hold still for 2 seconds.";
            switch(page)
            {
                case Page.Welcome:
                    heading.text="Welcome";
                    instructions.text="A seated experience of virtual arms and hands.\nChoose your appearance, fit your table, then explore\nhow your movements appear on the opposite side.\nMove only as much as feels comfortable.";
                    Set(5,"Let's begin");break;
                case Page.Participant:
                    heading.text="Choose participant number";
                    instructions.text="Selected: P"+participantNumber.ToString("D2")+"\nEach run is saved as a separate session.\nChoose a number, then Confirm.";
                    for(int n=0;n<10;n++){int number=participantPage*10+n+1;Set(n,"P"+number.ToString("D2")+(number==participantNumber?"\nSelected":""));}
                    Set(12,"P01 - P10",participantPage>0);Set(13,"P11 - P20",participantPage==0);
                    Set(14,"Confirm",recorder==null||!recorder.Saving);Set(15,"Back");break;
                case Page.HandSetup:
                    heading.text="Choose physical hand availability";
                    instructions.text="Select the hand(s) you will use.\nOne-hand mode needs only the selected hand.\nPractice shows that hand on its own side.\nMirroring shows ONLY the opposite virtual hand.";
                    Set(0,"Both hands");Set(1,"Left hand only");Set(2,"Right hand only");break;
                case Page.RecordingError:
                    heading.text="Recording needs attention";
                    instructions.text="Recording has stopped. Earlier files are retained.\nPlease tell the researcher before continuing.\n"+"Check headset storage and the recording logs.\nStart a new session after resolving the problem.";
                    Set(5,"Return to start");break;
                case Page.SkinTone:
                    heading.text="Make these hands yours";
                    instructions.text="Choose a skin tone for both hands and arms.\nLook down to preview your selection.\nSelected: "+DemoSkinPalette.Names[skinTone];
                    for(int n=0;n<5;n++){Set(n,(skinTone==n?"Selected\n":"")+DemoSkinPalette.Names[n]);texts[n].color=n<3?new Color(.08f,.06f,.05f):Color.white;}
                    Set(5,"Continue");break;
                case Page.Calibration:
                    heading.text="Get comfortable";
                    instructions.text="Sit at your table with feet comfortably supported.\nSelect Begin alignment, then look straight ahead.\nThe menu disappears for 4 seconds.\nNext, rest both palms flat to estimate table height.";
                    if(alignmentFailure.Length>0)instructions.text=alignmentFailure+"\nPlease try alignment again.";
                    Set(4,"Back");Set(5,"Begin alignment");break;
                case Page.Table:
                    heading.text="Check your table";
                    instructions.text="Rest both palms on the real table.\nCheck that the virtual surface matches.\nAdjust or estimate again if needed.\nHeight "+(height*100).ToString("F1")+" cm  |  Edge "+((setup==null?.18f:setup.Parameters.tableNearEdge)*100).ToString("F0")+" cm";
                    if(notice.Length>0){instructions.text=notice;status.text="Check the surface, then hold gaze on Confirm surface.";}
                    Set(0,"Lower 1 cm");Set(1,"Raise 1 cm");Set(2,"Estimate from hands");Set(3,"More surface options");Set(4,"Enter exact height");Set(5,"Confirm surface",setup==null||setup.SurfacePreview);break;
                case Page.SurfaceCapture:
                    heading.text="Rest hands on the REAL table";
                    instructions.text=surfaceCapture==null?"Hold both palms flat and still.":surfaceCapture.Message;
                    Set(5,"Cancel");break;
                case Page.SurfaceTools:
                    heading.text="Surface position";instructions.text="Align the near edge with the real table.\nTable height remains unchanged.";
                    Set(0,"Edge closer 2 cm");Set(1,"Edge farther 2 cm");Set(5,"Back");break;
                case Page.HeightEntry:
                    heading.text="Enter measured table height";
                    instructions.text="Floor to tabletop - "+(inches?"inches":"centimetres")+"\n"+(heightEntry.Length==0?"_":heightEntry)+"\n"+"Allowed: "+(inches?"17.8 - 43.3 in":"45 - 110 cm")+(notice.Length>0?"\n"+notice:"");
                    string[] keys={"1","2","3","Back","4","5","6","Clear","7","8","9",inches?"in":"cm",".","0","Save","Cancel"};
                    for(int i=0;i<16;i++)Set(i,keys[i]);break;
                case Page.Fit:
                    heading.text="Check your arm fit";
                    instructions.text="Check both wrists, elbows and shoulders.\nRest, lift, and slowly turn each hand.\nUse only comfortable movement.\nShoulders/elbows are estimates, not a scan.\n"+(FitReady?"Tracking ready - confirm the visual fit.":"Fit pending: use Estimate from hands,\nthen check tracking and joint markers.");
                    Set(0,"Arms slimmer");Set(1,"Arms fuller");Set(2,"Table rest "+(setup==null||setup.Parameters.tableContactEnabled?"ON":"OFF"));Set(3,"Adjust table");Set(4,"Realign (4 sec)");Set(5,"Fit looks correct",FitReady);break;
                case Page.Ready:
                    heading.text="Meet your virtual hands";
                    instructions.text="First, each hand moves on its own side.\nFollow gentle prompts for about 20 seconds.\nYou can pause or skip an activity at any time.\nKeep movements small and comfortable.";
                    Set(4,"Adjust fit");Set(5,"Start familiarisation",setup==null||FitReady);break;
                case Page.Running:
                    heading.text=(mirrorActivity?"OPPOSITE-SIDE MOVEMENT":"GETTING FAMILIAR")+"  |  "+activity.Remaining+" s";
                    instructions.text=activity.Prompt;
                    Set(0,"Pause");Set(1,"Skip activity");break;
                case Page.Comfort:
                    heading.text="How does it feel?";
                    instructions.text="Take a moment to rest your hands.\nDo the arms and table feel comfortably aligned?\nContinue when you are ready, or adjust the fit.";
                    Set(2,"Adjust fit");Set(3,"Repeat practice");Set(4,"End demo");Set(5,"I feel ready");break;
                case Page.MirrorIntro:
                    heading.text="Explore the opposite side";
                    instructions.text="Your real left hand will move the virtual right.\nYour real right hand will move the virtual left.\nBoth virtual hands stay visible when tracked.\nFollow six gentle prompts, about 10 seconds each.";
                    Set(4,"Back");Set(5,"Begin mirror experience",setup==null||FitReady);break;
                case Page.FutureTouch:
                    heading.text="Coming next: visual touch";
                    instructions.text="A future clinician-guided stage will pair a stroke\non one real forearm with a visible stroke\non the opposite virtual forearm.\nHaptic-glove integration: to be implemented.\nNo touch sensing or stimulation is active here.";
                    Set(5,"Finish");break;
                case Page.Finished:
                    heading.text="Thank you for participating";
                    instructions.text="The demo is complete.\nPlease remove your headset.\n\nYour researcher will now conduct a brief\ninterview about your experience.";
                    Set(5,"New participant");break;
                case Page.Settings:
                    heading.text="Take your time";
                    instructions.text=(notice.Length>0?notice+"\n":"The experience is paused.\n")+"Rest your hands or adjust your position.\nRecalibration restarts familiarisation.\nResume when tracking and fit are ready.";
                    Set(0,"Recalibrate");Set(1,"Adjust table");Set(4,"End demo");Set(5,"Resume",setup==null||FitReady);break;

            }
            if(SingleHand)
            {
                if(page==Page.MirrorIntro)instructions.text="Move your REAL "+SourceSide+" hand.\nOnly the virtual "+TargetSide+" hand will appear.\nThe source-side virtual limb is hidden.\nFollow gentle prompts and rest whenever needed.";
                else if(page!=Page.Running&&page!=Page.SurfaceCapture&&page!=Page.HandSetup)
                    instructions.text=instructions.text.Replace("both palms","your "+SourceSide.ToLowerInvariant()+" palm").Replace("both hands and arms","the visible hand and arm").Replace("both wrists, elbows and shoulders","your tracked wrist and estimated arm").Replace("each hand","your selected hand").Replace("your hands","your selected hand");
                if(page==Page.SurfaceCapture)heading.text="Rest your "+SourceSide+" hand on the table";
                if(page==Page.Table)Set(2,"Estimate from hand");
            }
        }
        void Act(int i)
        {
            switch(page)
            {
                case Page.Welcome: if(i==5)Show(Page.Participant);break;
                case Page.Participant:
                    if(i<10)participantNumber=participantPage*10+i+1;
                    if(i==12)participantPage=0;if(i==13)participantPage=1;
                    if(i==14){if(recorder.Begin("P"+participantNumber.ToString("D2")))Show(Page.HandSetup);else Show(Page.RecordingError);}
                    if(i==15)Show(Page.Welcome);break;
                case Page.HandSetup:
                    if(i>=0&&i<=2){setup.SetParticipantHands((ParticipantHands)i);fittedArms?.ClearFit();stableSince=-1;Record("hand_profile_selected");Show(Page.SkinTone);}break;
                case Page.RecordingError:
                    if(i==5){recorder.End("recording_error");Show(Page.Welcome);}break;
                case Page.SkinTone:
                    if(i<5){skinTone=i;GetComponent<SeatedAvatarDisplay>()?.SetSkinTone(DemoSkinPalette.Colours[i]);Record("skin_tone_selected");}
                    if(i==5)Show(Page.Calibration);break;
                case Page.Calibration:
                    if(i==4)Show(Page.SkinTone);
                    // Alignment belongs to the current participant: preserve their selected hand profile.
                    if(i==5){body.BeginParticipant(0);BeginAlignment();}break;
                case Page.Table:
                    if(i==0||i==1){notice="";setup.AdjustTableHeight(i==0?-.01f:.01f);Record("surface_height_step");}
                    if(i==2){surfaceCapture.Begin();notice="";Show(Page.SurfaceCapture);}
                    if(i==3)Show(Page.SurfaceTools);
                    if(i==4){heightEntry="";notice="";Show(Page.HeightEntry);}
                    if(i==5){if(fittedArms!=null&&!fittedArms.HasFit)fittedArms.CaptureFit();setup.ConfirmTable();Record("table_confirmed");Show(Page.Fit);}break;
                case Page.SurfaceCapture:
                    if(i==5){surfaceCapture.Cancel();Show(Page.Table);}break;
                case Page.SurfaceTools:
                    if(i<2)setup.AdjustTableDistance(i==0?-.02f:.02f);if(i==5)Show(Page.Table);break;
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
                    if(i==4)Show(Page.Fit);if(i==5&&FitReady)StartActivity(false);break;
                case Page.Running:
                    if(i==0){resumePage=Page.Running;notice="";Record("activity_paused");Show(Page.Settings);}
                    if(i==1){Record("activity_skipped");activity.SkipPrompt();lastPrompt=-1;}break;
                case Page.Comfort:
                    if(i==2)Show(Page.Fit);if(i==3)StartActivity(false);
                    if(i==4)Finish();if(i==5)Show(Page.MirrorIntro);break;
                case Page.MirrorIntro:
                    if(i==4)Show(Page.Comfort);if(i==5&&FitReady)StartActivity(true);break;
                case Page.FutureTouch: if(i==5)Finish();break;
                case Page.Finished:
                    if(i==5){skinTone=2;GetComponent<SeatedAvatarDisplay>()?.SetSkinTone(DemoSkinPalette.Colours[2]);fittedArms?.ClearFit();setup.BeginParticipantSetup();Record("new_participant");Show(Page.Welcome);}break;
                case Page.Settings:
                    if(i==0){body.BeginParticipant(0);BeginAlignment();}
                    if(i==1){setup.SetMovement(MovementCondition.Normal,true);Show(Page.Table);}
                    if(i==4)Finish();
                    if(i==5&&FitReady){notice="";Record("activity_resumed");Show(resumePage);}break;

            }
            RefreshText();
        }
        static string StageCaption(Page p)
        {
            switch(p)
            {
                case Page.Welcome:case Page.Participant:case Page.HandSetup:return "01 / 07   WELCOME";
                case Page.SkinTone:return "02 / 07   YOUR APPEARANCE";
                case Page.Ready:case Page.Comfort:return "04 / 07   FAMILIARISATION";
                case Page.MirrorIntro:return "05 / 07   MIRROR EXPERIENCE";
                case Page.FutureTouch:return "06 / 07   FUTURE TOUCH";
                case Page.Finished:return "07 / 07   COMPLETE";
                case Page.Settings:return "EXPERIENCE PAUSED";
                default:return "03 / 07   PERSONAL FIT";
            }
        }
        static bool RequiresAlignment(Page p)=>p!=Page.Welcome&&p!=Page.Participant&&p!=Page.HandSetup&&p!=Page.RecordingError&&p!=Page.SkinTone&&p!=Page.Calibration&&p!=Page.Finished;
        void StartActivity(bool mirrored)
        {
            mirrorActivity=mirrored;activity.Begin(mirrored,HandProfile);lastPrompt=-1;
            var condition=!mirrored?MovementCondition.Normal:!SingleHand?MovementCondition.SwapBoth:HandProfile==ParticipantHands.LeftOnly?MovementCondition.LeftToRight:MovementCondition.RightToLeft;
            setup.SetMovement(condition,!mirrored||!SingleHand);
            Record("automatic_mapping_selected");Show(Page.Running);
        }
        void UpdateActivity()
        {
            if(page!=Page.Running)return;
            if(!FitReady){notice=SingleHand?"Tracking interrupted. Keep your "+SourceSide+" hand in view.":"Tracking interrupted. Keep both hands in view.";resumePage=Page.Running;Record("tracking_pause");Show(Page.Settings);return;}
            activity.Advance(Time.unscaledDeltaTime);
            if(activity.PromptIndex!=lastPrompt){lastPrompt=activity.PromptIndex;Record("activity_prompt");}
            if(activity.Complete){Record("activity_complete");Show(mirrorActivity?Page.FutureTouch:Page.Comfort);}
        }
        void Finish(){setup.SetMovement(MovementCondition.Normal,true);Record("demo_finished");Show(Page.Finished);recorder?.End("finished");}
        [Serializable] sealed class PilotEvent
        {
            public string kind,utc,version,affectedSide,movement,stage,skinTone,participant,session;public double monotonicSeconds;public float activitySeconds;public int promptIndex;
            public float tableHeight,tableNearEdge,standingHeight;public bool tableConfirmed,tableRest;
        }
        void Record(string kind)
        {
            recorder?.Event(kind);
            if(setup==null)return;
            var p=setup.Parameters;
            try{events?.WriteLine(JsonUtility.ToJson(new PilotEvent{kind=kind,participant=recorder==null?"":recorder.Participant,session=recorder==null?"":recorder.Session,stage=page.ToString(),skinTone=DemoSkinPalette.Names[skinTone],activitySeconds=activity.Elapsed,promptIndex=activity.PromptIndex,utc=DateTime.UtcNow.ToString("O"),version=Application.version,affectedSide="Not collected in guided demo",movement=p.movementCondition.ToString(),monotonicSeconds=Time.realtimeSinceStartupAsDouble,tableHeight=p.tableHeight,tableNearEdge=p.tableNearEdge,standingHeight=0,tableConfirmed=p.tableAlignmentConfirmed,tableRest=p.tableContactEnabled}));}
            catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException){notice="Demo event log failed";Debug.LogWarning("[Pilot] "+ex.Message);events=null;}
        }
        void OnApplicationPause(bool paused){if(paused){Record("app_paused");recorder?.End("app_paused");alignAt=-1;page=Page.Welcome;placed=false;wasFocused=false;if(panel!=null)panel.gameObject.SetActive(false);}}
        void RefreshFontTexture(Font updated){if(updated==font&&uiText!=null)uiText.mainTexture=font.material.mainTexture;}
        void OnDestroy(){if(roundedMesh!=null)Destroy(roundedMesh);foreach(var m in swatches)if(m!=null)Destroy(m);if(dotMaterial!=null)Destroy(dotMaterial);Font.textureRebuilt-=RefreshFontTexture;Record("app_stop");events?.Dispose();if(panel!=null)Destroy(panel.gameObject);if(cardMaterial!=null)Destroy(cardMaterial);if(uiIdle!=null)Destroy(uiIdle);if(uiHover!=null)Destroy(uiHover);if(uiText!=null)Destroy(uiText);}
    }
}
