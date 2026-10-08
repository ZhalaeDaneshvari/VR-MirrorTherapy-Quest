using System;
using System.IO;
using QuestResearch.Hands;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace QuestResearch.BodyCalibration
{
    // Lightweight diagnostic, deliberately separate from the experimental avatar and logger.
    public sealed class BodyProbeDisplay : MonoBehaviour
    {
        [SerializeField] BodyTrackingProbe source;
        [SerializeField] HandTrackingSource hands;
        [SerializeField] Transform trackingSpace;
        [SerializeField] TextMesh label;
        [SerializeField] Material markerMaterial;
        readonly Transform[] markers=new Transform[8];
        StreamWriter writer;
        double nextSample;
        string fileStatus="";
        [Serializable] sealed class Sample
        {
            public string type="sample",status,calibration,fidelity,height;
            public double receiptSeconds,bodyProviderSeconds,bodyAgeSeconds;
            public uint skeletonRevision;
            public int originRevision;
            public float confidence;
            public bool supported,permission;
            public Pose[] bodyPoses;
            public bool[] bodyValid;
            public int[] bodyFlags;
            public Pose leftHandWrist,rightHandWrist;
            public bool leftHandValid,rightHandValid;
            public string leftHandStatus,rightHandStatus;
            public double leftHandReceipt,rightHandReceipt;
        }
        public void Configure(BodyTrackingProbe body,HandTrackingSource handSource,Transform space,TextMesh text,Material material)
        {source=body;hands=handSource;trackingSpace=space;label=text;markerMaterial=material;}
        void Start()
        {
            for(int i=0;i<markers.Length;i++)
            {
                var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name=BodyTrackingProbe.JointIds[i].ToString();Destroy(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(trackingSpace,false);sphere.transform.localScale=Vector3.one*.035f;
                sphere.GetComponent<Renderer>().sharedMaterial=markerMaterial;markers[i]=sphere.transform;sphere.SetActive(false);
            }
            try
            {
                string name="body-probe-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+".jsonl";
                writer=new StreamWriter(Path.Combine(Application.persistentDataPath,name),false){AutoFlush=true};
                writer.WriteLine("{\"type\":\"metadata\",\"schema\":1,\"provider\":\"Meta Core 207.0.0 OVRPlugin upper body\",\"space\":\"Unity tracking origin, metres\",\"clock\":\"receipt monotonic seconds; provider clock separate\",\"appVersion\":\""+Application.version+"\",\"utc\":\""+DateTime.UtcNow.ToString("O")+"\"}");
                fileStatus="Diagnostic recording (10 Hz)";
            }
            catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException){LogFailure(ex);}
        }
        void LateUpdate()
        {
            for(int i=0;i<markers.Length;i++)
            {
                markers[i].gameObject.SetActive(source.Valid[i]);
                if(source.Valid[i])markers[i].SetLocalPositionAndRotation(source.Poses[i].position,source.Poses[i].rotation);
            }
            double now=Time.realtimeSinceStartupAsDouble;
            if(now<nextSample)return;nextSample=now+.1;
            int wrist=XRHandJointID.Wrist.ToIndex();
            bool left=hands.Left.JointValid[wrist],right=hands.Right.JointValid[wrist];
            float dl=left&&source.Valid[3]?Vector3.Distance(hands.Left.JointPoses[wrist].position,source.Poses[3].position):float.NaN;
            float dr=right&&source.Valid[7]?Vector3.Distance(hands.Right.JointPoses[wrist].position,source.Poses[7].position):float.NaN;
            double age=double.IsNegativeInfinity(source.ReceivedAt)?-1:now-source.ReceivedAt;
            label.text="BODY CALIBRATION PROBE\n"+source.Status+"\nCalibration: "+source.Calibration+" | Fidelity: "+source.Fidelity+
                $"\nBody confidence: {source.Confidence:F2} | age: {age:F2}s\n"+source.HeightStatus+
                $"\nWrist difference L/R: {dl*100:F1} / {dr*100:F1} cm\nGreen = body estimates; cyan/orange = XR hands\n"+fileStatus;
            if(writer==null)return;
            var sample=new Sample{receiptSeconds=now,bodyProviderSeconds=source.ProviderTime,bodyAgeSeconds=age,
                status=source.Status,calibration=source.Calibration,fidelity=source.Fidelity,height=source.HeightStatus,
                supported=source.Supported,permission=source.PermissionGranted,confidence=source.Confidence,
                skeletonRevision=source.SkeletonRevision,originRevision=source.OriginRevision,
                bodyPoses=source.Poses,bodyValid=source.Valid,bodyFlags=source.LocationFlags,
                leftHandValid=left,rightHandValid=right,leftHandWrist=left?hands.Left.JointPoses[wrist]:default,
                rightHandWrist=right?hands.Right.JointPoses[wrist]:default,
                leftHandStatus=hands.Left.Availability.ToString(),rightHandStatus=hands.Right.Availability.ToString(),
                leftHandReceipt=double.IsNegativeInfinity(hands.Left.ReceivedAt)?-1:hands.Left.ReceivedAt,
                rightHandReceipt=double.IsNegativeInfinity(hands.Right.ReceivedAt)?-1:hands.Right.ReceivedAt};
            try{writer.WriteLine(JsonUtility.ToJson(sample));}catch(IOException ex){LogFailure(ex);}
        }
        void LogFailure(Exception ex){fileStatus="RECORDING FAILED: see device log";Debug.LogError("[BodyProbe] "+ex.Message);writer?.Dispose();writer=null;}
        void OnDestroy(){writer?.Dispose();}
    }
}
