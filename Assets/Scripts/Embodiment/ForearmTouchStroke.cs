using System;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.OpenXR;
using QuestResearch.Hands;

namespace QuestResearch.Embodiment
{
    // A clinician-followed instruction and visual stimulus, never evidence of physical touch.
    public sealed class ForearmTouchStroke : MonoBehaviour
    {
        public enum Phase { Idle, Countdown, Stroking }
        public Phase State {get;private set;}
        public bool Active=>State!=Phase.Idle;
        public PhysicalHand PhysicalSide {get;private set;}=PhysicalHand.Left;
        public bool CrossLimb {get;private set;}=true;
        public int VisualSide=>CrossLimb?1-(int)PhysicalSide:(int)PhysicalSide;
        public float Duration {get;private set;}=4;
        public string Message {get;private set;}="Manual cue: physical touch is not detected.";
        public event Action<TouchEvent> Event;
        [Serializable] public sealed class TouchEvent
        {
            public string kind,utc,physicalSide,visualSide,movement,reason,source="manual_instruction";
            public double monotonicSeconds;public float duration,progress;public Vector3 displayedWorldPoint;
            public bool hasDisplayedPoint;public string strokeId;
        }
        SeatedSetup setup;HandTrackingSource hands;SeatedAvatarDisplay avatar;
        Transform marker;Material material;AudioSource audioSource;AudioClip tick,go,end;
        double requestedAt,strokeAt,lastFrame,nextSample;int lastCount=-1;
        MovementCondition movement;bool showSource;string strokeId;
        public void Configure(SeatedSetup s,HandTrackingSource h,SeatedAvatarDisplay a,Material skin)
        {
            setup=s;hands=h;avatar=a;
            var item=GameObject.CreatePrimitive(PrimitiveType.Sphere);item.name="Manual forearm stroke contact marker";
            Destroy(item.GetComponent<Collider>());marker=item.transform;marker.SetParent(transform,false);marker.localScale=new Vector3(.022f,.008f,.022f);
            material=new Material(skin){color=new Color(.1f,.85f,.8f)};item.GetComponent<Renderer>().sharedMaterial=material;item.SetActive(false);
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;audioSource.volume=.35f;
            tick=Tone(600,.07f);go=Tone(900,.12f);end=Tone(450,.12f);
        }
        static AudioClip Tone(float hz,float length)
        {
            const int rate=24000;int n=(int)(rate*length);var values=new float[n];
            for(int i=0;i<n;i++)values[i]=Mathf.Sin(2*Mathf.PI*hz*i/rate)*.3f*Mathf.Sin(Mathf.PI*i/n);
            var clip=AudioClip.Create("Touch timing cue",n,1,rate,false);clip.SetData(values,0);return clip;
        }
        public bool SetOptions(PhysicalHand source,bool cross,float seconds)
        {
            if(Active||!Enum.IsDefined(typeof(PhysicalHand),source)||!float.IsFinite(seconds)||seconds<2||seconds>8)return false;
            PhysicalSide=source;CrossLimb=cross;Duration=seconds;return true;
        }
        public static int CountdownAt(double elapsed)=>Mathf.Clamp(3-(int)Math.Floor(Math.Max(0,elapsed)),0,3);
        public static float ProgressAt(double elapsed,float duration)=>Mathf.Clamp01((float)(elapsed/duration));
        bool Ready(out string reason)
        {
            reason="Align and confirm the table first.";
            if(setup==null||avatar==null||!avatar.HasBodyFit||!setup.Aligned||!setup.Parameters.tableAlignmentConfirmed||!setup.HeadTracked)return false;
            reason="Keep both hands tracked, flat and resting on the table.";
            if(!Application.isFocused||!OpenXRUtility.IsUserPresent)return false;
            for(int side=0;side<2;side++)
            {
                var hand=side==0?hands.Left:hands.Right;
                if(hand.Availability!=HandAvailability.Available||Time.realtimeSinceStartupAsDouble-hand.ReceivedAt>.15)return false;
                var p=hand.JointPoses;
                Vector3 along=p[XRHandJointID.MiddleProximal.ToIndex()].position-p[XRHandJointID.Wrist.ToIndex()].position;
                Vector3 across=p[XRHandJointID.IndexProximal.ToIndex()].position-p[XRHandJointID.LittleProximal.ToIndex()].position;
                if(Mathf.Abs(Vector3.Dot(Vector3.Cross(along,across).normalized,Vector3.up))<.9f)return false;
            }
            reason="Displayed forearm unavailable. Check arm fit.";
            if(!avatar.TryTouchPoint(VisualSide,0,out _))return false;
            reason="";return true;
        }
        public bool StartStroke()
        {
            if(Active)return false;
            if(!Ready(out string reason)){Message=reason;Emit("touch_start_rejected",reason);return false;}
            movement=setup.Parameters.movementCondition;showSource=setup.Parameters.showSourceHand;
            strokeId=Guid.NewGuid().ToString("N");requestedAt=lastFrame=Time.realtimeSinceStartupAsDouble;
            State=Phase.Countdown;lastCount=3;Message="Touch begins in 3";audioSource.PlayOneShot(tick);
            Emit("touch_countdown_started");return true;
        }
        [BeforeRenderOrder(10001)]
        void Render()
        {
            if(!Active)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(now-lastFrame>.25){Stop("Rendering interrupted; restart the stroke.");return;}lastFrame=now;
            if(!Ready(out string reason)){Stop(reason);return;}
            if(setup.Parameters.movementCondition!=movement||setup.Parameters.showSourceHand!=showSource){Stop("Movement mapping changed.");return;}
            if(State==Phase.Countdown)
            {
                int count=CountdownAt(now-requestedAt);
                if(count>0){Message="Touch begins in "+count;if(count!=lastCount){lastCount=count;audioSource.PlayOneShot(tick);Emit("touch_countdown_tick");}return;}
                State=Phase.Stroking;strokeAt=now;nextSample=now;audioSource.PlayOneShot(go);Emit("visual_touch_start");
            }
            float progress=ProgressAt(now-strokeAt,Duration);
            if(!avatar.TryTouchPoint(VisualSide,progress,out var point)){Stop("Displayed forearm lost.");return;}
            marker.gameObject.SetActive(true);marker.position=point+Vector3.up*.004f;marker.rotation=Quaternion.identity;
            Message="Stroke now: elbow to wrist";
            if(now>=nextSample){nextSample=now+.1;Emit("visual_touch_sample","",progress,point,true);}
            if(progress>=1){Emit("visual_touch_stop","completed",1,point,true);State=Phase.Idle;marker.gameObject.SetActive(false);Message="Stroke complete.";audioSource.PlayOneShot(end);}
        }
        public void Stop(string reason="Stopped by researcher")
        {
            if(!Active)return;
            Emit(State==Phase.Stroking?"visual_touch_stop":"touch_countdown_cancelled",reason);
            State=Phase.Idle;if(marker!=null)marker.gameObject.SetActive(false);Message="Stopped: "+reason;
            if(audioSource!=null)audioSource.Stop();
        }
        void Emit(string kind,string reason="",float progress=0,Vector3 point=default,bool positioned=false)
        {
            Event?.Invoke(new TouchEvent{kind=kind,utc=DateTime.UtcNow.ToString("O"),monotonicSeconds=Time.realtimeSinceStartupAsDouble,
                physicalSide=PhysicalSide.ToString(),visualSide=((PhysicalHand)VisualSide).ToString(),movement=setup.Parameters.movementCondition.ToString(),
                duration=Duration,progress=progress,displayedWorldPoint=point,hasDisplayedPoint=positioned,reason=reason,strokeId=strokeId});
        }
        void OnEnable(){Application.onBeforeRender+=Render;}
        void OnDisable(){Application.onBeforeRender-=Render;Stop("Component disabled");}
        void OnApplicationFocus(bool focused){if(!focused)Stop("App lost focus");}
        void OnApplicationPause(bool paused){if(paused)Stop("App paused");}
        void OnDestroy(){if(marker!=null)Destroy(marker.gameObject);if(material!=null)Destroy(material);if(tick!=null)Destroy(tick);if(go!=null)Destroy(go);if(end!=null)Destroy(end);}
    }
}
