# Class embodiment demo branch

This branch adds a **desktop simulation** for class. Open **Quest Prototype → Class Demo → Create or Open Demo Scene**, then press **Play** and select **Game**. The demo now defaults to **opposite-arm-only** mapping, front-facing avatars, and a full-width first-person mode with right-mouse look. Read [CLASS_DEMO.md](CLASS_DEMO.md) for controls and limitations. It does not complete or validate later Quest research milestones. The original Milestone 1 documentation follows.

# Quest head-tracking bootstrap — Milestone 1

Status: implementation/build verification is recorded in `VERIFICATION.md`. Quest hardware acceptance is **pending** until the researcher runs the checklist below. Do not start Milestone 2 on the strength of a successful build alone.

For a headset-free class presentation, follow [CLASS_DEMO.md](CLASS_DEMO.md). This shows the existing editor environment without claiming hardware verification.

## Scope and location

This is an independent project at `/Users/zhalaedaneshvari/VR-MirrorTherapy-Quest`.
The legacy `/Users/zhalaedaneshvari/VR-MirrorTherapy` repository is unchanged and tagged `legacy-baseline-2019.4.14f1` at `09a13524e945ef211d93f11402a095ef52016001`.

Only head tracking and a fixed reference environment are implemented. There are no hand-tracking, avatar, IK, mirroring, touch, experiment-control, or research-recording systems. Do not import legacy Oculus assets here.

## Open in Unity

1. Unity Hub → Projects → Add → Add project from disk.
2. Select `/Users/zhalaedaneshvari/VR-MirrorTherapy-Quest` (not the legacy directory).
3. Select **Unity 6000.6.2f1, Apple Silicon** and open it. Keep this exact editor version for the checkpoint.
4. Let packages import. Do not accept an editor upgrade or install optional XR samples.
5. In Project, double-click `Assets/Scenes/QuestBootstrap.unity`.
6. Open File → Build Profiles. Activate **Android** if it is not already active. This project uses the Android platform with Meta Quest Support enabled in OpenXR; a separate Meta Quest profile is not needed.
7. Run **Quest Prototype → Validate Milestone 1**. The Console should report zero OpenXR issues.

No XR setup is intended to be performed manually. If Unity asks about input handling, choose the new Input System and restart; the saved project already specifies it. In Unity → Settings → External Tools, use SDK, NDK, JDK and Gradle installed with this editor. Android Build Support, Android SDK & NDK Tools, and OpenJDK are required.

Editor Play mode on this Mac is a scene preview, not a substitute for a standalone Quest test. No desktop XR loader is enabled.

## Package rationale

Exact direct registry dependencies:

| Package | Version | Purpose |
|---|---|---|
| OpenXR | 1.18.0 | XR runtime, head device, stereo rendering, Meta Quest Support feature |
| Input System | 1.20.0 | Head position/rotation/tracking-state actions and standard Tracked Pose Driver |
| XR Core Utilities | 2.6.0 | Standard XR Origin and floor-origin handling |
| XR Management | 4.7.0 | Initialize the Android OpenXR loader at startup |

OpenXR/XR Management also resolve **XR Legacy Input Helpers 3.0.1**. This is a required Unity dependency, not Oculus Integration, and is not used for custom tracking code. Built-in audio, physics, and XR modules plus their transitive engine dependencies are included. Audio supplies the standard camera AudioListener; physics is also required by the XR dependency chain. No physics simulation or sound content is authored.

`Packages/manifest.json` and `Packages/packages-lock.json` are both included for version control. No XR Hands, XR Interaction Toolkit, Animation Rigging, Meta XR bundle, or Unity Meta OpenXR extension package is installed. The Meta Quest Support feature used here ships inside the OpenXR package itself.

## Configuration

- Editor: 6000.6.2f1; built-in rendering pipeline; unlit reference materials.
- Android APK; IL2CPP; ARM64 only; minimum Android API 32; target API 34.
- Application ID: `org.mirrortherapy.questprototype`.
- Display name: **Quest Head Tracking Test**, version 0.1.0 (code 1).
- GameActivity; Landscape Left; Vulkan; Linear colour; single-pass instanced/multiview; no depth submission.
- OpenXR initialized on startup. Meta Quest Support enabled, Quest 3 device selected.
- Oculus Touch interaction profile enabled to satisfy the provider's profile validation. It does not require connected controllers. There are no controller objects, controller action bindings, or menus in the application.
- XR Origin requests Floor mode, with a 1.6 m device-origin fallback. A fallback is **not** a passed floor calibration test: diagnostics warn unless Floor is reported.
- Head camera follows `<XRHMD>/centerEyePosition`, `centerEyeRotation`, and `trackingState`, with Update and BeforeRender updates.
- No locomotion or artificial camera movement. Camera clips at 0.05–30 m.
- A 10 m square visual floor with a 1 m grid, and blue/orange/green reference blocks at different depths. These virtual objects do not indicate safe physical walking space.
- The build helper creates a Development APK for diagnostic logs, using local debug signing. No store publishing, entitlement integration, or Meta App ID is involved.

OpenXR generates the VR/headtracking manifest declarations. No camera, microphone, hand/body-tracking, or storage permission is intentionally requested. Any manifest permissions actually present are listed in `VERIFICATION.md`.

## Connect Quest 3 to the Mac

