using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;

namespace QuestResearch.Embodiment
{
    // First-person upper body. Only hands/wrists come from tracking; shoulders and elbows are estimates.
    public sealed class SeatedAvatarDisplay : MonoBehaviour
    {
        [SerializeField] MovementRemapper mapping;
        [SerializeField] SeatedSetup setup;
        [SerializeField] Transform trackingSpace;
        [SerializeField] Material skin,shirt,tableMaterial;
        [SerializeField] RemappedHandMesh leftHandPrefab, rightHandPrefab;
        readonly RemappedHandMesh[] handMeshes = new RemappedHandMesh[2];
        readonly ContinuousArmSurface[] armSurfaces = new ContinuousArmSurface[2];
        public void ConfigureMeshes(RemappedHandMesh left, RemappedHandMesh right)
        { leftHandPrefab = left; rightHandPrefab = right; }
        Transform body,torso,tableTop;
        readonly Transform[] tableLegs=new Transform[4],upper=new Transform[2],forearm=new Transform[2],handRoots=new Transform[2],palms=new Transform[2];
        Transform[][] joints,bones;
        int[] parents;
        bool ready;
        bool[] previousReach={true,true};
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
                    armSurfaces[side].Initialize(skin);
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
            ready=true;mapping.Changed+=Refresh;setup.Changed+=Refresh;Refresh();
        }
        Transform Part(string name,PrimitiveType shape,Transform parent,Material material)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);
            Destroy(go.GetComponent<Collider>());var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
        }
        void Chain(XRHandJointID first,XRHandJointID last,int wrist){for(int i=first.ToIndex();i<=last.ToIndex();i++)parents[i]=i==first.ToIndex()?wrist:i-1;}
        void Refresh()
        {
            if(!ready)return;
            bool visible=setup.Aligned&&setup.HeadTracked;body.gameObject.SetActive(visible);
            var p=setup.Parameters;
            torso.localPosition=new Vector3(0,setup.SeatedHeadHeight-.52f,-.09f);torso.localScale=new Vector3(p.shoulderWidth,.28f,.23f);
            tableTop.localPosition=new Vector3(0,p.tableHeight-.02f,p.tableNearEdge+p.tableDepth*.5f);
            tableTop.localScale=new Vector3(p.tableWidth,.04f,p.tableDepth);
            for(int i=0;i<4;i++)
            {
                float x=(i%2==0?-1:1)*(p.tableWidth*.5f-.05f);
                float z=p.tableNearEdge+(i<2?.05f:p.tableDepth-.05f);
                tableLegs[i].localPosition=new Vector3(x,(p.tableHeight-.04f)*.5f,z);tableLegs[i].localScale=new Vector3(.04f,p.tableHeight-.04f,.04f);
            }
            Draw(0,mapping.Left,visible);Draw(1,mapping.Right,visible);
        }
        void Draw(int side,DisplayedHandPose hand,bool bodyVisible)
        {
            handRoots[side].gameObject.SetActive(setup.HeadTracked);
            if (handMeshes[side] != null)
                handMeshes[side].Apply(hand, trackingSpace, setup.HeadTracked);
            else DrawPrimitiveHand(side, hand);
            DrawArm(side, hand, bodyVisible);
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
            if(bodyVisible&&hand.Valid[w])
            {
                var p=setup.Parameters;
                Vector3 shoulder=new Vector3((side==0?-1:1)*p.shoulderWidth*.5f,setup.SeatedHeadHeight-p.shoulderDrop,-.07f);
                Vector3 wrist=setup.BodyFrame.InverseTransformPoint(trackingSpace.TransformPoint(hand.Joints[w].position));
                Vector3 pole=new Vector3(side==0?-1:1,-.6f,-.3f);
                reachable=SeatedArmMath.TryElbow(shoulder,wrist,p.upperArmLength,p.forearmLength,pole,out Vector3 elbow);
                if(reachable)
                {
                    if (armSurfaces[side] != null)
                    {
                        Vector3 across = setup.BodyFrame.InverseTransformDirection(trackingSpace.rotation * hand.Joints[w].rotation * Vector3.right);
                        armSurfaces[side].Apply(shoulder, elbow, wrist, across);
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
            if(mapping!=null)mapping.Changed-=Refresh;if(setup!=null)setup.Changed-=Refresh;
            if(ready){body.gameObject.SetActive(false);foreach(var root in handRoots)root.gameObject.SetActive(false);}
        }
        void OnEnable(){if(ready){mapping.Changed+=Refresh;setup.Changed+=Refresh;Refresh();}}
        void OnDestroy(){if(ready){Destroy(body.gameObject);foreach(var root in handRoots)Destroy(root.gameObject);}}
    }
}
