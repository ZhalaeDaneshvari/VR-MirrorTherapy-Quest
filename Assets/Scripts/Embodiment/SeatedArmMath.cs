using UnityEngine;
namespace QuestResearch.Embodiment
{
    public static class SeatedArmMath
    {
        // Prefer elbows down/back beside the torso, with only a small outward bias.
        // These are directional ratios, not measured offsets or forced elbow positions.
        public static Vector3 RestingElbowPole(bool left) => new Vector3(left ? -.12f : .12f, -1f, -.35f);

        // Select a different point on the exact two-bone elbow circle. This changes
        // only the inferred elbow, never tracked wrists or bone lengths. It models
        // a forearm supported by the known table, not measured elbow/contact data.
        public static bool TrySupportedElbow(Vector3 shoulder, Vector3 wrist, float upper, float lower,
            Vector3 pole, float minimumHeight, float weight, out Vector3 elbow)
        {
            if(!TryElbow(shoulder,wrist,upper,lower,pole,out elbow))return false;
            if(elbow.y>=minimumHeight || weight<=0)return true;
            Vector3 axis=(wrist-shoulder).normalized;
            float distance=Vector3.Distance(wrist,shoulder);
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            Vector3 centre=shoulder+axis*along;
            float radius=Vector3.Distance(elbow,centre);
            Vector3 projectedUp=Vector3.ProjectOnPlane(Vector3.up,axis);
            float verticalRange=radius*projectedUp.magnitude;
            if(verticalRange<.00001f || minimumHeight>centre.y+verticalRange)return false;
            Vector3 up=projectedUp.normalized;
            Vector3 side=Vector3.Cross(axis,up).normalized;
            float cosine=Mathf.Clamp((minimumHeight-centre.y)/verticalRange,-1,1);
            float sine=Mathf.Sqrt(Mathf.Max(0,1-cosine*cosine));
            Vector3 a=up*cosine+side*sine,b=up*cosine-side*sine;
            Vector3 preferred=(elbow-centre)/radius;
            Vector3 target=Vector3.Dot(a,preferred)>=Vector3.Dot(b,preferred)?a:b;
            elbow=centre+Vector3.Slerp(preferred,target,Mathf.Clamp01(weight))*radius;
            return true;
        }

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
