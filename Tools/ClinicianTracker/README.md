# Clinician tracker probe (VIVE Ultimate Tracker, Windows) — 0.1.0

Isolated Windows tool for the clinician-hand tracking investigation. It is **not part of the pilot**:
nothing under `Assets/`, `Packages/` or the pilot scene/APK is touched, and no XR package is added to the
Unity project. It reads one VIVE Ultimate Tracker through SteamVR's OpenVR API and logs it separately from
participant data.

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

`test` gives timed prompts with a beep at each step: rest (noise), slide ~30 cm along the table, lift ~20 cm,
turn ~90° in place, cover the tracker's cameras, uncover. It then reports:

| Check | Evidence | Plausibility expectation |
|---|---|---|
| identity | serial, model, tracking system | one generic tracker with a serial |
| valid while resting | valid fraction, RMS/max noise | ≥ 99 % valid |
| slide | horizontal and vertical displacement between rest poses | ≥ 10 cm horizontal, < 5 cm vertical |
| lift | vertical displacement | ≥ +8 cm, i.e. +Y is up |
| turn | rotation angle, axis tilt | ≥ 45° about a near-vertical axis |
| covered / uncovered | statuses seen, drift, recovery time | informational only |

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
- Battery is readable (`Prop_DeviceBatteryPercentage_Float`); it was 4–6 % during these checks.

Not yet verified: deliberate movement (hardware test 1), behaviour when the headset leaves Link, network
streaming to the standalone Quest app, calibration accuracy, latency.

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
