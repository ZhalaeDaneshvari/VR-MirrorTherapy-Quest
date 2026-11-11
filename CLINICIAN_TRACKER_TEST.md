# Clinician tracker test build (branch `clinician-tracker-pilot`)

A separate, optional test of a virtual clinician hand driven by one VIVE Ultimate Tracker. It is **not** the
pilot:

- The pilot scene (`QuestBodyDrivenAvatar.unity`), its package ID `org.mirrortherapy.questprototype.bodyavatar`,
  participant mirroring, calibration and movement recording are unchanged.
- The test scene `Assets/Scenes/QuestBodyAvatarClinicianTest.unity` is regenerated from the current pilot scene
  plus the clinician feature (ignored by Git; recreate it from the menu).
- The test APK installs **beside** the pilot as `org.mirrortherapy.questprototype.bodyavatar.cliniciantest`
  ("Mirror Pilot Clinician Test"). Uninstalling it never touches the pilot.
- The clinician hand is **off by default**: nothing is shown until the researcher calibrates, confirms
  alignment and selects a mode.

## What it does and does not do

- One tracker gives a **rigid** hand pose. Fingers use the hand model's fixed bind pose (open hand). Finger
  movement and physical contact are **not** tracked or detected.
- The clinician hand is placed only from the tracker (identified by serial number) and the explicit
  calibration. Participant hand tracking is read only during calibration and alignment confirmation, never
  to place the clinician hand, so a participant hand cannot be mistaken for the clinician.
