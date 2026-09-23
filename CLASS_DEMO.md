# Class demonstration — Milestone 1

**Unity Editor environment preview. Quest 3 hardware testing is pending.**

## Two-minute demonstration

1. Open this project with Unity 6000.6.2f1 and open `Assets/Scenes/QuestBootstrap.unity`.
2. Stay in the Scene tab; Play mode is not required.
3. Select World References in the Hierarchy, move the pointer over the Scene view, and press F to frame the environment.
4. Show the floor grid and three colored blocks. These are fixed references for testing head rotation, translation and depth/parallax on the headset.
5. Expand XR Origin to show Camera Offset and Head Camera. Explain that OpenXR will supply the headset pose when running on Quest.
6. Choose Quest Prototype → Validate Milestone 1, then show the Console result with zero OpenXR validation issues.
7. Show README and the two Git commits to explain the separate modern project and reproducible configuration.

Do not move scene objects or change camera transforms for this demonstration. Scene-view navigation changes the editor view, not the headset tracking. The colored arrows on a selected object are editing handles, not VR content.

## Suggested explanation

“I am modernizing a legacy VR mirror-therapy project into a standalone Quest 3 research prototype. I preserved the legacy repository and created a separate Unity 6/OpenXR project. This first milestone establishes the headset tracking foundation: a stable floor and objects at different depths. The Android app builds and configuration validation passes. Today I am showing the editor environment; actual headset tracking and controller-free launch still need hardware testing.”

“The future research manipulation will map movement from one physical arm to the opposite virtual arm. Hands, avatar embodiment, mirroring, and visual touch are planned later; they are not implemented in this demonstration.”

## What can be claimed today

- Separate modern Unity project and preserved legacy project.
- Minimal OpenXR head-tracking scene configured for Quest 3.
- Successful Android build and OpenXR configuration validation.
- User-confirmed editor validation screenshot: zero issues, 2026-09-23.

## What cannot yet be claimed

- Successful installation or launch on a Quest 3.
- Verified 6DoF tracking, floor alignment, resume behavior, or performance on hardware.
- Implemented hand tracking, avatar, mirroring, touch, or therapeutic effectiveness.

Milestone 2 remains on hold pending the Quest 3 acceptance test.
