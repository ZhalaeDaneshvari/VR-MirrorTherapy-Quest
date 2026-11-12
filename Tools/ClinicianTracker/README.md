# Clinician tracker probe (VIVE Ultimate Tracker, Windows) — 0.1.0

Windows tool for the clinician-hand test app. It is **not part of the pilot** and is not a Unity component:
it adds no XR package to the Unity project and never touches the pilot scene or APK. It reads one VIVE
Ultimate Tracker through SteamVR's OpenVR API, logs it separately from participant data, and in `stream` mode
feeds the Quest app **Mirror Pilot Clinician Test** and serves the researcher dashboard. The Quest side and
the session procedure are in [`CLINICIAN_TRACKER_TEST.md`](../../CLINICIAN_TRACKER_TEST.md).

## Requirements

- Windows PC with SteamVR running and the tracker shown as ready in VIVE Hub and as an icon in SteamVR.
- .NET SDK 9 (build only). The OpenVR loader `openvr_api.dll` is copied from the local SteamVR install
  (`-p:SteamVRDir=...` if SteamVR is elsewhere). The C# binding is Valve's OpenVR SDK v2.15.6
  (`ThirdParty/OpenVR`, BSD-3-Clause).

## Build and run

```powershell
dotnet build Tools/ClinicianTracker/ClinicianTrackerProbe/ClinicianTrackerProbe.csproj -c Release -o probe-bin
probe-bin/ClinicianTrackerProbe.exe list
probe-bin/ClinicianTrackerProbe.exe live            # Ctrl+C to stop; --seconds N for a timed capture
probe-bin/ClinicianTrackerProbe.exe test            # hardware test 1, about 1 minute
```

`--serial <serial>` selects the clinician tracker when more than one generic tracker is registered. The probe
initialises OpenVR as a **background** application: it never launches SteamVR, never takes scene focus from
Quest Link content, and exits when SteamVR quits.

## What is recorded

`logs/<UTC>-<mode>/` (ignored by Git): `session.json` (tool version, OpenVR runtime, all SteamVR device
identities, chosen tracker, stopwatch frequency, coordinate/timing conventions), `tracker.jsonl` (one sample
or event per line), `closed.json`, and for `test` a `summary.json`.

Each sample: host QueryPerformanceCounter ticks and seconds, UTC ms, tracker serial and OpenVR index,
`connected`, `trackingResult`, and two poses with their own validity:

