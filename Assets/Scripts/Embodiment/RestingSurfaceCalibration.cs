using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using QuestResearch.Hands;

namespace QuestResearch.Embodiment
{
    // An instructed contact estimate, NOT scene scanning or automatic touch detection.
    // Samples uncorrected hand skin while the wearer reports resting flat on the surface.
    public sealed class RestingSurfaceCalibration : MonoBehaviour
    {
        readonly List<float> samples=new List<float>();
        SeatedSetup setup;HandTrackingSource hands;SeatedAvatarDisplay avatar;BodyArmTargets arms;
        double begin,stableStart,lastSample;
        Vector3 anchorLeft,anchorRight;
        bool UseLeft=>setup.Parameters.UsesSide(0);
        bool UseRight=>setup.Parameters.UsesSide(1);
        string Palms=>UseLeft&&UseRight?"both palms":UseLeft?"your left palm":"your right palm";
        public bool Active {get;private set;}
        public string Message {get;private set;}="";
        public bool Succeeded {get;private set;}
        public void Initialize(SeatedSetup s,HandTrackingSource h,SeatedAvatarDisplay a,BodyArmTargets b)
        {setup=s;hands=h;avatar=a;arms=b;}
        public void Begin()
        {
            Succeeded=false;Active=true;begin=Time.realtimeSinceStartupAsDouble;ResetWindow();
            setup.SurfacePreview=false;setup.SetMovement(MovementCondition.Normal,true);
            Message="Rest "+Palms+" flat on the real table.\nKeep fingers relaxed and visible. Hold still.";
        }
        public void Cancel(){Active=false;ResetWindow();}
        void ResetWindow(){samples.Clear();stableStart=-1;lastSample=-1;}
        void Update()
        {
            if(!Active)return;
            double now=Time.realtimeSinceStartupAsDouble;
            if(!setup.Aligned||!setup.HeadTracked){Finish(false,"Tracking changed. Align again before fitting the table.");return;}
            if(now-begin>20){Finish(false,"Could not get steady hand tracking.\nRest "+Palms+" on the table and try again,\nor enter a measured height.");return;}
            if(now-begin<4)return; // Allow time to lower hands after selecting the button.
            if(now-lastSample<.05)return;
            lastSample=now;
            var left=hands.Left;var right=hands.Right;
            int wrist=XRHandJointID.Wrist.ToIndex();
            if((UseLeft&&!Ready(left,now))||(UseRight&&!Ready(right,now)))
            {ResetWindow();Message="Keep "+Palms+" down and visible.\nWaiting for stable hand tracking...";return;}
            Vector3 l=left.JointPoses[wrist].position,r=right.JointPoses[wrist].position;
            if(stableStart<0 || (UseLeft&&Vector3.Distance(anchorLeft,l)>.012f) || (UseRight&&Vector3.Distance(anchorRight,r)>.012f))
            {ResetWindow();stableStart=now;anchorLeft=l;anchorRight=r;}
            float lh=0,rh=0;
            if((UseLeft&&!avatar.TryRestingHeight(left,0,out lh))||(UseRight&&!avatar.TryRestingHeight(right,1,out rh))||(UseLeft&&UseRight&&Mathf.Abs(lh-rh)>.025f))
            {ResetWindow();Message="Rest "+Palms+" flat on the table.";return;}
            samples.Add(UseLeft&&UseRight?(lh+rh)*.5f:UseLeft?lh:rh);
            Message="Hold "+Palms+" still: "+Mathf.CeilToInt(Mathf.Max(0,2-(float)(now-stableStart)))+" sec\nTable hidden while sampling your resting hand(s).";
            if(now-stableStart<2||samples.Count<30)return;
            if(!TryEstimate(samples,out float height))
            {ResetWindow();Message="Surface estimate unstable or outside 45-110 cm.\nUse measured height if needed.";return;}
            setup.AdjustTableHeight(height-setup.Parameters.tableHeight);
            bool fitted=arms!=null&&arms.CaptureFit();
            Debug.Log($"[SurfaceCalibration] height={height:F3} samples={samples.Count} bodyFit={fitted}; instructed rest estimate, manual confirmation required");
            Finish(true,"Estimated surface: "+(height*100).ToString("F1")+" cm.\nCheck fit; use +/- 1 cm if needed.\n"+(fitted?"Arm lengths estimated; verify joint markers.":"Body fit not ready; retry capture before starting."));
        }
        static bool Ready(TrackingHandFrame hand,double now)=>hand.Availability==HandAvailability.Available && now-hand.ReceivedAt<=.15 && Flat(hand);
        static bool Flat(TrackingHandFrame hand)
        {
            foreach(var joint in new[]{XRHandJointID.Wrist,XRHandJointID.MiddleProximal,XRHandJointID.IndexProximal,XRHandJointID.LittleProximal})if(!hand.JointValid[joint.ToIndex()])return false;
            var p=hand.JointPoses;
            Vector3 along=p[XRHandJointID.MiddleProximal.ToIndex()].position-p[XRHandJointID.Wrist.ToIndex()].position;
            Vector3 across=p[XRHandJointID.IndexProximal.ToIndex()].position-p[XRHandJointID.LittleProximal.ToIndex()].position;
            // Plane horizontal within ~20 degrees. Palm direction checked from wrist to knuckles
            // by the guided instruction; no claim of detecting physical contact.
            return Mathf.Abs(Vector3.Dot(Vector3.Cross(along,across).normalized,Vector3.up))>.94f;
        }
        public static bool TryEstimate(List<float> values,out float height)
        {
            height=0;if(values.Count<30)return false;
            var sorted=values.ToArray();foreach(float v in sorted)if(!float.IsFinite(v))return false;
            Array.Sort(sorted);height=sorted[sorted.Length/2]-.001f;
            return height>=.45f&&height<=1.1f&&sorted[(int)(sorted.Length*.9f)]-sorted[(int)(sorted.Length*.1f)]<.015f;
        }
        void Finish(bool success,string message){Active=false;Succeeded=success;Message=message;}
        void OnDisable(){Cancel();}
    }
}
