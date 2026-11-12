# Quest mirror-therapy research prototype

Current guided pilot: **0.13.1** (code 26) on the default branch **codex/milestone-1** (participant recording merged from `codex/participant-movement-recording`). App: **Quest Body Driven Avatar** (`org.mirrortherapy.questprototype.bodyavatar`). One-hand profiles and fixes since 0.12.0: [demo readiness](DEMO_READINESS.md).

**This branch (`clinician-tracker-pilot`)** adds a separate, optional [clinician-hand test app](#clinician-hand-test-app-branch-clinician-tracker-pilot) driven by a VIVE Ultimate Tracker. The pilot app, scene, package ID and participant pipeline described below keep their behaviour; the few hooks the test app uses in shared pilot scripts are off unless the test app switches them on.

**Participant recording:** select P01–P20 before the demo. Head, hand, estimated arm and displayed/mapped movement are recorded on the headset and copied over USB to **Desktop → Research Movement Data Demo**. Eye tracking is unavailable on Quest 3. See [movement data instructions](MOVEMENT_DATA.md) and [recording verification](MOVEMENT_DATA_VERIFICATION.md).

**Start with [the guided demo instructions](GUIDED_DEMO.md)** and [verification status](GUIDED_DEMO_VERIFICATION.md).

Welcome → participant number → hand profile (both / left only / right only) → five skin tones → seated/table calibration → normal familiarisation → automatic mirror movement → future-touch information → finish. Large head-gaze buttons use a visible two-second selection ring. Pinch selection and the previous manual-touch interaction are absent from this flow. The pilot itself has no clinician hand; see the test app below.

Open with Unity **6000.6.2f1**, Android support installed. Use **Quest Prototype → Body Avatar → Configure**, then **Build**. The environment/UI are created at runtime in `Assets/Scenes/QuestBodyDrivenAvatar.unity`. Build output is `Builds/QuestBodyDrivenAvatar.apk`.

The legacy project remains untouched. Hardware acceptance of this new guided flow is pending; earlier wearer feedback does not establish validation of this release. Body/table calibration is estimation and requires visual confirmation.

## Clinician-hand test app (branch `clinician-tracker-pilot`)

A second app, **Mirror Pilot Clinician Test** (`org.mirrortherapy.questprototype.bodyavatar.cliniciantest`), installs beside the pilot. It is the pilot flow plus a virtual clinician hand placed from one VIVE Ultimate Tracker worn by the clinician. Full procedure and every hardware result: [CLINICIAN_TRACKER_TEST.md](CLINICIAN_TRACKER_TEST.md). PC tool: [Tools/ClinicianTracker](Tools/ClinicianTracker/README.md).

- **How it connects:** Windows PC with SteamVR (run without a headset driver, so it keeps going while the Quest runs the standalone app) reads the tracker; `ClinicianTrackerProbe.exe stream` sends it to the Quest over the USB cable (`adb reverse`) and serves a **researcher dashboard** at `http://localhost:47801` (calibration state, clinician-hand visibility, participant number, stage, tracker/link health, an optional scrcpy view of the headset).
- **Session flow:** the headset opens on **Researcher / Participant**. In Researcher mode the researcher wears the tracker, calibrates (about 15 s of slow hand movement) and confirms alignment, then taps Done and hands the headset over. The calibration follows the room boundary through the handover. In Participant mode the participant sees the number the researcher set on the dashboard, chooses the **affected arm**, and does the normal setup; the unaffected arm is moved and mirrored onto the affected side. Select **Mirrored** on the dashboard after every app start (the clinician hand starts Off).
- **What the participant sees:** the clinician hand only in the mirror experience and the **Clinician touch** stage (elbow-to-wrist stroke on the unaffected forearm, shown on the affected side). Display-only aids, all logged: the clinician hand rests on top of the displayed forearm instead of sinking in (≤ 2.5 cm), the participant's resting arm is held for up to 3 s if the clinician's hand blocks hand tracking, and a nearly straight participant arm stays drawn (shoulder moves ≤ 3 cm).
- **Limits:** one tracker gives a rigid hand; fingers are a fixed open pose. Finger movement and physical contact are not tracked. Alignment accuracy after the handover and after app restarts is not yet measured well (one confirmation after an app restart was 6.3 cm; fresh calibrations 0.8–1.5 cm), so recalibrate in Researcher mode after each app start. Clinician data are logged separately (`ClinicianTrackerData/`) from participant `MovementData/`.
- **Build:** Unity **Quest Prototype → Clinician Tracker (test) → Build Test APK** → `Builds/QuestBodyAvatarClinicianTest.apk` (runs self-checks first; restores the pilot's player settings afterwards).

## Earlier implementation references


- [M1 head tracking](MILESTONE1.md), [headset observations](MILESTONE1_HARDWARE_TEST.md)
- [M2 hand-tracking guide](MILESTONE2.md), [verification](MILESTONE2_VERIFICATION.md)
- [M3 seated embodiment](MILESTONE3.md), [verification](MILESTONE3_VERIFICATION.md)


- [Body avatar](BODY_AVATAR.md), [body calibration](BODY_CALIBRATION.md)
- [Milestone 4 movement mapping](MILESTONE4.md)
- [Previous manual touch experiment](MANUAL_TOUCH_DEMO.md) — inactive in the guided release
- [Clinician-hand tracking plan](CLINICIAN_TRACKING_PLAN.md) — earlier feasibility options and the selected tracker route

Saved basic-pilot tags and historical branches remain available. Older guides describe their respective versions; use the guided instructions for the current app. APKs, caches, and local platform credentials are not committed.
