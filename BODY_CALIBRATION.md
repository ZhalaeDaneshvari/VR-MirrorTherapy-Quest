# Body calibration investigation

Status: research and proposed validation plan, 2026-10-06. No body-tracking SDK has been installed, no new APK has been built, and personalized arm fitting has not passed headset testing.

## Why the current arms are not accepted

The participant reported hands blending into the table and arms bowing outward. The existing renderer uses assumed shoulder locations and segment lengths, analytic two-bone IK, and procedural arm surfaces. Passing transformation/mesh tests does not establish anatomical fit. Table-support correction was disabled on the device after the failed visual test. Preserve the user's approximately 88 cm table setting as a visual preference, not a measured physical height. Legacy code remains untouched.

## Separate four calibration problems

1. Tracking: where the runtime estimates head, hands, shoulders and elbows are, including validity and source.
2. Body dimensions: participant height, shoulder placement, upper-arm/forearm length and circumference. Height alone cannot uniquely determine these.
3. Avatar: rig, skin weights, wrist alignment and mesh proportions. A shape scan could inform appearance but does not supply live joint motion.
4. Environment: floor, body midline and physical table plane. Never alter raw tracked wrists to conceal an incorrect table height.

## Findings from primary sources

Meta exposes body calibration with an explicit height override and a runtime calibration state. Startup while seated can impair automatic height estimation. Camera-assisted IOBT is requested rather than guaranteed; the app must check actual fidelity and treat unavailable estimates explicitly. This is a candidate replacement for our assumed elbow placement, not a personal surface scanner or ground-truth measurement system. Retargeting and skin weights still require validation. [Meta body tracking documentation](https://developers.meta.com/vr/documentation/unity/move-body-tracking/).

The managed OVRBody interface offers SuggestBodyTrackingCalibrationOverride(height), ResetBodyTrackingCalibration(), GetBodyTrackingCalibrationStatus() and GetBodyTrackingFidelityStatus(). Exact signatures must be checked against the installed release, rather than copying old native examples. [Meta OVRBody API](https://developers.meta.com/vr/reference/unity/v81/class_o_v_r_body/).

Fitted Avatars demonstrates personalized skeleton calibration using additional tracked devices and calibration movements. Its result does not prove equivalent accuracy from Quest head and hands alone. It supports separating segment fitting from uniform avatar scaling. [Research paper](https://arxiv.org/abs/2307.09558).

The Unity Movement sample offers body retargeting and character setup. Only features needed for upper-body tracking should be adopted; networking, face tracking, synthesized standing poses and generative legs are outside this prototype. [Official sample](https://github.com/oculus-samples/Unity-Movement).

Unity Meta OpenXR 2.6 describes AR Foundation and other platform features but does not list a body tracking provider. Do not add it merely to obtain body tracking. [Unity package documentation](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.6/manual/index.html).

## Dependency feasibility

Read from the official Unity package registry on 2026-10-06: com.meta.xr.sdk.core latest is 207.0.0, requiring Unity 6000.0.66f2. Installed Unity 6000.6.2f1 meets that declared minimum. The package declares XR Hands 1.7.2 and built-in UI, audio, IMGUI, image conversion and web-request modules. Our project explicitly uses XR Hands 1.9.0 and OpenXR 1.18.0. Meeting a minimum editor version is not evidence that this exact combination has compiled or run correctly.

Next dependency experiment: pin Meta Core 207.0.0 on this isolated branch, inspect its body API and OpenXR setup, and implement a minimal diagnostic body provider before considering the larger Movement retargeting package. Do not install Interaction SDK unless a demonstrated feature requires it. Retain a single XR origin and verify coordinate conversion; do not let competing camera rigs drive the view.

Registry: https://packages.unity.com/com.meta.xr.sdk.core

Local blocker: only approximately 635 MiB free at inspection. Do not start SDK import/Android build with this headroom. Request at least 10 GB free as practical working headroom (engineering estimate, not a vendor minimum). No personal files should be deleted automatically to make room.

## Small next hardware experiment

Create a separate diagnostic scene/application, preserving the current APK and settings. Show hand joints and body shoulder/elbow/wrist markers before skinning an avatar. Request body permission, verify support and actual fidelity, and show calibration state, joint validity and sample age. Record monotonic sample timing and provider identity. Permission denial, tracking loss, application resume and origin reset must invalidate affected output rather than silently freeze it.

Compare estimated elbow and shoulder positions while seated, hands above the table, and forearms resting on it. Test independently on both sides and with brief occlusion. Check whether body and XR Hands wrists agree in the same coordinate frame. Quantify repeatability against measured references when possible; a visually pleasing result alone is insufficient. Do not present body estimates as directly measured anatomical joint centres.

Do not proceed to avatar integration if the provider still produces unstable or implausible arms. Investigate external tracked references if it fails. No promise of exact arm matching is justified yet.

## Proposed participant fitting workflow

- Enter known standing height for the body provider; do not substitute seated head height. Do not require standing or a painful extended-arm pose to calibrate.
- Use comfortable seated posture and a helper where needed. Measure shoulder width and arm segment lengths if estimates cannot be validated. Circumference/appearance fitting is separate and optional.
- Calibrate floor/body orientation and table independently. Use a measured table height or a known tracked contact reference with its offset accounted for. Multiple surface samples can estimate a plane, but the optical hand joint centre is not the skin contact point.
- Compare raw tracking, fitted targets and visible skin. Save fitting values with units, source (measured/estimated/manual), timestamp and calibration revision. No automatic trial-time rescaling.
- Use a continuous, properly weighted rigged upper-body mesh, including wrist twist deformation; validate close-up first-person appearance and limb proportions.
- Recheck after recentering or moving the chair/table. Preserve prior settings for rollback.

For glass, test tracking and plane alignment separately; do not assume every overlap comes from glass or fix it by raising the hands. Any optional display correction must be explicit, bounded, reversible and logged separately from physical tracking and experimental mirroring.

## Research boundaries

Physical tracking -> body/hand source frames -> explicit movement mapping -> fitted avatar targets -> renderer remains the data flow. Calibration must not erase left/right source identity or the distinction between recorded physical and displayed poses.

Automatic clinician-hand tracking remains essential but unresolved. Participant body tracking does not establish reliable identification of an additional person's hands or actual physical contact. It needs a separate sensing and identity/occlusion experiment. A scanned arm mesh would not solve that problem either.

## Acceptance checkpoints

1. SDK probe compiles and reports supported/unsupported, permission, calibration and actual fidelity on Quest. Commit provider plus evidence.
2. Seated marker test establishes whether the estimates improve on current IK; document failures as well as successes.
3. Personalized rig fitting passes first-person wrist continuity, elbow placement and table-rest tests without corrupting raw tracking.
4. Reintroduce mirrored conditions only after normal embodiment passes; log calibration and mapping separately.

No step is currently marked hardware-passed.
