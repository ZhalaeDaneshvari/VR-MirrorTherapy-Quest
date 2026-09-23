# Milestone 1 verification — 2026-09-23

**Build verified; Quest hardware acceptance pending.** No headset runtime test was performed. No later milestones are implemented.

## Automatically verified

- Exact editor: Unity 6000.6.2f1 Apple Silicon; bundled Android SDK, NDK and OpenJDK.
- Package resolution and C# compilation succeeded.
- OpenXR Android validation: zero issues.
- Final batch-mode Android Development build: exit 0, Build Result Success.
- APK signature verifies with Android apksigner (v2 scheme).
- APK: `Builds/QuestHeadTracking.apk`, 31,834,542 bytes.
- SHA-256: `26b3f817606ffb5be36637cb7b6d16fc433694bce22a4a36810bfc625459c27a`.
- Packaged application ID `org.mirrortherapy.questprototype`; version 0.1.0 / code 1.
- APK declares minimum API 32, target/compile API 34; ARM64 only.
- Native OpenXR loader and Unity OpenXR libraries are packaged.
- Android manifest declares VR category, required head tracking, focus awareness, and Quest 3 (`eureka`).
- Saved scene uses XR Origin floor mode, Input System head position/rotation/tracking-state bindings, and before-render pose updates.
- Legacy Git working tree remains clean at `09a13524e945ef211d93f11402a095ef52016001`, with baseline tag `legacy-baseline-2019.4.14f1`.

## Actual APK permissions

- `android.permission.INTERNET`: development PlayerConnection support; no application networking code.
- `org.khronos.openxr.permission.OPENXR` and `org.khronos.openxr.permission.OPENXR_SYSTEM`: OpenXR runtime access declarations.
- `org.mirrortherapy.questprototype.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION`: application-scoped AndroidX receiver permission.
- No camera, microphone, hand/body tracking or storage permission is present.

## Diagnostics remaining

- Editor startup logged a licensing handshake error against an older running licensing client, then recovered using the editor's compatible client. Both builds completed successfully. No licensing workaround or bypass was used.
- The first native build emitted clang warnings: `'-x c++' after last input file has no effect [-Wunused-command-line-argument]`. The final incremental build did not repeat them. No authored C# compiler error or warning was found.
- Editor shutdown emitted thread-finalization messages and `debugger-agent: Unable to listen on 52`. These are host editor diagnostics after the successful build, not verified headset errors.
- No outstanding OpenXR validation issues. Runtime errors cannot be ruled out until the device test.

Local build logs: `/private/tmp/quest-m1-build.log` and `/private/tmp/quest-m1-build-final.log`. These are temporary machine-specific logs, not committed research records.

The researcher also ran validation in the Unity Editor and supplied a screenshot showing zero OpenXR issues on 2026-09-23. This is editor evidence only.

## Requires researcher testing

All checklist items in README: installation, immersive launch, controller-free entry, rotation, translation/parallax, floor/world stability, headset removal/resume, system-menu return, and device logs. Performance, tracking loss/recovery and floor-origin behavior are not established by compilation.

## Git checkpoint

This is a new independent repository on `codex/milestone-1`. The project and documentation are saved as separate commits at the user's request. Generated caches and APKs are ignored. `FILES.md` lists every source/configuration/documentation file, including Unity-generated metadata and settings.

Commit subjects:

- `feat: add standalone Quest 3 OpenXR head-tracking bootstrap`
- `docs: add setup, verification, and class demonstration guide`

After the hardware checklist passes, record the headset OS, test date, APK checksum and results before accepting Milestone 1. Do not start Milestone 2 automatically.
