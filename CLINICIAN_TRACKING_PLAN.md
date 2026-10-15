# Clinician-hand tracking: feasibility milestone

Status: investigation, not an implemented clinician detector. Updated 2026-10-15.

## Preserve the working pilot

The wearer accepted `6a6a37a` (0.9.0); tagged `basic-mirror-therapy-pilot-v0.9.0-tested`. The default resting-hand table-fit update is tagged `basic-mirror-therapy-pilot-v0.9.1`. Keep the installed pilot available. All clinician experiments use a separate scene/app ID and branch; do not enable multimodal input or add camera permissions in the accepted pilot as a side effect.

## What the first clinician milestone must prove

Track a separate clinician input while both participant hands remain correctly associated with the participant. Do not begin by rendering a convincing virtual clinician hand: first display independently labeled tracks and record identity errors, availability and timing. Standard XR Hands offers wearer left/right slots, not persistent clinician identity. A second person's hand entering view is not proof that it has been correctly classified.

## Candidate inputs and decisions

| Input | Useful capability | Missing evidence / cost |
|---|---|---|
| Quest RGB cameras + custom hand detection | Headset-only images; existing Meta APIs provide calibrated camera geometry and timestamps | A trained detector/runtime, persistent person assignment, metric depth, contact inference, occlusion handling and performance validation are still needed. Head-mounted view can lose hands during touch. |
| External calibrated camera(s) | Independent clinician view; can avoid a participant-facing camera being blocked by touch | Requires available camera hardware, world/body-frame registration, clock alignment, data transport and identity validation. A single RGB view alone does not establish reliable metric contact depth. |
| Clinician-held Quest controller | Separate tracked tool pose with an explicit clinician role | Not bare-hand tracking or sensed skin contact. Meta multimodal input conflicts with current body tracking, so requires a separate arm estimator and hardware validation. |

User preference/equipment question is pending. The prior requirement is automatic bare-hand clinician tracking; a controller proxy must not silently replace that requirement.

## Small next steps

1. **Acquisition diagnostic.** Choose the sensor route from available equipment. Camera route: show local camera feed, frame timestamp/age and calibration parameters in a separate diagnostic app; no recording by default. Keep wearer tracking active and measure whether it survives a clinician entering the view. Controller route: a separate app with no Body API and explicit clinician controller designation. Test loss/reacquisition and participant hand availability.
2. **Identity test.** Show participant-left/right and clinician track labels. Test entry from either side, overlapping/crossing hands, partial occlusion, lifting off the table and leaving/re-entering view. Record any ID swaps. Reject ambiguous attribution and suppress the clinician display; never assign a track solely because it is the third hand detected or because it is left/right handed.
3. **Metric alignment.** Register the sensor into the same floor/body frame and validate against known points on the actual tabletop. Camera pixel rays alone are not 3D contact. A table-plane intersection is valid only for points assumed to lie on the table, not an elevated finger touching an arm. Report position error and timestamp/latency distributions from measurements; do not set scientific acceptance thresholds without the protocol.
4. **Touch mapping diagnostic.** Separate raw clinician pose, source-side arm coordinates, reflected target, and rendered stimulus. Represent contact as unknown/possible/confirmed; proximity alone does not prove pressure/contact. Start with a marker rather than a full hand. Hide stale/ambiguous input rather than showing a plausible continuation.
5. **Integrated demonstration.** Only after the independent input passes, add an optional clinician mode to a new build and verify that the accepted movement mapping and calibration still pass. Preserve the original pilot APK and tag for rollback.

## Data needed for feasibility

Record acquisition timestamp and clock domain, host receipt time, sensor/frame sequence, identity assignment and confidence/ambiguity, raw position/orientation and coordinate frame, calibration revision, tracking validity, remapped displayed target, render time, and explicit inferred/manual/physically-sensed contact origin. Keep clinician data separate from wearer tracking and experimental condition. Do not fabricate numerical confidence where the provider exposes only validity.

## Verified sources and installed evidence

- Meta multimodal incompatibility with IOBT/FBS: https://developers.meta.com/vr/documentation/native/android/native-multimodal/
- Same restriction in installed Core 207.0.0: `Scripts/Editor/Movement/OVRProjectSetupMovementSDKConfigurationTasks.cs` ("Body API is not compatible with simultaneous hands and controllers").
- OpenXR hand selection: https://registry.khronos.org/OpenXR/specs/1.0/man/html/XrHandEXT.html
- Meta Camera API: https://developers.meta.com/vr/documentation/unity/unity-pca-documentation/ (current page updated Oct 7, 2026). MRUK PassthroughCameraAccess provides intrinsics, extrinsics and timestamps; raw camera access needs HEADSET_CAMERA permission. MRUK is not installed in this pilot. The sample editor versions are examples, not evidence that a new camera pipeline is tested in Unity 6000.6.2f1.
- A possible detector, not a complete clinician solution: https://developers.google.com/edge/mediapipe/solutions/vision/hand_landmarker . Landmark/handedness results do not establish person identity or physical contact. No detector/model/package has been installed or selected yet.
