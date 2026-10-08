# Seated body-tracking diagnostic

This is a separate diagnostic application, not a corrected avatar or a completed research milestone. The existing mirror application and its table calibration remain installed separately. The legacy repository is not involved.

## Purpose and dependencies

Compare Meta's estimated upper-body joints with the existing OpenXR XR Hands wrist poses before adopting them for avatar fitting. Meta Core 207.0.0 is pinned; Unity stays at 6000.6.2f1, OpenXR at 1.18.0 and XR Hands at 1.9.0. The SDK additionally requires built-in uGUI 2.6.0 and Asset Bundle 1.0.0 to compile in this deliberately minimal project; its declared dependencies alone produced missing-type errors. No Movement, Interaction SDK or legacy Oculus Integration was imported. The SDK itself generates an Assets/Oculus configuration directory; this is modern package configuration, not copied legacy code.

## Build and launch

Use the active `codex/body-calibration-investigation` branch in the modern Hands worktree. In Unity use **Quest Prototype > Body Calibration > Configure Probe**, then **Build Probe APK**. The configure command selects only the probe scene and sets the separate application identifier `org.mirrortherapy.questprototype.bodyprobe`. APK: `Builds/QuestBodyCalibrationProbe.apk`.

On Quest open **Quest Body Calibration Probe** from Apps / Unknown Sources. Allow its body-tracking permission prompt. Sit comfortably and look down at your hands. Green spheres are estimated shoulder/upper-arm/elbow/wrist locations; cyan and orange lines show independently sourced left/right XR hand joints. There is no skinned avatar or calibrated virtual table in this diagnostic.

The panel reports calibration state, actual fidelity, whole-body confidence, body sample age and differences between the two providers' wrist positions. These differences are diagnostic discrepancies, not errors against anatomical ground truth. Samples are collected at different runtime update times, so moving quickly can increase discrepancies.

No standing height is assumed. To provide a known standing height, write `body-calibration.json` in the application's persistent files directory, containing `{"standingHeightMetres":1.70}` with the participant's actual value, then restart the application. 1.70 is a format example, not a default or a recommendation. The panel distinguishes automatic calibration from an accepted/rejected override. Acceptance by the API does not establish physical accuracy. Never substitute seated headset height or require uncomfortable movements to calibrate.

## Hardware checks

1. Confirm head movement and both hand skeletons still work.
2. Read calibration and actual fidelity. High fidelity was requested, not guaranteed. Record Low or unavailable results honestly.
3. With forearms resting comfortably, inspect whether green elbow markers are near your real elbows. Test each side separately and small comfortable movements above the table.
4. Compare body wrist spheres and XR hand wrists. Note persistent offsets, visible jumps, oscillations or inaccurate elbow position.
5. Briefly hide a hand and bring it back; check that unavailable body poses disappear instead of freezing. Runtime estimation may continue through occlusion; valid does not mean directly observed.
6. Remove/re-don the headset and recenter. The source must invalidate old poses and restart after focus/pause/origin changes.
7. Repeat before accepting this provider for fitting. Do not claim clinician-hand tracking or touch/contact detection from this test.

## Diagnostic records

`body-probe-<UTC>.jsonl` is created in persistentDataPath. Metadata identifies source, version, coordinate space and clock interpretation. Roughly 10 samples/second contain monotonic receipt time, separate provider time, validity, raw location flags, calibration/fidelity, skeleton and origin revision, body poses and XR hand wrist poses/times. Body arrays are ordered: left shoulder, left upper-arm joint, left lower-arm joint (elbow), left wrist, then the same four on the right. Invalid poses must be ignored, even if their array slots contain a previous value. Age -1 means no received body sample.

Each line is flushed through StreamWriter; power loss can still lose OS-buffered writes or leave a partial last line. A reader must tolerate an incomplete last line. This is an engineering diagnostic, not the final scientific dataset or synchronized exposure timestamps. File-write failure is shown on the panel and logged. Records stay on the headset unless explicitly retrieved; do not commit participant diagnostic records to GitHub.

## Implementation

- `BodyTrackingProbe`: owns only the body service, permission request, validity, calibration and Unity tracking-space conversion. It does not instantiate OVRBody/OVRManager or a second camera rig.
- `BodyProbeDisplay`: renders diagnostic markers and records diagnostic samples; does not modify source poses.
- `QuestBodyProbeSetup`: editor setup and explicit separate APK build.

The pinned SDK's `FromFlippedZVector3f`/`FromFlippedZQuatf` conversion is a coordinate-handedness conversion, not the experimental sagittal reflection. Both providers are displayed under the existing XR camera offset. Actual cross-provider spatial alignment still needs headset validation.
