# Guided demo verification — 0.11.0 (Android code 22)

Date: 2026-10-19. Unity 6000.6.2f1, Android IL2CPP ARM64.

## Verified automatically

- Unity configuration and Android APK build completed successfully.
- Existing body-frame reflection, arm fitting, tabletop continuity/hysteresis, invalid-data and synthetic arm/hand clearance checks passed.
- All 15 visible guided pages rendered; alignment deliberately has no visible UI.
- UI overlay remained readable with scene geometry in front of it.
- Dwell activates once while held, requires gaze exit across page transitions (including a different button), and rearms after exit.
- Normal activity has two 10-second prompts; mirror activity has six. Skip reaches the next prompt/completion; invalid or long-stall deltas do not advance the clock. Pausing is implemented by withholding advancement while outside the Running stage.
- Table entry range/unit conversion checks passed.
- Welcome, skin-choice, table, fit and compact activity renders were visually reviewed. Three example renders are in `Docs/GuidedDemo`.
- The live flow contains no construction of `ForearmTouchStroke` or `ManualTouchCommands`; those historical implementations are not active.
- No XR package changes. Existing unrelated local XR editor/Standalone settings and platform credentials were excluded from commits.

## Device deployment

- Quest 3 was visible as an authorized ADB device.
- `adb install -r` succeeded without clearing app data.
- Package manager reported `versionName=0.11.0`, `versionCode=22`.
- Launch command succeeded; the app process existed afterward.
- At the first capture, the app had lost focus and its surface was stopped. Screen capture returned no image. This does **not** establish that the wearer has seen or accepted the new flow.

APK SHA-256:
`5828361040dc19679ae06e8231370d22989fc3a4cd7f35001d71fcd05784a0e8`

## Remaining warnings / limits

- Existing editor-only `BodyTrackingProbe.cs` unreachable-code warning (`CS0162`) follows the non-device early return. No C# compilation errors.
- Unity licensing emitted an access-token refresh message; entitlement/build nevertheless completed successfully.
- Quest startup logged a missing Google Play `AssetPackManager` class and Meta/OpenXR function lookup warnings for optional scene/space APIs. Initialization continued afterward; no fatal exception or managed null-reference exception was found in the captured startup log. These messages are recorded, not presented as proof of an entirely clean runtime.
- Wearer confirmation remains necessary for readability, dwell usability, all five skin tones on both arms/hands, table fit, mapping transitions, performance and pause/recovery behavior. Follow the acceptance checklist in `GUIDED_DEMO.md`.
- No claim is made that the activity sequence is a validated clinical protocol. No clinician sensing or haptic-glove integration exists in this release.
