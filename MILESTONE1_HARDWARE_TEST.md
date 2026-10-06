# Milestone 1 hardware observation — 2026-10-06

Device: Meta Quest 3. OS build returned by `ro.build.display.id`: `UP1A.231005.007.A1` (Android build identifier, not necessarily the Meta marketing OS version).

Installed APK SHA-256: `26b3f817606ffb5be36637cb7b6d16fc433694bce22a4a36810bfc625459c27a`; size 31,834,542 bytes; application `org.mirrortherapy.questprototype`, version 0.1.0.

## Evidence

- ADB installation returned Success; activity launch command succeeded.
- The user confirmed the floor/blocks were visible and head rotation worked.
- The user confirmed head translation and world stability.
- The user confirmed tracking worked with controllers put down.
- The user confirmed headset removal and re-entry resumed the environment/tracking.
- The app process was still running when inspected. Its retained process log contained no `FATAL EXCEPTION`, `Fatal signal`, or `XR_ERROR` entries. Only a short pause/focus-loss interval remained; startup diagnostics were no longer retained.

## Limits

This is a user-observed basic functional pass. It does not establish long-duration performance, numerical pose accuracy, floor-mode startup diagnostics, controller-free cold-start behavior under every OS state, or systematic recovery under all interruptions. System-menu return was not separately confirmed. No claim of clinical validation or avatar embodiment is made.

Milestone 2 was explicitly requested by the user after these checks. The class demo remains a separate simulated prototype and is not the basis for real tracking development.
