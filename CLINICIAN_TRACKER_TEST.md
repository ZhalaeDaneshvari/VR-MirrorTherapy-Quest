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

### 2026-11-12: first displayed clinician hand (new tracker, congruent mode)

Setup: replacement tracker (serial 5B-BH3R02428) after a new VIVE Hub map; SteamVR headless (`null` driver,
`oculus` disabled); test app over USB; session driven remotely from the PC. The tester wore the tracker on the
back of the right hand and sat inside the mapped area.

- Calibration accepted on the first attempt: 1.2 cm RMS residual, 14.2 cm tracker-to-wrist mount, tracker turned
  82°, 6.8 cm hand spread, 606 samples. The tracker dropped out twice during calibration (15:31:56–15:32:04 and
  15:32:06–15:33:07 local), so collection hit its 45 s cap and the first confirmation attempt timed out.
- Alignment confirmed after the tracker recovered: predicted vs. the wearer's own tracked right wrist **1.2 cm**.
- Congruent mode, 113 s (headset log): clinician hand displayed in 86.7 % of rendered frames; every hidden frame
  was "headset not tracking/worn", none was a tracker loss. Stream age at render median 2.6 ms (p95 26.4, max
  42.1); PC read to Quest receipt median 0.6 ms (p95 1.4) via the RTT clock mapping; USB round trip 0.53 ms.
  Displayed wrist step between frames median 1.2 mm, p99 22.9 mm, max 64.6 mm: occasional single-frame jumps
  of several centimetres, cause (fast movement or tracker correction) not determined.
- Tester's judgement: the clinician hand "looks right" (position and following).
- Not yet tested: a second person wearing the tracker as clinician, reflected mode, the elbow-to-wrist stroke
  (palm-to-displayed-forearm proximity needs the pilot body fit, which was not run), end-to-end motion-to-photon
  latency, and behaviour over longer sessions.

### 2026-11-12: reflected mode and the forearm stroke (one person, "Left hand only" pilot profile)

The tester wore the tracker on the right hand as the clinician and stroked their own resting left forearm;
the pilot's guided setup ran with "Left hand only". A PC helper kept alignment confirmed and recalibrated when
the app required it (all commands logged by the probe).

- Putting the headset back on moved the Quest origin again (15:39:18), invalidating alignment as designed; the
  helper recalibrated: two attempts rejected (implausible 48 cm mount; 2.5 cm residual), third accepted
  (1.1 cm RMS, 10.6 cm mount), alignment confirmed at 1.3 cm. The tracker did not drop out afterwards.
- Reflected clinician hand displayed; the tester confirmed seeing it.
- **Finding: the participant's displayed forearm disappears when the clinician hand reaches in.** Over 151 s
  of reflected mode, the displayed forearm vanished 28 times (0–1.9 s each) while the clinician hand was shown,
  usually as the clinician palm came within 7–20 cm (median 15 cm). Palm-to-displayed-forearm distances under
  6 cm were only recorded for 0–45 % of the elbow-to-wrist line. Cause: Quest hand tracking uses the headset
  cameras; the clinician's hand occludes the participant's hand, and the pilot intentionally hides the whole
  arm on invalid hand data instead of inventing a pose. The VIVE-tracked clinician hand is unaffected.
- Implication: the tracker solves clinician identity and clinician-hand pose, but touch near the participant's
  hand breaks participant hand tracking. Any mitigation (holding the resting arm during touch, stroke
  technique, an additional forearm tracker) changes or adds participant-side behaviour and needs a decision.

## Participant arm hold during clinician touch (test build only, added 2026-11-12)

Chosen by the user after the occlusion finding above. `ParticipantArmTouchHold` exists only in the generated test
scene; the accepted pilot scene and pilot scripts are unchanged.

- **When:** the participant's hand is lost by Quest hand tracking (not merely excluded by the hand profile), the
  same side was tracked and **resting** (wrist < 5 cm/s over 0.3 s) at most 0.5 s earlier, the movement condition
  is unchanged, the head is tracked, and the **displayed clinician palm is within 35 cm** of that displayed wrist.
- **What:** the last tracked displayed pose of that side (hand and the arm attached to it) keeps being shown.
- **Ends:** as soon as the hand is tracked again, after **3 s** at most, when the clinician hand moves beyond 35 cm
  or is hidden, if the movement condition changes, or if head tracking is lost. It is then hidden as before.