1. Complete normal headset setup and pair it with the **Meta Horizon mobile app** on your phone.
2. If needed, complete Meta developer-account verification and create/join a developer organization using Meta's official device-setup instructions linked below.
3. In the phone app: Devices → your Quest 3 → Headset Settings → Developer Mode → enable. Navigation labels can vary by app version; use the official guide if the switch is absent.
4. Restart the headset if requested. Set up its normal boundary and floor level.
5. Connect it directly to your Mac using a **USB data cable**, not a charge-only cable.
6. Put on the headset. At **Allow USB debugging?**, select **Always allow from this computer**, then **Allow**. A file-access prompt is separate from USB debugging.
7. Do not start Quest Link/Air Link: this is a native Android APK running on the headset.

Optional connection check in Terminal:

```sh
/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb devices
```

Expect one serial with status `device`. `unauthorized` means accept the debugging prompt inside the headset. An empty list usually means cable/port/developer-mode setup. No Windows ADB driver is required on macOS.

## Build and install

### Unity route

1. Open this project and `QuestBootstrap` as described above.
2. File → Build Profiles → Android → Switch Platform if necessary.
3. Confirm the only enabled scene is `Assets/Scenes/QuestBootstrap.unity`.
4. Select your connected Quest under **Run Device**; use Refresh if needed.
5. Enable **Development Build** for this initial diagnostic test.
6. Choose **Build And Run**, saving to `Builds/QuestHeadTracking.apk` inside this project.
7. Wait for build and installation to finish, then wear the headset.

The **Quest Prototype → Build Milestone 1 APK** menu creates the same APK without installing it. Do not confuse that menu with Build And Run.

### Install the already-built APK (if supplied)

```sh
/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb install -r /Users/zhalaedaneshvari/VR-MirrorTherapy-Quest/Builds/QuestHeadTracking.apk
/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb shell am start -n org.mirrortherapy.questprototype/com.unity3d.player.UnityPlayerGameActivity
```

The OS may need a system interaction to dismiss a prompt. The application itself has no controller gate. You can relaunch it from the headset's app library under **Unknown Sources → Quest Head Tracking Test** (the library filter wording may change).

## Quest acceptance checklist — researcher must perform

Use a clear physical space and small comfortable movements; leave the headset's safety boundary enabled. Do not walk toward the virtual blocks.

- [ ] APK installation succeeds.
- [ ] App launches into immersive VR rather than a flat Android panel or black screen.
- [ ] Put controllers down before launching with Build And Run/ADB. The floor and reference blocks appear without pressing an application button.
- [ ] Rotate head left/right, up/down, and tilt gently: the world remains fixed rather than rotating with the head.
- [ ] Lean sideways 10–20 cm: the nearer block shifts relative to farther blocks (parallax).
- [ ] Lean forward/back and raise/lower the head: viewpoint changes accordingly; this must not be rotation-only tracking.
- [ ] Floor remains stable and reasonably aligned with the real floor; blocks do not swim or follow the head.
- [ ] Stay in the app for several minutes: no obvious flicker, stereo mismatch, repeated judder, or black frames.
- [ ] Briefly remove and re-wear the headset: rendering and head tracking resume. Note any OS prompt or change in origin.
- [ ] Open the system menu and return; tracking resumes without an application-controller requirement.
- [ ] Review device logs for crashes, OpenXR failures, and the diagnostic line reporting `displayRunning=True`, `headTracked=True`, `origin=Floor` after startup.

To capture logs immediately after testing (Ctrl+C stops a live capture):

```sh
/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb logcat -d > /private/tmp/quest-m1-device.log
```

Search that file for `[QuestBootstrap]`, `OpenXR`, `FATAL EXCEPTION`, and `AndroidRuntime`. Report the observed behavior plus relevant error lines; do not treat every OS log warning as an app failure. This script provides startup diagnostics and lifecycle notices only, not a complete record of every tracking interruption.

Report: headset OS version, APK used, test date, pass/fail for each item, whether controllers were absent, and any errors. Milestone 1 hardware acceptance remains pending until this report.

## Rebuild from command line

Close this project's Editor first:

```sh
/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath /Users/zhalaedaneshvari/VR-MirrorTherapy-Quest -buildTarget Android -executeMethod QuestProjectSetup.Build -logFile /private/tmp/quest-m1-build.log
```

`QuestProjectSetup.Configure` documents how settings and the initial scene were generated. It only creates the scene if missing; do not delete the authored scene to regenerate it casually. Ordinary development should use the saved scene/settings, Validate, and Build.

## Authoritative references checked 2026-09-23

- [Unity 6.6 OpenXR compatibility](https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.xr.openxr.html): 1.18.0 released.
- [Unity 6.6 XR Hands compatibility](https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.xr.hands.html): 1.9.0 released; not installed in this milestone.
- [Meta Unity setup](https://developers.meta.com/horizon/documentation/unity/unity-project-setup/): supported editor generation and Android modules.
- [OpenXR Meta Quest Support](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.18/manual/features/metaquest.html).
- [Meta API 34 targeting update](https://developers.meta.com/horizon/blog/meta-quest-apps-android-14-march-1/).
- [Meta device setup](https://developers.meta.com/horizon/documentation/native/android/mobile-device-setup/).

Unity 6.6 was selected explicitly at the user's request. Documentation compatibility and successful build checks do not establish Quest runtime correctness.
