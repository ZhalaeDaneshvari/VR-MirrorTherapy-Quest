using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestResearch.Clinician
{
    // Two rigid transforms, both explicit and logged:
    //  alignment: tracker frame (OpenVR converted to Unity axes) -> Quest tracking space. Both frames are
    //             gravity-aligned (+Y up), so only yaw about +Y and a translation are estimated.
    //  mount:     clinician wrist (XR Hands wrist joint convention) in the tracker's local frame.
    [Serializable]
    public sealed class ClinicianCalibration
    {
        public int schema=1;
        public string revision,createdUtc,trackerSerial,clinicianHand;
        public float yawDegrees;public Vector3 translation;
        public Vector3 mountPosition;public Quaternion mountRotation=Quaternion.identity;
        public int samples;public float residualRmsMetres,orientationSpreadDegrees,positionSpreadMetres,mountRotationSpreadDegrees;
        public Pose TrackerInSpace(Pose tracker)
        {
            var yaw=Quaternion.Euler(0,yawDegrees,0);
            return new Pose(yaw*tracker.position+translation,yaw*tracker.rotation);
        }
        public Pose WristInSpace(Pose tracker)
        {
            var t=TrackerInSpace(tracker);
            return new Pose(t.position+t.rotation*mountPosition,t.rotation*mountRotation);
        }
    }

    // Co-motion calibration: the person wearing the headset straps the tracker on their own hand exactly as the
    // clinician will wear it and moves that hand slowly. For each paired sample the Quest wrist w, tracker
    // position p and rotation Q satisfy w = Ry(yaw)(p + Q o) + t. For a fixed yaw this is linear in the
    // mount offset o and translation t, so yaw is found by 1-D search and refined; no hand-tuned offsets.
    public static class ClinicianCalibrationSolver
    {
        public struct Sample{public Vector3 trackerPosition,wristPosition;public Quaternion trackerRotation,wristRotation;}
        // Provisional engineering gates for this prototype, not validated acceptance criteria.
        public const int MinimumSamples=300;
        public const float MinimumOrientationSpread=25f,MinimumPositionSpread=.05f,MaximumResidual=.02f,MaximumMountDistance=.25f;

        public static bool TrySolve(IReadOnlyList<Sample> samples,string serial,string hand,out ClinicianCalibration result,out string message)
        {
            result=null;int n=samples.Count;
            if(n<MinimumSamples){message="Only "+n+" usable samples (need "+MinimumSamples+"). Keep the strapped hand and tracker in view and moving slowly.";return false;}
            var p=new double[n,3];var q=new double[n,9];var w=new double[n,3];
            for(int i=0;i<n;i++)
            {
                var s=samples[i];p[i,0]=s.trackerPosition.x;p[i,1]=s.trackerPosition.y;p[i,2]=s.trackerPosition.z;
                w[i,0]=s.wristPosition.x;w[i,1]=s.wristPosition.y;w[i,2]=s.wristPosition.z;
                var m=Matrix4x4.Rotate(s.trackerRotation);for(int r=0;r<3;r++)for(int c=0;c<3;c++)q[i,r*3+c]=m[r,c];
            }
            double best=double.MaxValue,bestYaw=0;var x=new double[6];
            for(int d=0;d<360;d++){double e=Fit(d,p,q,w,n,x);if(e<best){best=e;bestYaw=d;}}
            double lo=bestYaw-1.5,hi=bestYaw+1.5;// golden-section refinement of the 1-D yaw minimum
            const double g=0.6180339887498949;
            for(int k=0;k<60;k++){double a=hi-g*(hi-lo),b=lo+g*(hi-lo);if(Fit(a,p,q,w,n,x)<Fit(b,p,q,w,n,x))hi=b;else lo=a;}
            double yaw=(lo+hi)/2;double residual=Math.Sqrt(Fit(yaw,p,q,w,n,x)/n);
            var mount=new Vector3((float)x[0],(float)x[1],(float)x[2]);var translation=new Vector3((float)x[3],(float)x[4],(float)x[5]);
            var yawRotation=Quaternion.Euler(0,(float)yaw,0);
            var relative=new List<Quaternion>(n);var orientations=new List<Quaternion>(n);var wrists=new List<Vector3>(n);
            foreach(var s in samples){var t=yawRotation*s.trackerRotation;orientations.Add(t);relative.Add(Quaternion.Inverse(t)*s.wristRotation);wrists.Add(s.wristPosition);}
            var mountRotation=Mean(relative,out float mountSpread);Mean(orientations,out float orientationSpread);
            Vector3 centre=Vector3.zero;foreach(var v in wrists)centre+=v/n;double spread=0;foreach(var v in wrists)spread+=(v-centre).sqrMagnitude;
            float positionSpread=(float)Math.Sqrt(spread/n);
            result=new ClinicianCalibration{revision=Guid.NewGuid().ToString("N"),createdUtc=DateTime.UtcNow.ToString("O"),trackerSerial=serial,clinicianHand=hand,
                yawDegrees=Mathf.Repeat((float)yaw,360),translation=translation,mountPosition=mount,mountRotation=mountRotation,samples=n,
                residualRmsMetres=(float)residual,orientationSpreadDegrees=orientationSpread,positionSpreadMetres=positionSpread,mountRotationSpreadDegrees=mountSpread};
            message=string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "yaw {0:0.0} deg, residual {1:0.0} cm RMS, mount offset {2:0.0} cm, rotation spread {3:0.0} deg, hand spread {4:0.0} cm, tracker turned {5:0} deg, {6} samples",
                result.yawDegrees,residual*100,mount.magnitude*100,mountSpread,positionSpread*100,orientationSpread,n);
            if(orientationSpread<MinimumOrientationSpread){message="Rejected: turn and tilt the hand more ("+message+")";result=null;return false;}
            if(positionSpread<MinimumPositionSpread){message="Rejected: move the hand over a larger area ("+message+")";result=null;return false;}
            if(residual>MaximumResidual){message="Rejected: poor fit; move more slowly and keep the strap tight ("+message+")";result=null;return false;}
            if(mount.magnitude>MaximumMountDistance){message="Rejected: implausible tracker-to-wrist distance ("+message+")";result=null;return false;}
            return true;
        }
        // Sum of squared residuals for one yaw; x receives [mount offset (3), translation (3)].
        static double Fit(double yawDegrees,double[,] p,double[,] q,double[,] w,int n,double[] x)
        {
            double r=yawDegrees*Math.PI/180,c=Math.Cos(r),s=Math.Sin(r);
            // Unity yaw about +Y: (x,y,z) -> (c x + s z, y, -s x + c z)
            var N=new double[6,7];var M=new double[9];var rp=new double[3];var b=new double[3];
            for(int i=0;i<n;i++)
            {
                for(int k=0;k<3;k++){M[k]=c*q[i,k]+s*q[i,6+k];M[3+k]=q[i,3+k];M[6+k]=-s*q[i,k]+c*q[i,6+k];}
                rp[0]=c*p[i,0]+s*p[i,2];rp[1]=p[i,1];rp[2]=-s*p[i,0]+c*p[i,2];
                for(int k=0;k<3;k++)b[k]=w[i,k]-rp[k];
                // Row block A_i=[M | I]; accumulate A^T A and A^T b. M is a rotation, so M^T M = I.
                for(int a=0;a<3;a++)
                {
                    N[a,a]+=1;N[3+a,3+a]+=1;
                    for(int k=0;k<3;k++){N[a,3+k]+=M[k*3+a];N[3+k,a]+=M[k*3+a];N[a,6]+=M[k*3+a]*b[k];}
                    N[3+a,6]+=b[a];
                }
            }
            if(!Solve(N,x))return double.MaxValue;
            double sum=0;
            for(int i=0;i<n;i++)
            {
                for(int k=0;k<3;k++){M[k]=c*q[i,k]+s*q[i,6+k];M[3+k]=q[i,3+k];M[6+k]=-s*q[i,k]+c*q[i,6+k];}
                rp[0]=c*p[i,0]+s*p[i,2];rp[1]=p[i,1];rp[2]=-s*p[i,0]+c*p[i,2];
                for(int k=0;k<3;k++){double e=M[k*3]*x[0]+M[k*3+1]*x[1]+M[k*3+2]*x[2]+x[3+k]+rp[k]-w[i,k];sum+=e*e;}
            }
            return sum;
        }
        static bool Solve(double[,] a,double[] x)
        {
            const int n=6;
            for(int col=0;col<n;col++)
            {
                int pivot=col;for(int r=col+1;r<n;r++)if(Math.Abs(a[r,col])>Math.Abs(a[pivot,col]))pivot=r;
                if(Math.Abs(a[pivot,col])<1e-9)return false;
                if(pivot!=col)for(int k=0;k<=n;k++){double t=a[col,k];a[col,k]=a[pivot,k];a[pivot,k]=t;}
                for(int r=0;r<n;r++)
                {
                    if(r==col)continue;double f=a[r,col]/a[col,col];if(f==0)continue;
                    for(int k=col;k<=n;k++)a[r,k]-=f*a[col,k];
                }
            }
            for(int r=0;r<n;r++)x[r]=a[r,n]/a[r,r];
            return true;
        }
        // Sign-aligned average; spread is the largest angle of any sample from the mean.
        public static Quaternion Mean(IReadOnlyList<Quaternion> values,out float spreadDegrees)
        {
            spreadDegrees=0;if(values.Count==0)return Quaternion.identity;
            Vector4 sum=Vector4.zero;var reference=values[0];
            foreach(var v in values){float sign=Quaternion.Dot(v,reference)<0?-1:1;sum+=new Vector4(v.x,v.y,v.z,v.w)*sign;}
            sum.Normalize();var mean=new Quaternion(sum.x,sum.y,sum.z,sum.w);
            foreach(var v in values)spreadDegrees=Mathf.Max(spreadDegrees,Quaternion.Angle(mean,v));
            return mean;
        }
    }
}
