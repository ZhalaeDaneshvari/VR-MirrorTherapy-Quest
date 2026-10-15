# Manual forearm-stroke demonstration — 0.10.1

This adds a timed visual stimulus to the saved basic mirror-therapy pilot. It does not detect clinician hands, physical contact, pressure, or actual stroke timing. No new XR packages or controller mode are enabled. Existing movement conditions and per-wearer calibration remain in place.

## Headset workflow

1. Complete alignment, default resting-hand surface estimation, and the embodiment fit check.
2. Choose the desired movement condition as before. Open **Settings → Touch demo**.
3. Choose **Real side: Left/Right** (the physical forearm the clinician will stroke). Choose **Cross-limb** for the opposite virtual arm, or **Congruent** for the same virtual arm. The screen explicitly names both sides. Touch mapping is independent of movement mapping.
4. Keep both palms down and relaxed on the real table. Default duration is four seconds; controls allow two to eight seconds. The visual marker travels along the top of the displayed forearm, from near the elbow to near the wrist. Small endpoint insets avoid the elbow crease and hand-mesh seam.
5. Select **Start countdown**, release the pinch, and rest both hands flat. The large card disappears. The app waits for half a second of steady valid hand poses before starting the countdown; it times out after 20 seconds if the hands never become ready. “Touch begins in 3…2…1” is accompanied by one beep each second. A higher cue marks the beginning of the stroke. The clinician follows the same elbow-to-wrist motion on the specified real forearm, aiming to reach the wrist at the ending cue.
6. A small turquoise contact marker follows the CURRENT displayed arm surface. It is a visual contact indicator, not a reconstructed clinician hand. The stroke runs once, then stops and returns to the touch controls. It never loops automatically.
7. **Stop touch** remains available using pinch or two-second gaze selection. USB Stop is available to the researcher without participant interaction.

Tracking/presence loss, invalid alignment, unavailable displayed forearm, substantial hand tilt, changing movement mapping, application suspension, or a render gap over 250 ms cancels the sequence. During preparation/countdown, an unsettled hand resets the countdown and waits for a new stable pose within the original 20-second preparation limit. Once the actual stroke starts, loss cancels it and it never automatically resumes. Flatness is a pose check, not proof that palms physically contact the tabletop. A clinician can still confuse the wearer hand tracker while its data remains marked valid; this update cannot detect that identity error. Check hand attribution before using the demonstration with physical clinician contact.

## Researcher control over USB

With the app running, awake and calibrated, run from the project directory:

```sh
python3 Tools/touch_control.py start --side left --mapping cross --duration 4
python3 Tools/touch_control.py stop
```

Use `--side right` to reverse the physical source and `--mapping congruent` for same-side visual touch. `--serial` selects a device when more than one is attached. `--adb` overrides the default Unity Android SDK adb path.

The script atomically delivers a uniquely identified command and waits for an acknowledgement. Existing command files are ignored on application launch so a stale Start cannot replay. Acknowledgement means the app accepted the request, not that the physical touch occurred. If the app rejects Start, check its message rather than assuming the clinician should begin. No external camera, controller, network service, or additional package is required.

The headset speaker provides beeps, not spoken narration. Confirm the clinician can hear them in the actual room before relying on them. Synchronization is manual and has not been measured.

## Engineering event log

Touch events share the auto-flushed `pilot-events-*.jsonl` stream: countdown requested/ticks/cancelled, visual start, approximately 10 Hz displayed world-space stimulus samples, visual stop/completion/rejection. Each includes a unique stroke ID, monotonic receipt/render-callback time, UTC, instructed physical side, visual side, movement mapping, duration, normalized progress and (when present) displayed world position. The source is explicitly `manual_instruction`; none of these events assert sensed physical contact. Before-render timestamps are not measured photon presentation times. This is still an engineering pilot log, not the finalized scientific dataset.

## Acceptance checks

- Run Left→Right and Right→Left touch, then same-side touch; verify the indicated virtual forearm and unchanged movement mapping.
- Confirm 3/2/1, start and end cues; verify one stroke and no automatic repeat.
- Verify the marker remains on top of the displayed forearm and travels elbow toward wrist.
- Stop during countdown and mid-stroke using headset and USB controls; verify no resumption without a new Start.
- Briefly remove tracking or pause the app; verify cancellation and no frozen floating marker.
- Compare forearm appearance and table fit with the saved basic pilot. This addition must not change body fit or move the participant hands.
- Check source/destination and timestamps in the log. Clinician timing accuracy and touch identity still require observation, not inference from these logs.

## Start-button repair (0.10.1 / Android 21)

Device logs from 0.10.0 showed repeated `touch_start_rejected` with the flat/tracked-hands requirement. Pinching Start conflicted with that instantaneous requirement. Start now accepts the request after checking calibration/presence, then waits for the wearer to release the pinch and settle both hands. The Start button is actionable so rejection reasons can be shown in the main instructions instead of a disabled button or a status line overwritten by hover feedback. Stable-pose timing/reset checks are automated; the wearer must still verify the repaired interaction on Quest.