- **Data:** physical hand data is never altered. In the test app, the pilot recorder's *mapped/displayed* records
  show the held pose during a hold; the clinician log marks every held frame (`heldLeft`/`heldRight`) and logs
  `participant_arm_hold_started/ended` events with the reason. The headset status line shows "HOLDING … arm".
- These limits are provisional choices for the stroke task (participant instructed to keep the arm still), not
  validated parameters. A held arm is an inference: it cannot show movement made while the hand is occluded.

### 2026-11-12: reflected stroke with the arm hold installed

- After reinstalling, opening the app triggered a burst of ~60 Quest tracking-origin events within about a
  second (app start / headset put on); calibration restarted sampling on each, as designed, then settled.
- Calibration: first attempt rejected (2.4 cm residual), second accepted (1.3 cm RMS, 8.2 cm mount, tracker
  turned 68°, 971 samples); alignment confirmed at 1.3 cm vs the wearer's own right wrist.
- Reflected stroke (156 s of reflected mode): the tester reported the arm stayed visible. Headset log: **0**
  forearm disappearances while the clinician hand was shown (28 in the earlier run), clinician palm within 6 cm
  of the displayed forearm over 35–100 % of elbow-to-wrist (minimum 0.1 cm).
- **The hold itself was not exercised:** 0 held frames and 0 hold events. Participant hand tracking did not drop
  during these strokes, so the improvement came from tracking holding up (stroke technique, hand position or
  conditions), not from the hold. The hold remains untested on the headset; a deliberate test (fully covering
  the resting hand for 1–2 s while the clinician hand is near) is still needed.

## Pilot-ready session flow (2026-11-12, test app only)

Built after the user asked for a demo/pilot setup driven from the PC. The accepted pilot scene keeps its own
behaviour; every new behaviour below is switched on only by the clinician test scene.

**Start (PC):** `probe-bin\ClinicianTrackerProbe.exe stream` (SteamVR in headset-less mode, Quest on USB). It
opens the researcher dashboard at `http://localhost:47801` (this PC only) and keeps running if SteamVR restarts.

**Researcher dashboard:** status cards for calibration, clinician hand (visible/hidden and why), participant
number and recording, headset stage and affected arm, tracker/link (battery, data age, round trip) and touch
display (arm hold, contact lift); controls for participant number, calibration, clinician-hand mode and the
marker; messages from the headset; **Open headset view** starts scrcpy 5.0.1 (official Windows release,
checksum-verified, in `Documents\VRMirrorTherapy\tools\scrcpy`, outside the repository) with the Unity adb, as a
read-only, unrecorded USB mirror of the headset display.

**Headset flow changes (test app):**
- After the participant number, the participant chooses **which arm is affected**. They move and rest the OTHER
  arm; it is mirrored onto the affected side (left affected = "right hand only" + right-to-left mirroring).
- The researcher can set the participant number from the dashboard ("Show on headset" leaves Confirm to the
  participant; "Show & confirm" starts the recording session).
- The placeholder "Coming next: visual touch" page becomes **Clinician touch**: rest the unaffected forearm;
  the clinician strokes it elbow to wrist; the stroke appears on the affected side.
- The clinician hand is shown only during the mirror experience and the clinician-touch stage. The participant
  never sees the alignment marker or researcher status text; only participant-calibration instructions.

**Calibration in a pilot:**
1. Full calibration once per tracker strap (researcher wears the headset and the strapped tracker): stores the
   strap mount and a room alignment.
2. During the participant's setup (e.g. hand colour), **Calibrate with participant**: the participant holds the
   tracker in their moving hand for ~15 s. The room alignment is re-solved; the stored strap mount is reused;
   alignment is confirmed from the fit. Then the tracker goes back on the clinician.
3. After a tracker dropout the alignment returns to "check needed": confirm with a hand measurement, accept
   without a check (logged as unmeasured), or calibrate with the participant again. A Quest recentre requires
   calibration.

**Brushing, not entering:** a display-only correction lifts the rigid clinician hand so its skin rests on top of
the displayed participant forearm (rounded forearm model around the displayed top line, 3 mm clearance). The lift
is logged per frame (`contactLiftMetres`) and shown on the dashboard. It does not sense contact.

**Status:** headset build compiles and passes the self-checks (including the contact-lift check); the dashboard
was verified serving status and commands with the headset app connected. Not yet verified on the headset:
participant-held calibration, affected-arm flow, gating, contact lift, researcher-set participant number, and
the scrcpy view on this PC.
