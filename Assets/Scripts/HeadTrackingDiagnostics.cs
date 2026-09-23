using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

// Startup diagnostics and lifecycle notices only, not a research recorder. Never changes the camera pose.
public sealed class HeadTrackingDiagnostics : MonoBehaviour
{
    IEnumerator Start()
    {
        if (Application.isEditor)
        {
            Debug.Log("[QuestBootstrap] Editor preview is not a Quest head-tracking test. Build the Android APK.");
            yield break;
        }
        yield return new WaitForSecondsRealtime(3f);
        Report();
    }

    void OnApplicationFocus(bool focused)
    {
        Debug.Log($"[QuestBootstrap] Application focus: {focused}");
        // Focus can arrive before XR starts or resumes; do not diagnose XR in this callback.
    }

    void OnApplicationPause(bool paused) => Debug.Log($"[QuestBootstrap] Application paused: {paused}");

    void Report()
    {
        var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
        var displays = new List<XRDisplaySubsystem>();
        var inputs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(displays);
        SubsystemManager.GetSubsystems(inputs);
        bool displayRunning = displays.Exists(display => display.running);
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        bool tracked = head.TryGetFeatureValue(CommonUsages.isTracked, out bool value) && value;
        string mode = inputs.Count > 0 ? inputs[0].GetTrackingOriginMode().ToString() : "Unavailable";
        Debug.Log($"[QuestBootstrap] Loader={loader?.name ?? "None"}; displayRunning={displayRunning}; headTracked={tracked}; origin={mode}");
        if (loader == null || !displayRunning)
            Debug.LogError("[QuestBootstrap] XR is not running. Do not treat this launch as a passed test.");
        if (!tracked) Debug.LogWarning("[QuestBootstrap] Head tracking unavailable at this observation.");
        if (mode != TrackingOriginModeFlags.Floor.ToString())
            Debug.LogWarning("[QuestBootstrap] Floor origin not confirmed. Check headset floor setup before accepting the test.");
    }
}
