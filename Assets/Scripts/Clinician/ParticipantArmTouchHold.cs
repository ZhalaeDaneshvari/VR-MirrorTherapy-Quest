using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using QuestResearch.Embodiment;
using QuestResearch.Hands;

namespace QuestResearch.Clinician
{
    // Clinician test build only (added by ClinicianTrackerSetup; never part of the accepted pilot scene).
    // When the clinician's hand blocks the headset cameras, Quest hand tracking loses the participant's hand and the
    // pilot hides that arm. While the tracked clinician hand is close, this briefly keeps showing the participant's
    // last tracked *resting* displayed pose instead. It is an inference, not tracking: physical hand data is never
    // altered, the hold ends the moment tracking returns, and every held frame is logged by ClinicianTrackerFeature.
    public sealed class ParticipantArmTouchHold : MonoBehaviour
    {
        [SerializeField] MovementRemapper mapping;
        [SerializeField] HandTrackingSource hands;
        [SerializeField] SeatedSetup setup;
        [SerializeField] ClinicianTrackerFeature clinician;
        [SerializeField] float maxHoldSeconds=3,clinicianRadius=.35f,restSpeed=.05f,restWindow=.3f,maxLossDelay=.5f;
        sealed class Side
        {
            public readonly DisplayedHandPose snapshot=new DisplayedHandPose();
            public readonly Queue<(double t,Vector3 p)> history=new Queue<(double,Vector3)>();
            public bool have,resting;public double snapshotAt=-1,holdSince=-1;public MovementCondition condition;
        }
        readonly Side[] sides={new Side(),new Side()};
        public float MaxHoldSeconds=>maxHoldSeconds;public float ClinicianRadius=>clinicianRadius;
        public bool Held(int side)=>sides[side].holdSince>=0;
        public void Configure(MovementRemapper remapper,HandTrackingSource source,SeatedSetup alignment,ClinicianTrackerFeature feature)
        {mapping=remapper;hands=source;setup=alignment;clinician=feature;}
        void OnEnable(){Application.onBeforeRender+=Apply;}
        void OnDisable()
        {
            Application.onBeforeRender-=Apply;
            for(int i=0;i<2;i++)if(sides[i].holdSince>=0){End(i,"component disabled");(i==0?mapping.Left:mapping.Right).Invalidate();}
        }
        static bool AllValid(DisplayedHandPose pose){foreach(bool v in pose.Valid)if(!v)return false;return true;}
        // Runs after the remapper and before SeatedAvatarDisplay (10000) consumes the mapped poses.
        [BeforeRenderOrder(9999)]
        void Apply()
        {
            if(mapping==null||hands==null||setup==null||clinician==null)return;
            double now=Time.realtimeSinceStartupAsDouble;
            Step(0,mapping.Left,now);Step(1,mapping.Right,now);
        }
        void Step(int index,DisplayedHandPose shown,double now)
        {
            var s=sides[index];var source=shown.SourceHand==PhysicalHand.Left?hands.Left:hands.Right;
            int w=XRHandJointID.Wrist.ToIndex();
            if(source.Availability==HandAvailability.Available&&AllValid(shown))
            {
                if(s.holdSince>=0)End(index,"participant hand tracked again");
                var wrist=shown.Joints[w].position;s.history.Enqueue((now,wrist));
                while(s.history.Count>1&&now-s.history.Peek().t>restWindow)s.history.Dequeue();
                var oldest=s.history.Peek();double span=now-oldest.t;
                s.resting=span>=restWindow*.8&&Vector3.Distance(wrist,oldest.p)/span<restSpeed;
                Array.Copy(shown.Joints,s.snapshot.Joints,shown.Joints.Length);Array.Copy(shown.Valid,s.snapshot.Valid,shown.Valid.Length);
                s.snapshot.SourceHand=shown.SourceHand;s.snapshot.Reflected=shown.Reflected;
                s.snapshotAt=now;s.condition=setup.Parameters.movementCondition;s.have=true;
                return;
            }
            if(source.Availability==HandAvailability.Available)return;// invalid for another reason (profile, mapping): never hold
            s.history.Clear();
            bool near=clinician.HandShown&&s.have&&Vector3.Distance(clinician.PalmInSpace,s.snapshot.Joints[w].position)<=clinicianRadius;
            if(s.holdSince<0)
            {
                bool eligible=near&&s.resting&&now-s.snapshotAt<=maxLossDelay&&s.condition==setup.Parameters.movementCondition
                    &&s.snapshot.SourceHand==shown.SourceHand&&s.snapshot.Reflected==shown.Reflected&&setup.HeadTracked;
                if(!eligible)return;
                s.holdSince=now;
                clinician.RecordExternal("participant_arm_hold_started",Name(index)+" displayed arm held: participant hand lost ("+source.Availability+") with clinician palm "
                    +(Vector3.Distance(clinician.PalmInSpace,s.snapshot.Joints[w].position)*100).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" cm away");
            }
            else
            {
                string reason=now-s.holdSince>maxHoldSeconds?"held for the "+maxHoldSeconds+" s maximum":!near?"clinician hand moved away or hidden":
                    s.condition!=setup.Parameters.movementCondition?"movement condition changed":!setup.HeadTracked?"head not tracked":null;
                if(reason!=null){End(index,reason);shown.Invalidate();return;}
            }
            Array.Copy(s.snapshot.Joints,shown.Joints,shown.Joints.Length);Array.Copy(s.snapshot.Valid,shown.Valid,shown.Valid.Length);
        }
        static string Name(int side)=>side==0?"Left":"Right";
        void End(int index,string reason)
        {
            var s=sides[index];double held=Time.realtimeSinceStartupAsDouble-s.holdSince;s.holdSince=-1;s.have=false;s.resting=false;s.history.Clear();
            clinician.RecordExternal("participant_arm_hold_ended",Name(index)+" arm hold ended after "+held.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" s: "+reason);
        }
    }
}
