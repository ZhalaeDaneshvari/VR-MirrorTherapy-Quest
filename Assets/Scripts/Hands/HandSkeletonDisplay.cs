using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;

namespace QuestResearch.Hands
{
    // A diagnostic 1:1 display only; never writes poses back to the tracking source.
    public sealed class HandSkeletonDisplay : MonoBehaviour
    {
        [SerializeField] HandTrackingSource source;
        [SerializeField] Transform trackingSpace;
        [SerializeField] Material leftMaterial,rightMaterial;
        Transform[][] joints;
        LineRenderer[][] bones;
        int[] parents;
        bool created;
        public void Configure(HandTrackingSource input,Transform space,Material left,Material right)
        {source=input;trackingSpace=space;leftMaterial=left;rightMaterial=right;}
        void Start()
        {
            if(source==null||trackingSpace==null||leftMaterial==null||rightMaterial==null)
            {Debug.LogError("[QuestHands] Skeleton references missing.",this);enabled=false;return;}
            int count=XRHandJointID.EndMarker.ToIndex();
            parents=new int[count];for(int i=0;i<count;i++)parents[i]=-1;
            int wrist=XRHandJointID.Wrist.ToIndex();
            parents[XRHandJointID.Palm.ToIndex()]=wrist;
            Chain(XRHandJointID.ThumbMetacarpal,XRHandJointID.ThumbTip,wrist);
            Chain(XRHandJointID.IndexMetacarpal,XRHandJointID.IndexTip,wrist);
            Chain(XRHandJointID.MiddleMetacarpal,XRHandJointID.MiddleTip,wrist);
            Chain(XRHandJointID.RingMetacarpal,XRHandJointID.RingTip,wrist);
            Chain(XRHandJointID.LittleMetacarpal,XRHandJointID.LittleTip,wrist);
            joints=new Transform[2][];bones=new LineRenderer[2][];
            for(int side=0;side<2;side++)
            {
                var root=new GameObject(side==0?"Displayed LEFT hand":"Displayed RIGHT hand").transform;
                root.SetParent(trackingSpace,false);
                joints[side]=new Transform[count];bones[side]=new LineRenderer[count];
                var material=side==0?leftMaterial:rightMaterial;
                for(int i=0;i<count;i++)
                {
                    var joint=GameObject.CreatePrimitive(PrimitiveType.Sphere);joint.name=XRHandJointIDUtility.FromIndex(i).ToString();
                    joint.transform.SetParent(root,false);joint.transform.localScale=Vector3.one*.008f;
                    Destroy(joint.GetComponent<Collider>());
                    var renderer=joint.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    joints[side][i]=joint.transform;joint.SetActive(false);
                    if(parents[i]<0)continue;
                    var line=new GameObject("Bone to "+joint.name).AddComponent<LineRenderer>();
                    line.transform.SetParent(root,false);line.useWorldSpace=false;line.positionCount=2;line.widthMultiplier=.005f;
                    line.sharedMaterial=material;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
                    bones[side][i]=line;
                }
            }
            created=true;source.Changed+=Refresh;Refresh();
        }
        void Chain(XRHandJointID begin,XRHandJointID end,int wrist)
        {for(int i=begin.ToIndex();i<=end.ToIndex();i++)parents[i]=i==begin.ToIndex()?wrist:i-1;}
        void Refresh(){if(created){Render(0,source.Left);Render(1,source.Right);}}
        void Render(int side,TrackingHandFrame frame)
        {
            for(int i=0;i<frame.JointPoses.Length;i++)
            {
                bool valid=frame.JointValid[i];joints[side][i].gameObject.SetActive(valid);
                if(valid)joints[side][i].SetLocalPositionAndRotation(frame.JointPoses[i].position,frame.JointPoses[i].rotation);
                var bone=bones[side][i];if(bone==null)continue;
                bone.enabled=valid&&frame.JointValid[parents[i]];
                if(bone.enabled){bone.SetPosition(0,frame.JointPoses[parents[i]].position);bone.SetPosition(1,frame.JointPoses[i].position);}
            }
        }
        void OnDisable()
        {
            if(source!=null)source.Changed-=Refresh;
            if(!created)return;
            foreach(var hand in joints)foreach(var joint in hand)if(joint!=null)joint.gameObject.SetActive(false);
            foreach(var hand in bones)foreach(var bone in hand)if(bone!=null)bone.enabled=false;
        }
        void OnEnable(){if(created){source.Changed+=Refresh;Refresh();}}
        void OnDestroy()
        {
            if(!created)return;
            foreach(var hand in joints)if(hand[0]!=null)Destroy(hand[0].parent.gameObject);
        }
    }
}