- Modes: **Off**, **Congruent** (where the tracker is) and **Reflected** (mirrored across the participant's
  calibrated body midline with the pilot's `MovementReflection.Reflect`, drawn as the opposite hand). Reflected
  mode stays hidden when the participant midline is not calibrated; it never falls back to congruent.
- Hidden whenever: mode off, PC link down, stream older than 100 ms, tracker not `Running_OK`, alignment not
  confirmed, headset not tracking/worn.
- Logged on the headset under `ClinicianTrackerData/` in the app's files folder, separate from participant
  `MovementData/`: the raw tracker pose exactly as streamed (OpenVR frame), the calibrated tracker and wrist
  poses (Quest space), the displayed wrist pose, validity, hidden reason, mode, calibration revision, PC and
  Quest timestamps, stream age and round-trip time. The palm-to-displayed-forearm distance is display
  geometry only, not contact. The PC probe logs its own raw copy under `logs/`.

## Equipment and connection

The Windows PC runs SteamVR + VIVE Hub with the tracker, and the probe in `stream` mode. The Quest connects
to the PC **over the USB cable** (`adb reverse tcp:47800`), so lab Wi-Fi is not involved. Whether SteamVR
keeps the tracker running while the Quest is in its standalone app (not Quest Link) is part of what this
test checks; HTC documents PC use with a SteamVR headset connected.

## Procedure

1. Build: Unity 6000.6.2f1 → **Quest Prototype → Clinician Tracker (test) → Build Test APK**
   (`Builds/QuestBodyAvatarClinicianTest.apk`). It runs the math self-checks first.
2. Install beside the pilot: `adb install -r Builds/QuestBodyAvatarClinicianTest.apk`.
3. PC: `probe-bin/ClinicianTrackerProbe.exe stream` (add `--hand left` if the clinician strokes with the
   left hand). It runs `adb reverse` and waits for the app.
4. Start **Mirror Pilot Clinician Test** on the Quest (not Quest Link). The PC status line shows
   `Quest: ...` once connected.
5. Do the pilot's normal participant setup (table, body fit, midline) inside the test app.
6. **Calibrate (C):** strap the tracker on your own hand exactly as the clinician will wear it, wear the
   headset, press C, and for 15 s move that hand slowly over the table area while turning and tilting it.
   The PC prints the yaw, residual, mount offset and sample counts; poor fits are rejected.
7. **Confirm (V):** a pink marker shows the calibrated tracker. With the tracker still on your hand, the
   app measures predicted vs. your own wrist; alternatively touch the tracker with an index fingertip.
   Press V; the measured offsets are logged.
8. Hand the tracker (same strap position) to the clinician. Press **1** (congruent) or **2** (reflected),
   **0** for off. **K** toggles the marker, **X** clears the calibration.
9. If the tracker loses tracking, the hand is hidden and the alignment returns to unconfirmed: check the
   marker and press V, or recalibrate. A Quest recentre or a different tracker serial requires C.

## Provisional gates (engineering, not validated)

Calibration needs ≥ 300 usable samples from 15 s of usable tracking (at most 45 s), ≥ 25° tracker rotation spread, ≥ 5 cm hand position spread,
≤ 2 cm RMS residual and a tracker-to-wrist distance ≤ 25 cm. Samples are skipped when the tracker moves faster
than 0.25 m/s (hand tracking and the USB stream have different latencies). These numbers are starting points
for the prototype, not acceptance criteria.

## Status

Implemented and compiled: see the commit history on this branch. Hardware results are recorded below as they
are obtained; nothing above is a claim that the clinician display is accurate.

### 2026-11-11: build, install, first off-Link check

- Unity 6000.6.2f1 on the lab PC: imports, compiles, math self-checks pass (synthetic calibration recovered
  yaw 37.0°, 0.3 cm RMS), test APK builds and installs beside the pilot; the pilot package (0.13.1) is untouched.
- Fresh-checkout finding (pilot, pre-existing): the checked-in pilot scene references the hand-prefab object
  IDs of the machine that first generated them; regenerated prefabs keep their GUIDs but get new IDs, so a
  pilot built from a fresh checkout would show no participant hands. The test scene copy is re-pointed at the
  local prefabs; the pilot scene is not modified here.
- **Blocker observed: SteamVR does not stay up once the Quest leaves Link.** With the default SteamVR setup
  (Quest via the `oculus` driver as the HMD), leaving Link left SteamVR running at first, with the tracker
  `Running_OK`. About two minutes later the `oculus` driver sent `VREvent_DriverRequestedQuit` (21:00:52 UTC)
  and SteamVR shut down, before the standalone app was launched (21:03:08 UTC). VIVE Hub's tracker service
  kept running, but SteamVR is the only interface to the tracker pose, so the stream stopped. The probe exited
  cleanly with reason `steamvr-quit`.
- Proposed next step (not yet done; changes SteamVR settings): run SteamVR without a headset using its `null`
  driver and with the `oculus` driver disabled, so no headset driver can end the session, then repeat the check.
  This is community-documented, not HTC-documented, and Quest Link through SteamVR is unavailable until the
  settings are restored.

### 2026-11-11: headless SteamVR and first on-headset attempts

- **Off-Link solved for this test:** SteamVR runs with its `null` headset driver and the `oculus` driver
  disabled (settings backed up outside the repo, restore script provided). The tracker then stays available
  while the Quest runs the standalone test app; the app connected over USB with frames 0–9 ms old and
  0.5–1 ms round trip, no dropped frames.
- Keyboard control was impractical for one person wearing the headset; the probe now also reads commands
  from a file and confirmation waits for the wearer's hand, so the session can be driven remotely.
- Calibration attempts on the wearer's right hand: (1) rejected, hand moved 2.1 cm and turned 15°;
  (2) rejected, hand spread 6.5 cm (gate then 8 cm, now 5 cm) although the fit was 0.7 cm RMS with a 6.2 cm
  mount offset, and the tracker lost tracking 8 s in; (3) aborted by repeated Quest tracking-origin changes.
- **Current blocker: tracking reliability in the room, not the software.** Over 22 min of PC log the tracker
  was `Running_OK` only 43 % of the time, with 8 losses lasting 28–161 s each. On the headset, the Quest
  logged `BoundaryNotFound → RelocalizingGracePeriod`, ground-plane errors and repeated tracking-origin
  changes (18 in 2 min), which invalidate any alignment, as the pilot itself also does. Both devices use
  inside-out cameras; lighting, visual features and the boundary/map setup for this room are the next things
  to fix. No clinician hand has been displayed or judged yet.
