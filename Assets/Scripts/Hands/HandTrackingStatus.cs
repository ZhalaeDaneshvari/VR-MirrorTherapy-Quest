using UnityEngine;
using UnityEngine.XR;

namespace QuestResearch.Hands
{
    // Human-readable diagnostics, not a scientific sample recorder.
    public sealed class HandTrackingStatus : MonoBehaviour
    {
        [SerializeField] HandTrackingSource source;
        [SerializeField] TextMesh label;
        double nextUpdate,nextHeartbeat;
        HandAvailability lastLeft=(HandAvailability)(-1),lastRight=(HandAvailability)(-1);
        public void Configure(HandTrackingSource input,TextMesh text){source=input;label=text;}
        void Update()
        {
            double now=Time.realtimeSinceStartupAsDouble;if(now<nextUpdate)return;nextUpdate=now+.2;
            if(source==null||label==null)return;
            string left=Describe(source.Left),right=Describe(source.Right);
            label.text="M2  HAND TRACKING\nLEFT (cyan): "+left+"\nRIGHT (orange): "+right+"\nPut controllers down. Hold hands in front.\nNo arms, mirroring, or touch in this test.";
            bool changed=source.Left.Availability!=lastLeft||source.Right.Availability!=lastRight;
            if(changed||now>=nextHeartbeat)
            {
                nextHeartbeat=now+5;lastLeft=source.Left.Availability;lastRight=source.Right.Availability;
                var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                bool tracked=head.TryGetFeatureValue(CommonUsages.isTracked,out bool value)&&value;
                Debug.Log($"[QuestHands] t={now:F3} subsystem={source.SubsystemRunning} headTracked={tracked} left={left} right={right} samples={source.Left.Sequence}/{source.Right.Sequence}");
            }
        }
        static string Describe(TrackingHandFrame hand)
        {
            string origin=hand.ReportedSource.HasValue?hand.ReportedSource.Value.ToString():"source not reported";
            return $"{hand.Availability} {hand.ValidJointCount}/26 ({origin})";
        }
    }
}
