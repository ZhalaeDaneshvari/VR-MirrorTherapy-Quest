using UnityEngine;
namespace QuestResearch.Embodiment
{
    public static class SeatedArmMath
    {
        // Analytic two-bone IK in participant-local coordinates. This estimates an elbow;
        // it does not measure one. Reject unreachable targets instead of stretching anatomy.
        public static bool TryElbow(Vector3 shoulder,Vector3 wrist,float upper,float lower,Vector3 pole,out Vector3 elbow)
        {
            elbow=shoulder;Vector3 delta=wrist-shoulder;float d=delta.magnitude;
            if(upper<=0||lower<=0||d<Mathf.Abs(upper-lower)+.002f||d>upper+lower-.002f)return false;
            Vector3 direction=delta/d;
            Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
            if(bend.sqrMagnitude<.001f)bend=Vector3.ProjectOnPlane(Vector3.forward,direction).normalized;
            if(bend.sqrMagnitude<.001f)return false;
            float along=(upper*upper-lower*lower+d*d)/(2*d);
            elbow=shoulder+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            return true;
        }
    }
}
