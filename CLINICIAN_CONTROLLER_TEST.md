# Clinician controller feasibility test

This is a separate diagnostic, not a new pilot or validated clinician tracker.

## Why a separate app

Meta XR Core 207 and Meta's multimodal documentation say simultaneous hands/controllers is incompatible with Inside-Out Body Tracking and Full Body Synthesis. The accepted pilot uses the Body API. This diagnostic therefore has no body estimator, arm avatar, table fitting, movement mirroring, touch detection or participant recorder. It reuses the existing XR Hands source and skeleton renderer without changing them. Controller-derived hand poses are rejected by that source; an unknown source is explicitly displayed as unknown and is not proof of wearer identity.

App: **Quest Clinician Controller Test**
Package: `org.mirrortherapy.questprototype.controllerprobe`
APK: `Builds/QuestClinicianControllerProbe.apk`
Scene: `Assets/Scenes/QuestClinicianControllerProbe.unity`

The original **Quest Body Driven Avatar** app and its recordings remain separate. No new packages are installed. Run this test natively on Quest, not through Quest Link.

## Build on the development Mac

Close this project in Unity. From its root run:

```sh
python3 Tools/build_controller_probe.py
```

The wrapper requires the pinned Unity 6000.6.2f1 Mac editor and snapshots existing ProjectSettings plus XR/Oculus configuration, scenes and materials (including pre-existing local changes). It prints the local recovery directory and restores those files byte-for-byte even if Unity exits with a build error. Do not run another editor/build concurrently. If the wrapper itself is force-killed, recover from that printed backup before opening the pilot. Use this wrapper rather than calling the build entry point directly.

Build log: `/private/tmp/controller-probe-build.log`.
The probe's generated scene/material are retained on first build. Probe OpenXR settings are configured only during the isolated build, so merely opening that scene with the pilot's restored settings is not a valid multimodal test.

## Device test: participant and a second person

1. Keep the original pilot installed. Install the new APK with `adb install -r Builds/QuestClinicianControllerProbe.apk` and open **Quest Clinician Controller Test** under Unknown Sources.
2. Participant wears the Quest and leaves BOTH hands empty. Put the left controller aside. Clinician holds the RIGHT controller, with its wrist strap secured. This explicit right-controller assignment never switches automatically to the other controller.
3. Check the blue LEFT and orange RIGHT participant skeletons. Move each finger individually and confirm the correct skeleton follows. These colors label provider slots, not independently verified person identity.
4. Bring the right controller into view. A purple elongated cube should follow its grip pose. It is a controller marker, not a calibrated fingertip/contact point. Status shows controller layout, input-update age, and each hand's availability/source.
5. Participant keeps both hands still. Clinician moves the controller slowly above and alongside each forearm. Watch for participant skeletons disappearing, following clinician motion, or switching identity. Repeat with clinician approaching from either side. Start without touching.
6. Participant moves fingers while clinician holds controller still, then both move independently. Verify neither input drives the other. Do not infer success merely from all markers being visible.
7. Move controller outside reliable tracking, then return. Invalid, stale (>150 ms), unfocused or ambiguous controller poses must hide. Returning valid tracking should restore the marker. Repeat after opening/closing the Quest system menu.
8. End the diagnostic and reopen the original pilot. Confirm normal calibration and mirroring remain unchanged.

Record observations manually: device/OS version, controller type, each step, any missing hands/identity switches/jumps, and whether normal or detached controller layout was reported. No pose files or participant data are recorded by this diagnostic. Software availability alone does not establish spatial accuracy or latency.

## Coordinate and validity contract

- Input System XRController grip pose, Unity tracking-origin coordinates, metres and quaternion rotation. Marker transform uses the same XR Origin floor-offset transform as the hand skeletons.
- Only right-side Touch controller layouts are considered, including the installed detached Touch Plus profile. No `XRNode.RightHand`, active-hand proxy or pose fallback is used.
- Require tracked position AND rotation, finite pose, plausible quaternion magnitude, input update age <=150 ms, application focus and successful multimodal enable request. If more than one right-controller candidate is valid, hide instead of guessing.
- Input update age is `InputState.currentTime - device.lastUpdateTime`; it is NOT optical capture latency. Provider timestamps and person identity are not available through this test.
- An accepted enable request is not proof of simultaneous runtime tracking. Observe separate hands/controller poses on the headset.

## Acceptance and next decision

Hardware acceptance is pending. Compile/build and validity-gate tests are not evidence that a clinician-held controller preserves wearer identity. If hand tracking is displaced or misassigned, this route fails the current requirement.

If acquisition works, the next design decision is an explicit alternative arm estimator for the clinician mode. Do not enable multimodal in the body-driven pilot or silently downgrade its calibrated arms. Only afterward investigate a controller-to-hand offset, body-centred clinician reflection, contact semantics and logging. A controller does not measure fingers, skin contact or force.

Official reference checked 11 November 2026:
https://developers.meta.com/vr/documentation/unity/unity-multimodal/

Installed evidence: `com.meta.xr.sdk.core` 207.0.0, `OVRProjectSetupMovementSDKConfigurationTasks.cs` (Body API incompatibility), `MetaQuestTouchPlusDetachedControllerProfile.cs` (dedicated detached controller device).

## Build verification (11 November 2026)

Unity 6000.6.2f1 Android build succeeded. Focus, tracked-state, position/rotation validity, stale-update and nonfinite-pose gate checks passed. APK manifest inspection confirmed the separate package/name, GameActivity entry point and hand-tracking feature. Original pilot settings/assets were restored byte-for-byte. No headset was attached to this Mac, so installation and all two-person tests remain pending.

Meta's setup checker still reports a GameActivity requirement warning, although the built APK's launchable activity is `com.unity3d.player.UnityPlayerGameActivity`; its check reads the editor manifest rather than establishing the final APK entry point. Recommended SDK setup notices also remain. These are not a substitute for on-headset testing. No new C# compiler warnings occurred in the final rebuild.
