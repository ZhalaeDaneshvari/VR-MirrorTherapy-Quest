using System;
using QuestResearch.Hands;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace QuestResearch.ClinicianProbe
{
    // Diagnostic only: right-controller identity is assigned by the test protocol, not person recognition.
    public sealed class ClinicianControllerProbe : MonoBehaviour
    {
        [SerializeField] HandTrackingSource hands;
        [SerializeField] Transform trackingSpace;
        [SerializeField] TextMesh status;
        [SerializeField] Transform marker;
        bool focused,paused,started,modeRequested;
        double nextRequest,nextStatus;
        int previousDevice=-1;
        public void Configure(HandTrackingSource input,Transform space,TextMesh label,Transform target)
        {hands=input;trackingSpace=space;status=label;marker=target;}
        void Start(){focused=Application.isFocused;started=true;marker.gameObject.SetActive(false);}
        void OnApplicationFocus(bool value){focused=value;nextRequest=0;Hide();}
        void OnApplicationPause(bool value){paused=value;nextRequest=0;Hide();}
        void Hide(){if(marker!=null)marker.gameObject.SetActive(false);}
        public static bool Accept(bool focused,bool tracked,int state,double age,Vector3 p,Quaternion q)
        {
            float n=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            return focused&&tracked&&(state&3)==3&&double.IsFinite(age)&&age>=0&&age<=.15
                &&float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)&&float.IsFinite(n)&&n>.5f&&n<1.5f;
        }
        static bool IsRightTouch(UnityEngine.InputSystem.InputDevice device)
        {
            if(!(device is XRController)||!device.added||!device.enabled)return false;
            bool right=false;foreach(var usage in device.usages)if(usage==UnityEngine.InputSystem.CommonUsages.RightHand)right=true;
            return right&&device.layout.IndexOf("Touch",StringComparison.OrdinalIgnoreCase)>=0;
        }
        void LateUpdate()
        {
            if(!started)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(focused&&!paused&&now>=nextRequest)
            {modeRequested=OVRInput.EnableSimultaneousHandsAndControllers();nextRequest=now+3;}
            XRController chosen=null;int validCount=0;double chosenAge=0;
            // Do not use XRNode.RightHand or 'active controller': those can resolve to wearer hands.
            // Detached and normal profiles are examined explicitly; simultaneous valid candidates are ambiguous.
            foreach(var device in InputSystem.devices)
            {
                if(!IsRightTouch(device))continue;
                var c=(XRController)device;
                double age=InputState.currentTime-c.lastUpdateTime; // Both in Input System clock domain.
                if(!Accept(focused&&!paused&&modeRequested,c.isTracked.isPressed,c.trackingState.ReadValue(),age,
                    c.devicePosition.ReadValue(),c.deviceRotation.ReadValue()))continue;
                chosen=c;chosenAge=age;validCount++;
            }
            bool valid=validCount==1;
            marker.gameObject.SetActive(valid);
            if(valid)
            {
                marker.SetPositionAndRotation(trackingSpace.TransformPoint(chosen.devicePosition.ReadValue()),
                    trackingSpace.rotation*chosen.deviceRotation.ReadValue());
                if(previousDevice!=chosen.deviceId)
                {previousDevice=chosen.deviceId;Debug.Log($"[ClinicianProbe] RIGHT controller device={chosen.deviceId} layout={chosen.layout}; grip pose, tracking-origin metres. Not person identity.");}
            }
            if(now<nextStatus)return;nextStatus=now+.2;
            status.text="CONTROLLER FEASIBILITY TEST\nParticipant hands: LEFT blue / RIGHT orange\n"
                +"Clinician: hold RIGHT controller (purple marker)\n\n"
                +$"Left: {HandStatus(hands.Left,now)}\nRight: {HandStatus(hands.Right,now)}\n"
                +(valid?$"Controller: TRACKED ({chosenAge*1000:0} ms update age)\n{chosen.layout}":validCount>1?"Controller: AMBIGUOUS - hidden":"Controller: UNAVAILABLE - hidden")
                +$"\nMultimodal request: {(modeRequested?"accepted":"waiting / unsupported")}\n\n"
                +"Keep participant hands still; clinician moves controller.\nCheck for hand swaps, loss or following the clinician.\nNo arms, mirroring, contact sensing or recording.";
        }
        static string HandStatus(TrackingHandFrame h,double now)
        {return $"{h.Availability}, {h.ValidJointCount} joints, source {h.ReportedSource?.ToString()??"unknown"}";}
        void OnDisable(){Hide();}
    }
}
