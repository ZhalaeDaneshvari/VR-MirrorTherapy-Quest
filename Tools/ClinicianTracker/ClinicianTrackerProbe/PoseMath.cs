using System;
using System.Collections.Generic;
using Valve.VR;
namespace QuestResearch.ClinicianTracker
{
    // OpenVR convention throughout: right-handed, +Y up, -Z forward, metres, quaternions x,y,z,w.
    // No handedness conversion happens here; raw values stay exactly in the provider's frame.
    public static class PoseMath
    {
        public static double[] Position(HmdMatrix34_t m)=>new double[]{m.m3,m.m7,m.m11};
        public static double[] Rotation(HmdMatrix34_t m)
        {
            double m00=m.m0,m01=m.m1,m02=m.m2,m10=m.m4,m11=m.m5,m12=m.m6,m20=m.m8,m21=m.m9,m22=m.m10;
            double tr=m00+m11+m22,x,y,z,w,s;
            if(tr>0){s=Math.Sqrt(tr+1)*2;w=0.25*s;x=(m21-m12)/s;y=(m02-m20)/s;z=(m10-m01)/s;}
            else if(m00>m11&&m00>m22){s=Math.Sqrt(1+m00-m11-m22)*2;w=(m21-m12)/s;x=0.25*s;y=(m01+m10)/s;z=(m02+m20)/s;}
            else if(m11>m22){s=Math.Sqrt(1+m11-m00-m22)*2;w=(m02-m20)/s;x=(m01+m10)/s;y=0.25*s;z=(m12+m21)/s;}
            else{s=Math.Sqrt(1+m22-m00-m11)*2;w=(m10-m01)/s;x=(m02+m20)/s;y=(m12+m21)/s;z=0.25*s;}
            return Normalize(new[]{x,y,z,w});
        }
        public static double[] Vector(HmdVector3_t v)=>new double[]{v.v0,v.v1,v.v2};
        public static double[] Normalize(double[] q){double n=Math.Sqrt(q[0]*q[0]+q[1]*q[1]+q[2]*q[2]+q[3]*q[3]);return n>0?new[]{q[0]/n,q[1]/n,q[2]/n,q[3]/n}:new double[]{0,0,0,1};}
        public static double Distance(double[] a,double[] b)=>Math.Sqrt(Sq(a[0]-b[0])+Sq(a[1]-b[1])+Sq(a[2]-b[2]));
        public static double Horizontal(double[] a,double[] b)=>Math.Sqrt(Sq(a[0]-b[0])+Sq(a[2]-b[2]));
        static double Sq(double v)=>v*v;
        static double Deg(double r)=>r*180/Math.PI;
        static double Clamp(double v)=>Math.Max(-1,Math.Min(1,v));
        // Angle of the tracker's local +Y axis from world up. Display/diagnostic only.
        public static double TiltDegrees(double[] q){double x=q[0],z=q[2];return Deg(Math.Acos(Clamp(1-2*(x*x+z*z))));}
        // Heading of the tracker's local -Z axis projected on the floor plane. Display/diagnostic only.
        public static double HeadingDegrees(double[] q){double x=q[0],y=q[1],z=q[2],w=q[3];return Deg(Math.Atan2(-2*(x*z+w*y),1-2*(x*x+y*y)));}
        public static double[] Multiply(double[] a,double[] b)=>new[]{
            a[3]*b[0]+a[0]*b[3]+a[1]*b[2]-a[2]*b[1],
            a[3]*b[1]-a[0]*b[2]+a[1]*b[3]+a[2]*b[0],
            a[3]*b[2]+a[0]*b[1]-a[1]*b[0]+a[2]*b[3],
            a[3]*b[3]-a[0]*b[0]-a[1]*b[1]-a[2]*b[2]};
        public static double[] Conjugate(double[] q)=>new[]{-q[0],-q[1],-q[2],q[3]};
        // World-frame rotation taking orientation a to orientation b.
        public static double AngleDegrees(double[] a,double[] b){double d=Math.Abs(a[0]*b[0]+a[1]*b[1]+a[2]*b[2]+a[3]*b[3]);return Deg(2*Math.Acos(Clamp(d)));}
        public static double AxisTiltFromVerticalDegrees(double[] a,double[] b)
        {
            var r=Multiply(b,Conjugate(a));double n=Math.Sqrt(r[0]*r[0]+r[1]*r[1]+r[2]*r[2]);
            return n<1e-9?double.NaN:Deg(Math.Acos(Clamp(Math.Abs(r[1])/n)));
        }
        public static double[] Mean(IReadOnlyList<double[]> v){var m=new double[3];foreach(var p in v){m[0]+=p[0];m[1]+=p[1];m[2]+=p[2];}for(int i=0;i<3;i++)m[i]/=Math.Max(1,v.Count);return m;}
        // Sign-aligned quaternion average; adequate for the small spreads of a resting tracker.
        public static double[] MeanRotation(IReadOnlyList<double[]> q)
        {
            if(q.Count==0)return new double[]{0,0,0,1};var s=new double[4];var r=q[0];
            foreach(var p in q){double sign=p[0]*r[0]+p[1]*r[1]+p[2]*r[2]+p[3]*r[3]<0?-1:1;for(int i=0;i<4;i++)s[i]+=sign*p[i];}
            return Normalize(s);
        }
        public static double MaxDeviation(IReadOnlyList<double[]> v,double[] mean){double m=0;foreach(var p in v)m=Math.Max(m,Distance(p,mean));return m;}
        public static double Rms(IReadOnlyList<double[]> v,double[] mean){double s=0;foreach(var p in v)s+=Sq(Distance(p,mean));return v.Count==0?double.NaN:Math.Sqrt(s/v.Count);}
    }
}
