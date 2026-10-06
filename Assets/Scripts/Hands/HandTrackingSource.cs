using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.OpenXR;

namespace QuestResearch.Hands
{
    public enum HandAvailability { Waiting, Unavailable, Partial, Available, Stale, Suspended, ControllerRejected }

    // Reused snapshot of provider data, not avatar targets. Copy before retaining it in a future logger.
    // Poses are in XR tracking-origin space, in metres. Valid does not mean optically observed:
    // the runtime may infer occluded joints. No numerical confidence is exposed by this API.
    public sealed class TrackingHandFrame
    {
        public readonly Pose[] JointPoses = new Pose[XRHandJointID.EndMarker.ToIndex()];
        public readonly bool[] JointValid = new bool[XRHandJointID.EndMarker.ToIndex()];
        public HandAvailability Availability { get; internal set; } = HandAvailability.Waiting;
        public int ValidJointCount { get; internal set; }
        public double ReceivedAt { get; internal set; } = double.NegativeInfinity;
        public ulong Sequence { get; internal set; }
        public XRHandSubsystem.UpdateType UpdatePhase { get; internal set; }
        public HandTrackingDataSource? ReportedSource { get; internal set; }

        public void Invalidate(HandAvailability reason)
        {
            Array.Clear(JointValid,0,JointValid.Length);
            ValidJointCount=0;
            Availability=reason;
            ReportedSource=null;
        }
    }

    // The OpenXR feature owns subsystem lifetime. This observer only subscribes and reads it.
    public sealed class HandTrackingSource : MonoBehaviour
    {
        [Tooltip("Hide stale poses after this many seconds without a successful joint update.")]
        [SerializeField,Min(.05f)] float staleAfterSeconds=.25f;
        public TrackingHandFrame Left { get; } = new TrackingHandFrame();
        public TrackingHandFrame Right { get; } = new TrackingHandFrame();
        public event Action Changed;
        public bool SubsystemRunning => subsystem!=null && subsystem.running;
        readonly List<XRHandSubsystem> candidates=new List<XRHandSubsystem>();
        XRHandSubsystem subsystem;
        bool focused=true,paused;
        double nextSearch;

        void OnEnable(){focused=Application.isFocused;nextSearch=0;}
        void Update()
        {
            if(!focused||paused){InvalidateBoth(HandAvailability.Suspended);return;}
            if(subsystem!=null&&!subsystem.running)Detach();
            double now=Time.realtimeSinceStartupAsDouble;
            if(subsystem==null && now>=nextSearch)
            {
                nextSearch=now+.5;
                candidates.Clear();SubsystemManager.GetSubsystems(candidates);
                foreach(var candidate in candidates)
                    if(candidate.running){subsystem=candidate;subsystem.updatedHands+=OnHands;Debug.Log("[QuestHands] Subscribed to "+candidate.subsystemDescriptor.id);break;}
            }
            if(subsystem==null){InvalidateBoth(HandAvailability.Waiting);return;}
            bool changed=Expire(Left,now)|Expire(Right,now);
            if(changed)Changed?.Invoke();
        }
        bool Expire(TrackingHandFrame frame,double now)
        {
            if(frame.ValidJointCount==0 || now-frame.ReceivedAt<=staleAfterSeconds)return false;
            frame.Invalidate(HandAvailability.Stale);return true;
        }
        void OnHands(XRHandSubsystem hands,XRHandSubsystem.UpdateSuccessFlags flags,XRHandSubsystem.UpdateType phase)
        {
            if(!focused||paused){InvalidateBoth(HandAvailability.Suspended);return;}
            Read(hands.leftHand,Handedness.Left,Left,(flags&XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints)!=0,phase);
            Read(hands.rightHand,Handedness.Right,Right,(flags&XRHandSubsystem.UpdateSuccessFlags.RightHandJoints)!=0,phase);
            Changed?.Invoke();
        }
        void Read(XRHand hand,Handedness side,TrackingHandFrame frame,bool jointUpdate,XRHandSubsystem.UpdateType phase)
        {
            if(!hand.isTracked || !jointUpdate){frame.Invalidate(HandAvailability.Unavailable);return;}
            frame.ReportedSource=subsystem.TryGetExtendedData<HandTrackingDataSource>(side,out var source)?source:(HandTrackingDataSource?)null;
            if(frame.ReportedSource==HandTrackingDataSource.Controller)
            {
                frame.Invalidate(HandAvailability.ControllerRejected);
                frame.ReportedSource=HandTrackingDataSource.Controller;return;
            }
            int count=0;
            for(int i=0;i<frame.JointPoses.Length;i++)
            {
                bool valid=hand.GetJoint(XRHandJointIDUtility.FromIndex(i)).TryGetPose(out Pose pose);
                frame.JointValid[i]=valid;
                if(valid){frame.JointPoses[i]=pose;count++;}
            }
            frame.ValidJointCount=count;
            frame.Availability=count==frame.JointPoses.Length?HandAvailability.Available:count>0?HandAvailability.Partial:HandAvailability.Unavailable;
            frame.ReceivedAt=Time.realtimeSinceStartupAsDouble; // Receipt time, not sensor exposure time.
            frame.UpdatePhase=phase;frame.Sequence++;
        }
        void InvalidateBoth(HandAvailability reason)
        {
            if(Left.Availability==reason&&Right.Availability==reason)return;
            Left.Invalidate(reason);Right.Invalidate(reason);Changed?.Invoke();
        }
        void OnApplicationFocus(bool value){focused=value;if(!value)InvalidateBoth(HandAvailability.Suspended);}
        void OnApplicationPause(bool value){paused=value;if(value)InvalidateBoth(HandAvailability.Suspended);}
        void Detach(){if(subsystem!=null)subsystem.updatedHands-=OnHands;subsystem=null;}
        void OnDisable(){Detach();InvalidateBoth(HandAvailability.Suspended);}
    }
}
