# Milestone 2 — controller-free Quest 3 hands

**Historical M2 guide.** The user subsequently reported working hand tracking; see MILESTONE2_VERIFICATION.md for precise observations. This branch adds a diagnostic hand-tracking scene to the tested Milestone 1 head-tracking foundation. No avatar, arm IK, mirroring, touch, gestures, or research recorder is included.

## Where this work lives

- Branch: `codex/milestone-2-hands`, based on `codex/milestone-1`.
- Working folder: `/Users/zhalaedaneshvari/VR-MirrorTherapy-Quest-Hands`.
- This is a Git worktree of the modern repository, not a fork of the legacy project. The original modern folder remains on the class-demo branch.
- Legacy `/Users/zhalaedaneshvari/VR-MirrorTherapy` remains untouched.
- [Milestone 1 headset observations](MILESTONE1_HARDWARE_TEST.md) and [original setup instructions](MILESTONE1.md).

## What you should see

The same floor and blocks, plus cyan LEFT-hand joints and orange RIGHT-hand joints. Each skeleton includes the wrist, palm, and finger joints. A small head-relative text display reports each hand's availability and valid joint count. It is a diagnostic representation, not a realistic hand mesh.

Tracked input snapshots are separate from displayed transforms. This milestone uses a direct 1:1 display in the XR Origin's Camera Offset space. The skeleton is never used as the authoritative physical input.

## Open / validate / build

1. Unity Hub → Projects → Add project from disk → select `VR-MirrorTherapy-Quest-Hands`.
2. Open with **Unity 6000.6.2f1 Apple Silicon**, with its Android Build Support, SDK, NDK, and OpenJDK modules.
3. Open `Assets/Scenes/QuestHandsBootstrap.unity`.
4. File → Build Profiles → Android. The only enabled build scene should be QuestHandsBootstrap.
5. Quest Prototype → Milestone 2 → Validate Hands.
6. Quest Prototype → Milestone 2 → Build Hands APK builds `Builds/QuestHandTracking.apk`; alternatively use Build And Run with the connected Quest.

The separately named app **Quest Hand Tracking Test** uses `org.mirrortherapy.questprototype.hands`, version 0.2.0/code 2. The installed **Quest Head Tracking Test** is preserved. Do not use the old Milestone 1 build menu from this worktree: this branch's project configuration belongs to M2.

Mac Play mode is not a headset hand-tracking test; no simulated inputs are installed.

## Minimal package/configuration changes

- Added and pinned `com.unity.xr.hands` **1.9.0**, the released XR Hands package for Unity 6000.6.
- Retained OpenXR 1.18.0, Input System 1.20.0, XR Core Utilities 2.6.0, XR Management 4.7.0.
- XR Hands resolves Collections 6.6.0, Mathematics 1.4.0, Burst 2.0.0, Mono Cecil 1.11.6, and associated Unity test tooling transitively. Exact resolutions are in `Packages/packages-lock.json`; these are not separately chosen interaction frameworks.
- Enabled **Hand Tracking Subsystem** with automatic startup.
- Enabled **Hand Tracking Data Source** with tracked-hand preference for both sides. This is an optional OpenXR extension in the same package, not an added SDK. A preference is not a guarantee; reported controller-derived poses are rejected. If source reporting is unsupported, the display explicitly says `source not reported` and controller-free use must be checked on the headset.
- No Meta Aim, XR Interaction Toolkit, Unity Meta OpenXR, Meta XR SDK bundle, legacy Oculus Integration, or sample content imported.
- Retained Android IL2CPP/ARM64/Vulkan, minimum API 32/target 34, floor-origin head tracking.
- The XR Hands package build processor adds `com.oculus.permission.HAND_TRACKING` and the optional `oculus.software.handtracking` feature. It does not request raw camera images.

## Tracking behavior and limits

- Reads OpenXR `XRHandSubsystem` Dynamic and BeforeRender updates into preallocated, reusable snapshots.
- Joint poses use XR tracking-origin coordinates in metres, already converted to Unity conventions by the provider. Renderer children live under Camera Offset so origin/floor offsets apply exactly once.
- `isTracked`, per-joint `TryGetPose`, update flags, update phase, receipt time, and sequence are handled explicitly. Valid joint count is **not** a confidence score or proof of direct optical observation. The runtime may estimate occluded finger poses.
- Reported tracking loss, invalid joints, app pause/focus loss, or reported controller-derived data hides the corresponding geometry. A bone is shown only when both endpoint poses are valid.
- A 0.25-second monotonic receipt-time watchdog hides retained poses if updates stop. This is a diagnostic stale-data limit, not a validated clinical threshold. Data is never deliberately frozen indefinitely or filled with simulated movement.
- Briefly inferred poses supplied as valid by the runtime cannot be independently identified as occluded by this application.
- No elbow, shoulder, full-arm or body measurement is supplied by this milestone.
- Diagnostics log state changes and a five-second heartbeat. These are not crash-safe scientific research data or sensor-exposure timestamps.

## Headset test — one step at a time

Use a clear, normally lit space and keep the headset boundary enabled. Ensure hand tracking is enabled in the headset's system settings. Set controllers down and let the headset switch to hands.

1. Confirm the environment and text appear.
2. Hold both bare hands comfortably in front of the headset: cyan left and orange right skeletons should appear in corresponding locations.
3. Move each hand independently and turn each wrist; open and close fingers. Verify there is no left/right swap or obvious offset.
4. Move one hand out of view, then bring it back. Observe the status and disappearance/recovery. The runtime may briefly estimate a hidden hand; note how long this persists.
5. Briefly overlap hands or bring a hand near the forearm/body. Note jitter, incorrect fingers, or tracking loss; do not interpret these as automatically solvable by IK.
6. Remove/re-wear the headset and return from the system menu. Confirm head and hands recover without stale frozen geometry.
7. Review `[QuestHands]` logs. Confirm subsystem running, changing sample sequences, and available joints. Record whether source is reported as Unobstructed or not reported.

Do not declare M2 complete from build validation alone. Report which items passed and any loss/recovery issues. Avatar embodiment is the next separate milestone after reliable hand input is established.

## Files

- `Assets/Scripts/Hands/HandTrackingSource.cs`: provider snapshots, availability, lifecycle, freshness, and source reporting.
- `Assets/Scripts/Hands/HandSkeletonDisplay.cs`: direct diagnostic rendering from valid snapshots.
- `Assets/Scripts/Hands/HandTrackingStatus.cs`: visible status and bounded heartbeat logging.
- `Assets/Editor/QuestHandsSetup.cs`: reproducible setup, validation, and APK build.
- `Assets/Scenes/QuestHandsBootstrap.unity`, two hand materials, and their Unity metadata.
- XR settings, Android identity/build scene, package manifest and lock updated for M2.

## References

- [Unity 6.6 XR Hands compatibility](https://docs.unity.com/en-us/engine/6000.6/manual/packages-list/packages-all/pack-safe/com-unity-xr-hands).
- XR Hands 1.9.0 bundled documentation: `Documentation~/openxr-features/handtracking.md`, `handtrackingdatasource.md`, and `Documentation~/hand-data/xr-hand-access-data.md`. Package source and docs were inspected directly after installation.
