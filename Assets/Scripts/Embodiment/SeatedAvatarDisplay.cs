using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;

namespace QuestResearch.Embodiment
{
    // First-person upper body. Hands use XR tracking; arms use mapped body estimates when configured, otherwise legacy IK.
    public sealed class SeatedAvatarDisplay : MonoBehaviour
    {
        [SerializeField] MovementRemapper mapping;
        [SerializeField] BodyArmTargets bodyTargets;
        public void ConfigureBodyTargets(BodyArmTargets targets){bodyTargets=targets;}
        int lastRenderedFrame=-1;
        public double RenderMilliseconds {get;private set;}
        // XR Hands 1.9 updates at default BeforeRender order. Consume the latest mapped poses once afterward.
        [BeforeRenderOrder(10000)]
        void RenderBeforeDisplay()
        {
            if(lastRenderedFrame==Time.frameCount)return;
            lastRenderedFrame=Time.frameCount;
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            Refresh();
            RenderMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        [SerializeField] SeatedSetup setup;
        [SerializeField] Transform trackingSpace;
        [SerializeField] Material skin,shirt,tableMaterial;
        [SerializeField] RemappedHandMesh leftHandPrefab, rightHandPrefab;
        readonly RemappedHandMesh[] handMeshes = new RemappedHandMesh[2];
        readonly ContinuousArmSurface[] armSurfaces = new ContinuousArmSurface[2];
        public bool HasBodyFit=>bodyTargets!=null&&bodyTargets.HasFit;
        public bool TryTouchPoint(int side,float progress,out Vector3 point)
        {
            point=Vector3.zero;
            return ready&&setup.Aligned&&setup.HeadTracked&&side>=0&&side<2&&armSurfaces[side]!=null
                &&armSurfaces[side].TryTopForearm(progress,out point);
        }
        public void ConfigureMeshes(RemappedHandMesh left, RemappedHandMesh right)
        { leftHandPrefab = left; rightHandPrefab = right; }
        Transform body,torso,tableTop;
        readonly Transform[] tableLegs=new Transform[4],upper=new Transform[2],forearm=new Transform[2],handRoots=new Transform[2],palms=new Transform[2];
        Transform[][] joints,bones;
        int[] parents;
        bool ready;
        readonly Transform[] fitMarkers=new Transform[6];
        public bool TryRestingHeight(QuestResearch.Hands.TrackingHandFrame frame,int side,out float height)
        {
            height=0;if(!ready||handMeshes[side]==null||frame.Availability!=QuestResearch.Hands.HandAvailability.Available)return false;
            var pose=contactHands[side];pose.CopyFrom(frame);
            handMeshes[side].Apply(pose,trackingSpace,setup.HeadTracked);
            handMeshes[side].LowestOverTable(setup.BodyFrame,setup.Parameters);
            height=handMeshes[side].LowestWholeHand;
            return float.IsFinite(height);
        }
        bool[] previousReach={true,true};
        readonly float[] surfaceClearance={float.PositiveInfinity,float.PositiveInfinity};
        double nextFitReport;
        readonly DisplayedHandPose[] contactHands={new DisplayedHandPose(),new DisplayedHandPose()};
        // Metres added AFTER experimental mapping. Raw tracking and mapped targets remain unchanged.
        public readonly float[] HandTableLift=new float[2];
        public readonly float[] ArmSurfaceLift=new float[2];
        public readonly float[] ElbowSupportLift=new float[2],PalmFlattenDegrees=new float[2];
        readonly TableRestState[] rests={new TableRestState(),new TableRestState()};
        readonly PhysicalHand[] previousSource=new PhysicalHand[2];
        readonly bool[] previousReflection=new bool[2];
        readonly bool[] belowTable=new bool[2];
        float previousTableHeight=-1;
        public void Configure(MovementRemapper targets,SeatedSetup alignment,Transform space,Material skinMaterial,Material shirtMaterial,Material table)
        {mapping=targets;setup=alignment;trackingSpace=space;skin=skinMaterial;shirt=shirtMaterial;tableMaterial=table;}
        void Start()
        {
            body=new GameObject("Estimated upper body and virtual table").transform;body.SetParent(setup.BodyFrame,false);
            torso=Part("Torso (estimated)",PrimitiveType.Capsule,body,shirt);
            tableTop=Part("Table top - align to physical surface",PrimitiveType.Cube,body,tableMaterial);
            for(int i=0;i<4;i++)tableLegs[i]=Part("Table leg "+i,PrimitiveType.Cube,body,tableMaterial);
            int count=XRHandJointID.EndMarker.ToIndex();parents=new int[count];for(int i=0;i<count;i++)parents[i]=-1;
            int wrist=XRHandJointID.Wrist.ToIndex();
            parents[XRHandJointID.Palm.ToIndex()]=wrist;
            Chain(XRHandJointID.ThumbMetacarpal,XRHandJointID.ThumbTip,wrist);
            Chain(XRHandJointID.IndexMetacarpal,XRHandJointID.IndexTip,wrist);
            Chain(XRHandJointID.MiddleMetacarpal,XRHandJointID.MiddleTip,wrist);
            Chain(XRHandJointID.RingMetacarpal,XRHandJointID.RingTip,wrist);
            Chain(XRHandJointID.LittleMetacarpal,XRHandJointID.LittleTip,wrist);
            joints=new Transform[2][];bones=new Transform[2][];
            for(int side=0;side<2;side++)
            {
                var prefab = side == 0 ? leftHandPrefab : rightHandPrefab;
                if (prefab != null)
                {
                    handMeshes[side] = Instantiate(prefab, trackingSpace, false);
                    armSurfaces[side] = new GameObject(side == 0 ? "Left continuous arm (IK)" : "Right continuous arm (IK)").AddComponent<ContinuousArmSurface>();
                    armSurfaces[side].transform.SetParent(body, false);
                    armSurfaces[side].Initialize(skin,handMeshes[side].WristSeamPoints,handMeshes[side].WristSeamNormals);
                    handRoots[side] = handMeshes[side].transform;
                    continue;
                }
                upper[side]=Part(side==0?"Left upper arm (IK)":"Right upper arm (IK)",PrimitiveType.Capsule,body,skin);
                forearm[side]=Part(side==0?"Left forearm (IK)":"Right forearm (IK)",PrimitiveType.Capsule,body,skin);
                handRoots[side]=new GameObject(side==0?"Displayed left hand":"Displayed right hand").transform;
                handRoots[side].SetParent(trackingSpace,false);
                palms[side]=Part("Palm surface",PrimitiveType.Sphere,handRoots[side],skin);
                joints[side]=new Transform[count];bones[side]=new Transform[count];
                for(int i=0;i<count;i++)
                {
                    joints[side][i]=Part(XRHandJointIDUtility.FromIndex(i).ToString(),PrimitiveType.Sphere,handRoots[side],skin);
                    joints[side][i].localScale=Vector3.one*.013f;
                    if(parents[i]>=0)bones[side][i]=Part("Hand segment "+i,PrimitiveType.Capsule,handRoots[side],skin);
                }
            }
            for(int i=0;i<6;i++)
            {
                fitMarkers[i]=Part((i<3?"Left ":"Right ")+new[]{"shoulder estimate","elbow estimate","tracked wrist"}[i%3],PrimitiveType.Sphere,body,shirt);
                fitMarkers[i].localScale=Vector3.one*.025f;
                var text=new GameObject("Joint label").AddComponent<TextMesh>();text.transform.SetParent(fitMarkers[i],false);
                text.transform.localPosition=Vector3.up*1.4f;text.transform.localScale=Vector3.one*40;
                text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.GetComponent<Renderer>().sharedMaterial=text.font.material;
                text.text=new[]{"Shoulder (estimated)","Elbow (estimated)","Wrist"}[i%3];text.fontSize=40;text.characterSize=.004f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
            }
            ready=true;
        }
        Transform Part(string name,PrimitiveType shape,Transform parent,Material material)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);
            Destroy(go.GetComponent<Collider>());var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;return go.transform;
        }
        void Chain(XRHandJointID first,XRHandJointID last,int wrist){for(int i=first.ToIndex();i<=last.ToIndex();i++)parents[i]=i==first.ToIndex()?wrist:i-1;}
        void Refresh()
        {
            if(!ready)return;
            bool visible=setup.Aligned&&setup.HeadTracked;body.gameObject.SetActive(visible);
            var p=setup.Parameters;
            if(!visible||!p.tableContactEnabled||previousTableHeight!=p.tableHeight){foreach(var rest in rests)rest.Reset();previousTableHeight=p.tableHeight;}
            torso.localPosition=new Vector3(0,setup.SeatedHeadHeight-.52f,-.09f);torso.localScale=new Vector3(p.shoulderWidth,.28f,.23f);
            tableTop.gameObject.SetActive(setup.SurfacePreview);
            foreach(var leg in tableLegs)leg.gameObject.SetActive(setup.SurfacePreview);
            foreach(var marker in fitMarkers)marker.gameObject.SetActive(false);
            tableTop.localPosition=new Vector3(0,p.tableHeight-.02f,p.tableNearEdge+p.tableDepth*.5f);
            tableTop.localScale=new Vector3(p.tableWidth,.04f,p.tableDepth);
            for(int i=0;i<4;i++)
            {
                float x=(i%2==0?-1:1)*(p.tableWidth*.5f-.05f);
                float z=p.tableNearEdge+(i<2?.05f:p.tableDepth-.05f);
                tableLegs[i].localPosition=new Vector3(x,(p.tableHeight-.04f)*.5f,z);tableLegs[i].localScale=new Vector3(.04f,p.tableHeight-.04f,.04f);
            }
            Draw(0,mapping.Left,visible);Draw(1,mapping.Right,visible);
            if(bodyTargets!=null && bodyTargets.TryGet(mapping.Left,out var leftShoulder,out _) && bodyTargets.TryGet(mapping.Right,out var rightShoulder,out _))
            {
                torso.localPosition=(leftShoulder+rightShoulder)*.5f+Vector3.down*.24f;
                torso.localScale=new Vector3(Vector3.Distance(leftShoulder,rightShoulder)+.08f,.28f,.25f);
            }
            float minimum=Mathf.Min(surfaceClearance[0],surfaceClearance[1]);
            setup.SurfaceFitWarning=minimum<-.005f?"Arm/table overlap: fit check needed":bodyTargets!=null?bodyTargets.Status:"";
            if(visible && Time.realtimeSinceStartupAsDouble>=nextFitReport)
            {
                nextFitReport=Time.realtimeSinceStartupAsDouble+5;
                Debug.Log($"[SurfaceFit] table={p.tableHeight:F3}m fullness={p.armFullness:F2} supportedIK={p.tableSupportEnabled} leftClearance={surfaceClearance[0]:F3}m rightClearance={surfaceClearance[1]:F3}m. handLiftL={HandTableLift[0]:F4}m handLiftR={HandTableLift[1]:F4}m armSurfaceLiftL={ArmSurfaceLift[0]:F4}m armSurfaceLiftR={ArmSurfaceLift[1]:F4}m. elbowLiftL={ElbowSupportLift[0]:F4}m elbowLiftR={ElbowSupportLift[1]:F4}m palmTiltL={PalmFlattenDegrees[0]:F1}deg palmTiltR={PalmFlattenDegrees[1]:F1}deg previousRenderMs={RenderMilliseconds:F2}. Display corrections only; not sensed physical contact.");
            }
        }
        void Draw(int side,DisplayedHandPose hand,bool bodyVisible)
        {
            handRoots[side].gameObject.SetActive(setup.HeadTracked);
            HandTableLift[side]=PalmFlattenDegrees[side]=0;
            bool valid=setup.HeadTracked;
            foreach(bool jointValid in hand.Valid)valid&=jointValid;
            var rest=rests[side];
            if(previousSource[side]!=hand.SourceHand||previousReflection[side]!=hand.Reflected)
                rest.Reset();
            previousSource[side]=hand.SourceHand;previousReflection[side]=hand.Reflected;
            bool guard=bodyVisible && setup.SurfacePreview && valid && handMeshes[side]!=null;
            bool assist=guard && bodyTargets!=null && setup.Parameters.tableContactEnabled;
            belowTable[side]=false;
            if(!assist)rest.Reset();
            var displayed=hand;
            bool eligible=false;
            if(guard)
            {
                displayed=contactHands[side];
                System.Array.Copy(hand.Joints,displayed.Joints,hand.Joints.Length);
                System.Array.Copy(hand.Valid,displayed.Valid,hand.Valid.Length);
                displayed.SourceHand=hand.SourceHand;displayed.Reflected=hand.Reflected;displayed.InputSequence=hand.InputSequence;
                var p=setup.Parameters;
                int w=XRHandJointID.Wrist.ToIndex();
                Vector3 wristBody=setup.BodyFrame.InverseTransformPoint(trackingSpace.TransformPoint(hand.Joints[w].position));
                Vector3 along=hand.Joints[XRHandJointID.MiddleProximal.ToIndex()].position-hand.Joints[w].position;
                Vector3 across=hand.Joints[XRHandJointID.IndexProximal.ToIndex()].position-hand.Joints[XRHandJointID.LittleProximal.ToIndex()].position;
                Vector3 normal=Vector3.Cross(along,across).normalized;
                Vector3 up=trackingSpace.InverseTransformDirection(setup.BodyFrame.up);
                if(Vector3.Dot(normal,up)<0)normal=-normal;
                eligible=TableContactMath.Inside(wristBody,p) && wristBody.y>=p.tableHeight-.025f && wristBody.y<=p.tableHeight+.09f && Vector3.Dot(normal,up)>.82f;
                eligible &= assist;
                rest.BeginFrame(eligible,Time.unscaledDeltaTime);
                Quaternion flatten=Quaternion.Slerp(Quaternion.identity,Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(normal,up),8),rest.FlatBlend);
                PalmFlattenDegrees[side]=Quaternion.Angle(Quaternion.identity,flatten);
                Vector3 pivot=hand.Joints[w].position;
                for(int i=0;i<displayed.Joints.Length;i++)
                    displayed.Joints[i]=new Pose(pivot+flatten*(hand.Joints[i].position-pivot),flatten*hand.Joints[i].rotation);
            }
            if(handMeshes[side]!=null)handMeshes[side].Apply(displayed,trackingSpace,setup.HeadTracked);
            else DrawPrimitiveHand(side,displayed);
            if(guard)
            {
                // Include a small edge margin so the shared arm/wrist seam stays above the plane too.
                float lowest=handMeshes[side].LowestOverTable(setup.BodyFrame,setup.Parameters,.08f);
                float offset=rest.UpdateSurface(lowest,handMeshes[side].HighestOverTable,setup.Parameters.tableHeight,eligible,Time.unscaledDeltaTime);
                belowTable[side]=float.IsFinite(lowest) && handMeshes[side].HighestOverTable<setup.Parameters.tableHeight;
                if(!belowTable[side] && float.IsFinite(lowest))
                    offset=Mathf.Max(offset,setup.Parameters.tableHeight+.001f-handMeshes[side].LowestWholeHand);
                // Fully below-table hands retain their actual pose and table occlusion; do not pull them upward.
                if(belowTable[side])offset=0;
                offset=Mathf.Clamp(offset,-.01f,.015f); // Never mask a bad height by dragging the wrist far from tracking.
                HandTableLift[side]=offset;
                if(offset!=0)
                {
                    Vector3 delta=trackingSpace.InverseTransformVector(setup.BodyFrame.up*offset);
                    for(int i=0;i<displayed.Joints.Length;i++)displayed.Joints[i].position+=delta;
                    handMeshes[side].Apply(displayed,trackingSpace,setup.HeadTracked);
                }
            }
            DrawArm(side,displayed,bodyVisible);
        }
        void DrawPrimitiveHand(int side, DisplayedHandPose hand)
        {
            for(int i=0;i<hand.Joints.Length;i++)
            {
                bool valid=hand.Valid[i];joints[side][i].gameObject.SetActive(valid);
                if(valid)joints[side][i].SetLocalPositionAndRotation(hand.Joints[i].position,hand.Joints[i].rotation);
                if(bones[side][i]==null)continue;
                bool boneValid=valid&&hand.Valid[parents[i]];bones[side][i].gameObject.SetActive(boneValid);
                if(boneValid)Segment(bones[side][i],hand.Joints[parents[i]].position,hand.Joints[i].position,.012f);
            }
            int w=XRHandJointID.Wrist.ToIndex(),m=XRHandJointID.MiddleProximal.ToIndex(),idx=XRHandJointID.IndexProximal.ToIndex(),little=XRHandJointID.LittleProximal.ToIndex();
            bool palmValid=hand.Valid[w]&&hand.Valid[m]&&hand.Valid[idx]&&hand.Valid[little];
            palms[side].gameObject.SetActive(palmValid);
            if(palmValid)
            {
                Vector3 a=hand.Joints[w].position,b=hand.Joints[m].position;
                Vector3 along=b-a,across=hand.Joints[idx].position-hand.Joints[little].position;
                Vector3 normal=Vector3.Cross(along,across);
                if(along.sqrMagnitude>.00001f&&normal.sqrMagnitude>.0000001f)
                {
                    palms[side].localPosition=Vector3.Lerp(a,b,.55f);
                    palms[side].localRotation=Quaternion.LookRotation(along,normal);
                    palms[side].localScale=new Vector3(Mathf.Clamp(across.magnitude*1.25f,.04f,.11f),.024f,Mathf.Clamp(along.magnitude*1.1f,.06f,.13f));
                }
                else palms[side].gameObject.SetActive(false);
            }
        }
        void DrawArm(int side, DisplayedHandPose hand, bool bodyVisible)
        {
            int w = XRHandJointID.Wrist.ToIndex();
            bool reachable=false;
            surfaceClearance[side]=float.PositiveInfinity;ArmSurfaceLift[side]=ElbowSupportLift[side]=0;
            if(bodyVisible&&!belowTable[side]&&hand.Valid[w]&&(handMeshes[side]==null||handMeshes[side].Visible))
            {
                var p=setup.Parameters;
                Vector3 shoulder=new Vector3((side==0?-1:1)*p.shoulderWidth*.5f,setup.SeatedHeadHeight-p.shoulderDrop,-.07f);
                Vector3 wrist=setup.BodyFrame.InverseTransformPoint(trackingSpace.TransformPoint(hand.Joints[w].position));
                Vector3 pole=SeatedArmMath.RestingElbowPole(side==0);
                Vector3 elbow;
                if(bodyTargets!=null)
                    reachable=bodyTargets.TryGetFitted(hand,wrist,out shoulder,out elbow);
                else reachable=SeatedArmMath.TryElbow(shoulder,wrist,p.upperArmLength,p.forearmLength,pole,out elbow);
                float aboveTable=wrist.y-p.tableHeight;
                bool overTable=Mathf.Abs(wrist.x)<p.tableWidth*.5f && wrist.z>=p.tableNearEdge && wrist.z<=p.tableNearEdge+p.tableDepth;
                if(bodyTargets==null && reachable && p.tableSupportEnabled && overTable && aboveTable>-.03f && aboveTable<.16f)
                {
                    // Blend back to free-space IK as the wrist lifts. Never claim sensed contact.
                    float support=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.07f,.16f,aboveTable));
                    float edge=Mathf.Min(p.tableWidth*.5f-Mathf.Abs(wrist.x),Mathf.Min(wrist.z-p.tableNearEdge,p.tableNearEdge+p.tableDepth-wrist.z));
                    support*=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/.05f));
                    if(SeatedArmMath.TrySupportedElbow(shoulder,wrist,p.upperArmLength,p.forearmLength,pole,p.tableHeight+.04f*p.armFullness,support,out var supported))elbow=supported;
                }
                if(reachable && bodyTargets!=null && rests[side].Active && !bodyTargets.HasFit)
                {
                    // Support the estimated elbow vertically; preserve its lateral location to avoid bowed-out arms.
                    float target=setup.Parameters.tableHeight+.035f*p.armFullness;
                    ElbowSupportLift[side]=Mathf.Clamp(target-elbow.y,0,.16f)*rests[side].FlatBlend;
                    elbow.y+=ElbowSupportLift[side];
                }
                if(reachable)
                {
                    for(int j=0;j<3;j++)
                    {
                        var marker=fitMarkers[side*3+j];marker.gameObject.SetActive(setup.ShowFitMarkers);
                        marker.localPosition=j==0?shoulder:j==1?elbow:wrist;
                        marker.rotation=Camera.main!=null?Camera.main.transform.rotation:Quaternion.identity;
                    }
                    if (armSurfaces[side] != null)
                    {
                        Quaternion wristRotation=Quaternion.Inverse(setup.BodyFrame.rotation)*trackingSpace.rotation*hand.Joints[w].rotation;
                        armSurfaces[side].Apply(shoulder, elbow, wrist, wristRotation,p.armFullness);
                        // Large corrections flatten anatomy. Correct only small residual skin overlap;
                        // a misplaced surface must be recalibrated rather than deforming the wearer.
                        if(setup.SurfacePreview && armSurfaces[side].MinimumClearance(p)>=-.015f)
                            ArmSurfaceLift[side]=armSurfaces[side].ResolveSmallTableOverlap(p);
                        surfaceClearance[side]=armSurfaces[side].MinimumClearance(p);
                    }
                    else { Segment(upper[side],shoulder,elbow,.075f);Segment(forearm[side],elbow,wrist,.058f); }
                }
                if(reachable!=previousReach[side])Debug.Log($"[Seated] {(side==0?"Left":"Right")} arm reachable={reachable}; hand remains at tracked position. Adjust estimated body dimensions if needed.");
                previousReach[side]=reachable;
            }
            if (armSurfaces[side] != null) armSurfaces[side].gameObject.SetActive(reachable);
            else { upper[side].gameObject.SetActive(reachable);forearm[side].gameObject.SetActive(reachable); }
        }
        static void Segment(Transform segment,Vector3 from,Vector3 to,float diameter)
        {segment.localPosition=(from+to)*.5f;segment.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);segment.localScale=new Vector3(diameter,Vector3.Distance(from,to)*.5f,diameter);}
        void OnDisable()
        {
            Application.onBeforeRender-=RenderBeforeDisplay;foreach(var rest in rests)rest.Reset();
            if(ready){body.gameObject.SetActive(false);foreach(var root in handRoots)root.gameObject.SetActive(false);}
        }
        void OnEnable(){lastRenderedFrame=-1;Application.onBeforeRender+=RenderBeforeDisplay;}
        void OnDestroy(){if(ready){Destroy(body.gameObject);foreach(var root in handRoots)Destroy(root.gameObject);}}
    }
}