- `raw`: `TrackingUniverseRawAndUncalibrated` (the tracker driver's frame) with velocity and angular velocity.
- `standing`: SteamVR's standing play space, for comparison only.

Units and axes are OpenVR's (metres, right-handed, +Y up, -Z forward, quaternion x,y,z,w); no Unity
conversion is applied. Status transitions, device (de)activation, universe changes and SteamVR quit are
logged as events.

**Timing limits.** OpenVR gives client applications no sensor exposure timestamp. Poses are requested with
zero prediction, i.e. vrserver's estimate at call time, and vrserver extrapolates between driver updates; a
changing pose therefore does not prove a new tracker measurement, and the tracker's own update rate is not
observable here. Host receipt time is recorded; end-to-end latency must be measured separately.

## Hardware test 1: real movement

`test` waits for Enter, then gives timed prompts with a beep at each step: rest (noise), slide, lift,
turn in place, cover the tracker without moving it, uncover. Pass the movement you will actually measure out
(`--slide-cm`, `--lift-cm`, `--turn-deg`; defaults 30, 20, 90) so the report compares against it:

| Check | Evidence | Plausibility expectation |
|---|---|---|
| identity | serial, model, tracking system | one generic tracker with a serial |
| valid while resting | valid fraction, RMS/max noise | ≥ 99 % valid |
| slide | horizontal and vertical displacement between rest poses | 0.5–1.5× stated, < 5 cm vertical |
| lift | vertical displacement | 0.5–1.5× stated upward, i.e. +Y is up |
| turn | rotation angle, axis tilt | 0.5–1.5× stated, axis ≤ 30° from vertical |
| recovers after uncovering | time to the first valid `Running_OK` sample | within the 10 s uncover step |
| covered | statuses seen, time to loss, displacement while still valid | informational only |

These thresholds only catch a missing or mis-oriented signal. They are not accuracy, latency or
acceptance criteria. A rest pose that is not still (> 1 cm spread over its last 2 s) makes the related check
inconclusive rather than passed.

## Verified on the lab PC (2026-11-11, Quest Link active)

- SteamVR lists the tracker as `GenericTracker`, model `VIVE Ultimate Tracker 1`, tracking system
  `vive_ultimate_tracker`, alongside the Quest 3 HMD and controllers from the `oculus` driver.
- 5 s resting capture: 250 Hz polling (median gap 4.00 ms), 100 % valid `Running_OK`, 0.09 mm RMS noise.
- `raw` and `standing` poses were bit-identical: SteamVR applied no transform of its own between the
  tracker driver's frame and its play space. Whether VIVE's driver already applies VIVE Space Calibrator's
  alignment inside that frame is not known. Alignment to the standalone Quest scene must come from this
  project's own calibration.
- `GetTimeSinceLastVsync` returned no data for this background client.
- Battery is readable (`Prop_DeviceBatteryPercentage_Float`); it was 2–6 % during these checks.

### Hardware test 1, first run (battery 4 %)

Movement reached the probe with the expected axes: slide 10.4 cm horizontal / 0.0 cm vertical, lift
+16.6 cm (+Y up), turn 59.9° about an axis 0.4° from vertical; rest noise 0.14 mm RMS. The physical
distances and angle were not measured, so these numbers show direction and plausibility, not accuracy.

Covering: the pose stayed valid and unchanged for 3.5 s after the prompt, then moved 6 cm at 0.6 m/s
(physical movement while covering is the likely cause), then switched to `Running_OutOfRange` with
`valid=false`. **Invalid poses arrive as position (0, 0, 0), frozen**: any display must gate on validity
or the clinician hand would jump to the frame origin. The tracker did **not** recover within 10 s of
uncovering and was still `Running_OutOfRange` minutes later at 2 % battery. Recovery was not a check
in that run (it reported PASS); it is now, so a run like that one fails.

### Hardware test 1, second run (battery 10 %, stated movement measured with marks/ruler)

`test --slide-cm 15 --lift-cm 15 --turn-deg 90`: slide 11.6 cm (0.77 of stated) with 0.3 cm vertical;
lift 14.0 cm (0.93); turn 93.2° (1.04) about an axis 0.6° from vertical; rest noise 0.21 mm RMS. The
slide shortfall is not explained yet (tracker centre vs. marked edge, or scale); calibration against
known points will measure scale directly.

Covered without being moved (1.8 mm displacement while still valid), the tracker went
`Running_OutOfRange`/invalid 3.6 s after the prompt and **did not recover within 10 s** (test result:
NOT PASSED). Watched afterwards for 90 s, still on the desk and uncovered: poses reported
`connected=false`, result 0 for 35 s, then the device deactivated/reactivated in SteamVR and stayed
`Running_OutOfRange`/invalid to the end. In the first run it recovered at some point before the next
check, cause unknown.

**Open issue:** after a few seconds of full camera occlusion the tracker loses tracking and, lying flat on
the table, has not relocalised on its own. Whether pointing its cameras at the mapped room restores it,
and how long that takes, is the next check; it decides whether occlusion during clinician touch is
recoverable in a session.

Later results (2026-11-11/12, details in `CLINICIAN_TRACKER_TEST.md`): with the default SteamVR setup,
SteamVR quits about two minutes after the Quest leaves Link; with SteamVR running headless (below) the tracker
stays available while the Quest runs its standalone app, and streaming over USB worked on the headset (frames
median 2.6 ms old at render, 0.5 ms round trip). The original tracker had poor room tracking; a replacement
tracker on a new VIVE Hub map tracked well enough for calibration (0.8–1.5 cm confirmed) but still drops out
occasionally, and the app then asks for the alignment to be checked again.

Still not verified: whether the tracker's frame is unchanged after a dropout (the app assumes it may not be),
end-to-end motion-to-photon latency, and calibration accuracy over a whole session.

## Documented constraints (checked 2026-11-11)

- HTC documents PC use through VIVE Hub with a SteamVR headset connected (Quest via the Meta Quest Link app),
  a tracking map created in VIVE Hub, and VIVE Space Calibrator to align trackers to the headset.
  Tracker-only use without a headset is **not documented by HTC**; community setups use SteamVR's `null`
  headset driver. Quest standalone apps cannot connect to the tracker directly; HTC lists direct
  third-party standalone support as future plans.
  [Setup for third-party headsets](https://blog.vive.com/us/how-to-set-up-vive-ultimate-tracker-for-third-party-steamvr-headsets/),
  [Other headsets](https://www.vive.com/us/support/ultimate-tracker/category_howto/can-i-use-the-tracker-with-other-headsets.html)
- The map origin is set during VIVE Hub map creation. Rebuilding the map, re-running VIVE Space Calibrator
  or running another space calibrator can move the tracker's frame; HTC's fix for a mismatch is covering
  the cameras for about 5 s or power-cycling. Any project calibration must be treated as invalid after these.
  [Creating a tracking map](https://www.vive.com/us/support/ultimate-tracker/category_howto/creating-a-tracking-map.html),
  [Position mismatch FAQ](https://business.vive.com/eu/support/ultimate-tracker/faq/tracker-position-in-vr-and-real-world-dont-match.html)
- Inside-out tracking with two wide-FOV cameras; up to 10 m to the dongle on 2.4 GHz; up to 7 h battery;
  low light, direct sunlight, blank walls and reflective surfaces degrade tracking. HTC publishes no report
  rate or latency. Optional auto-sleep can put an idle tracker to sleep after 3 minutes.
  [Product specs](https://business.vive.com/vive-ultimate-tracker),
  [Tracking not stable](https://business.vive.com/us/support/ultimate-tracker/faq/tracking-is-not-stable-or-accurate.html)
- OpenVR: each driver has its own raw space; `TrackedDevicePose_t` carries no timestamp.
  [Driver API documentation](https://github.com/ValveSoftware/openvr/blob/master/docs/Driver_API_Documentation.md)

## Streaming to the Quest test app

`stream` sends every tracker sample to the clinician test app over USB (`adb reverse tcp:47800`; it finds
Unity's `adb.exe` or takes `--adb`), keeps running and reconnects if SteamVR restarts, and accepts researcher
commands three ways:

- **Researcher dashboard** (the "website"): `dashboard.html`, served on this PC only at
  `http://localhost:47801` (`--dashboard <port>`, `--browser no` to not open it). It shows calibration
  (ready / check needed / needs calibration, fit, room anchoring), clinician-hand visibility and why it is
  hidden, participant number and recording, headset stage and affected arm, tracker battery, data age and round
  trip, the arm hold and contact lift, and the headset's messages. Controls: participant number (show, or show
  and confirm), calibrate with participant, full calibration, confirm alignment, accept without a check,
  marker, clear, clinician-hand mode Off / Mirrored / Same side, and **Open headset view** (scrcpy, read-only,
  not recorded; default `..	ools\scrcpy\scrcpy.exe` relative to the probe's folder, or `--scrcpy <scrcpy.exe>`).
- **Keys** in the probe window: **C** calibrate, **V** confirm alignment, **K** marker, **0/1/2**
  off/congruent/reflected, **X** clear calibration, **A** redo `adb reverse`, **Q** quit.
- **Command file** (`stream-commands.txt` in the working folder, or `--commands <file>`): append one command per
  line to drive a session remotely.

Line protocol: see `StreamServer.cs`. Verified on the lab headset 2026-11-11/12 (see `CLINICIAN_TRACKER_TEST.md`).

### Running SteamVR without a headset (needed once the Quest leaves Link)

With the Quest as SteamVR's headset (`oculus` driver), SteamVR shuts down soon after the Quest leaves Link and
the tracker stream stops. For the standalone test app, close SteamVR, back up
`C:\Program Files (x86)\Steam\config\steamvr.vrsettings`, and set: `"steamvr": {"requireHmd": false,
"forcedDriver": "null", "activateMultipleDrivers": true}`, `"driver_null": {"enable": true}`,
`"driver_oculus": {"enable": false}`. This is community-documented, not HTC-documented. Quest Link through
SteamVR does not work until the backed-up file is restored (close SteamVR first). On the lab PC the backup and a
restore script are in `Documents\VRMirrorTherapy\steamvr-backup\` (outside the repository).
