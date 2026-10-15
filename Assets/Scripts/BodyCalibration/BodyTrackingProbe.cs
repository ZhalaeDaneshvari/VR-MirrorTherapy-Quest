using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace QuestResearch.BodyCalibration
{
    // Diagnostic source only: no avatar fitting, mirroring or table constraints.
    // Meta pose -> Unity tracking space uses the SDK's handedness conversion, not mirror math.
    public sealed class BodyTrackingProbe : MonoBehaviour
    {
        public static readonly OVRPlugin.BoneId[] JointIds = {
            OVRPlugin.BoneId.Body_LeftShoulder, OVRPlugin.BoneId.Body_LeftArmUpper,
            OVRPlugin.BoneId.Body_LeftArmLower, OVRPlugin.BoneId.Body_LeftHandWrist,
            OVRPlugin.BoneId.Body_RightShoulder, OVRPlugin.BoneId.Body_RightArmUpper,
            OVRPlugin.BoneId.Body_RightArmLower, OVRPlugin.BoneId.Body_RightHandWrist };
        public readonly Pose[] Poses = new Pose[8];
        public readonly bool[] Valid = new bool[8];
        public readonly int[] LocationFlags = new int[8];
        public string Status { get; private set; } = "Starting";
        public string Calibration { get; private set; } = "Unknown";
        public string Fidelity { get; private set; } = "Unknown";
        public string HeightStatus { get; private set; } = "Automatic height (unverified)";
        public float Confidence { get; private set; }
        public double ReceivedAt { get; private set; } = double.NegativeInfinity;
        public double ProviderTime { get; private set; }
        public uint SkeletonRevision { get; private set; }
        public int OriginRevision { get; private set; }
        public bool Supported { get; private set; }
        public bool PermissionGranted { get; private set; }
        OVRPlugin.BodyState state;
        bool started, requested, paused, focused, heightApplied, resetPending;
        double nextStart, lastProviderTime = double.NaN;
        float height;
        XRInputSubsystem input;
        readonly List<XRInputSubsystem> inputs = new List<XRInputSubsystem>();
        const string PermissionName = "com.oculus.permission.BODY_TRACKING";
        [Serializable] sealed class Settings { public float standingHeightMetres; }
        void OnEnable()
        {
            focused=Application.isFocused;
            var path=Path.Combine(Application.persistentDataPath,"body-calibration.json");
            if(File.Exists(path))
            {
                try
                {
                    var settings=JsonUtility.FromJson<Settings>(File.ReadAllText(path));
                    height=settings==null?0:settings.standingHeightMetres;
                    if(float.IsNaN(height)||float.IsInfinity(height)||height<.5f||height>2.5f)
                    { height=0; HeightStatus="Invalid height file: using automatic"; }
                }
                catch(Exception ex) when(ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
                { HeightStatus="Height file unreadable: automatic"; Debug.LogWarning("[BodyProbe] "+ex.Message); }
            }
        }
        // Explicit new-wearer action: do not silently reuse another participant's height.
        public void BeginParticipant(float standingHeight)
        {
            height=standingHeight>=.5f && standingHeight<=2.5f?standingHeight:0;
            heightApplied=false;
            resetPending=true;
            HeightStatus=height>0?$"Known height {height:F2} m":"Automatic height (verify arm fit)";
            Invalidate("Recalibrating for new wearer");
            Debug.Log($"[BodyProbe] New wearer height={height:F2} resetQueued={resetPending}");
        }
        void Update()
        {
            if(!focused||paused){Invalidate("Suspended");return;}
            if(input==null)
            {
                SubsystemManager.GetSubsystems(inputs);
                foreach(var item in inputs) if(item.running){input=item;input.trackingOriginUpdated+=OriginChanged;break;}
            }
            if(input==null||!input.running){Invalidate("Waiting for XR session");return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            PermissionGranted=UnityEngine.Android.Permission.HasUserAuthorizedPermission(PermissionName);
            if(!PermissionGranted)
            {
                if(!requested){requested=true;UnityEngine.Android.Permission.RequestUserPermission(PermissionName);}
                Invalidate("Body permission needed (allow in headset)");return;
            }
#else
            Invalidate("Quest device required");return;
#endif
            double now=Time.realtimeSinceStartupAsDouble;
            if(!started)
            {
                if(now<nextStart)return;
                nextStart=now+2;
                Supported=OpenXRRuntime.IsExtensionEnabled("XR_FB_body_tracking")&&OVRPlugin.bodyTrackingSupported;
                if(!Supported){Invalidate("Body tracking unsupported / unavailable");return;}
                started=OVRPlugin.StartBodyTracking2(OVRPlugin.BodyJointSet.UpperBody);
                if(!started){Invalidate("Body service start failed; retrying");return;}
                bool fidelityRequested=OVRPlugin.RequestBodyTrackingFidelity(OVRPlugin.BodyTrackingFidelity2.High);
                Debug.Log("[BodyProbe] Started upper-body estimates; high fidelity request accepted="+fidelityRequested);
                heightApplied=false;
            }
            if(resetPending)
            {
                bool accepted=OVRPlugin.ResetBodyTrackingCalibration();resetPending=false;
                Debug.Log("[BodyProbe] New wearer calibration reset accepted="+accepted);
            }
            if(height>0&&!heightApplied)
            {
                heightApplied=true;
                bool accepted=OVRPlugin.SuggestBodyTrackingCalibrationOverride(new OVRPlugin.BodyTrackingCalibrationInfo{BodyHeight=height});
                HeightStatus=$"Height {height:F2} m: override "+(accepted?"accepted":"rejected");
                Debug.Log("[BodyProbe] "+HeightStatus);
            }
            if(!OVRPlugin.GetBodyState4(OVRPlugin.Step.Render,OVRPlugin.BodyJointSet.UpperBody,ref state))
            {Invalidate("Body sample unavailable");return;}
            Calibration=state.CalibrationStatus.ToString();Fidelity=state.Fidelity.ToString();
            Confidence=state.Confidence;SkeletonRevision=state.SkeletonChangedCount;ProviderTime=state.Time;
            if(double.IsNaN(state.Time)||double.IsInfinity(state.Time)){Invalidate("Invalid provider timestamp");return;}
            if(state.Time!=lastProviderTime){lastProviderTime=state.Time;ReceivedAt=now;}
            if(now-ReceivedAt>.25){Invalidate("Body sample stale");return;}
            if(state.CalibrationStatus!=OVRPlugin.BodyTrackingCalibrationState.Valid||state.Confidence<=0)
            {Invalidate("Waiting for valid calibration / confidence");return;}
            Status="Estimated body (not an arm scan)";
            for(int i=0;i<JointIds.Length;i++)
            {
                int index=(int)JointIds[i];
                if(state.JointLocations==null||index>=state.JointLocations.Length){Valid[i]=false;LocationFlags[i]=0;continue;}
                var joint=state.JointLocations[index];
                LocationFlags[i]=(int)joint.LocationFlags;
                Valid[i]=joint.PositionValid&&joint.OrientationValid;
                if(Valid[i]) Poses[i]=new Pose(joint.Pose.Position.FromFlippedZVector3f(),joint.Pose.Orientation.FromFlippedZQuatf());
            }
        }
        void Invalidate(string reason){Array.Clear(Valid,0,Valid.Length);Array.Clear(LocationFlags,0,LocationFlags.Length);Status=reason;}
        void Stop(string reason)
        {
            Invalidate(reason);
            if(started){OVRPlugin.StopBodyTracking();started=false;}
            Calibration="Unknown";Fidelity="Unknown";Confidence=0;
            ReceivedAt=double.NegativeInfinity;lastProviderTime=double.NaN;nextStart=0;
        }
        void OriginChanged(XRInputSubsystem _) {OriginRevision++;Stop("Tracking origin changed");}
        void OnApplicationFocus(bool value){focused=value;if(!value)Stop("Suspended");}
        void OnApplicationPause(bool value){paused=value;if(value)Stop("Suspended");}
        void OnDisable(){Stop("Disabled");if(input!=null)input.trackingOriginUpdated-=OriginChanged;input=null;}
    }
}
