# Guided PI demonstration — 0.8.0 / code 13

This build is a guided **movement/embodiment demonstration**, not a complete clinical study system. Automatic clinician-hand identification and sensed touch are not implemented. Keep other people's hands out of the headset's hand-tracking view during this demonstration. The provider does not give us a reliable person identity, so we cannot guarantee that it will reject a clinician's hand.

## Prepare (helper)

- Charge the headset, use a well-lit room, and prepare a stable chair and real table.
- Measure floor-to-tabletop height with a tape measure. The app does not scan the glass tabletop.
- If known, note the wearer's standing height. Do not ask them to stand or adopt a painful calibration pose just to obtain it. Automatic body estimation is available but must be visually checked.
- Put the controllers aside for the body-driven demonstration. Do not enable simultaneous controllers/hands: the installed Meta Core 207.0.0 SDK and Meta documentation mark it incompatible with body tracking.
- Launch **Quest Body Driven Avatar**. Each launch requires explicit setup confirmation. The saved table dimensions are suggestions, not confirmation for the next wearer.

## In-headset walkthrough

Use head gaze at a button and a thumb/index pinch. Release the pinch between selections.

1. **Welcome.** Enter known standing height with the +/- buttons, or leave automatic. Sit comfortably, centered at the table, facing it squarely. Select Begin alignment, then look straight ahead with relaxed shoulders. The entire menu disappears for four seconds; the next page returns when alignment finishes. If the participant cannot pinch comfortably, do not force the movement; researcher-operated controls are a remaining accessibility requirement.
2. **Fit the table.** Select Enter table height and type the measured height on the keypad (cm or inches), then Save, then adjust the near edge using 2 cm steps. Rest hands gently on the physical table and compare virtual placement. Confirm only when the wearer/helper agrees. If placement is wrong, correct the table instead of using hand support to mask the error. Floor origin must already be correct.
3. **Check embodiment.** Inspect wrists, elbows and shoulders. Rest, lift and slowly turn each hand within a comfortable range. Optional arm fullness and Table rest ON/OFF are appearance/display settings, not anatomical measurements. Continue is enabled only after both hands and valid body estimates remain available for one second. The wearer must still confirm the fit visually; valid tracking does not prove accurate anatomy.
4. **Movement demo.** Select Normal, Left to right, Right to left, or Swap both sides, then Start demo. One-way modes show the source and reflected destination hands. During the demonstration only Settings remains. Move each hand slowly and explain which physical side drives which virtual side.
5. **Settings.** Pause the demonstration, change the affected-side label, end the demo to choose another mapping, refit, or choose New wearer. Recalibration always hides the menu for four seconds and requires table confirmation again. Affected side is metadata; it does not silently choose a mapping.

Resume/head-tracking loss requires setup again. Invalid hand data still hides that hand instead of inventing tracking. This may look like a disappearance; do not confuse it with a rendering frame-rate issue.

## Suggested two-minute explanation to the PI

"This prototype separates physical hand tracking from displayed avatar motion. In Normal, each physical hand drives the corresponding virtual hand. In one-way mirroring, one physical hand drives the opposite virtual hand while the source can remain visible. In bilateral swap, the two sides independently exchange control. The shoulders and elbows use body estimates; the table is fitted and confirmed for each wearer. The next feasibility milestone is independently sensing the clinician's contact while maintaining participant identity. That part is not demonstrated as solved today."

## Clinician sensing: verified constraint

OpenXR's standard hand tracker selects the user's left or right hand; it does not expose a separate clinician identity:
https://registry.khronos.org/OpenXR/specs/1.0/man/html/XrHandEXT.html

Meta documents multimodal hands/controllers as incompatible with IOBT/full-body synthesis:
https://developers.meta.com/vr/documentation/native/android/native-multimodal/

The same restriction is explicit in installed Meta Core `OVRProjectSetupMovementSDKConfigurationTasks.cs` and `OVRBody.cs`. A controller-driven clinician tool therefore needs a separate mode with a different arm estimator and hardware validation, or an independently tracked external sensor. It would be a tracked tool, not bare-hand detection. Camera access can support custom computer vision, but does not supply validated multi-person identity/contact tracking:
https://developers.meta.com/vr/documentation/unity/unity-pca-documentation/

Do not physically perform a clinician-touch demonstration with the implication that the app identifies or mirrors the clinician's hands. The current system cannot guarantee participant/clinician separation.

## Data and verification boundaries

`pilot-events-*.jsonl` records app/setup/demo events with UTC, monotonic receipt time, version, mapping, affected-side label, table values, height entry and table-rest setting. Each line is flushed as written; this improves crash recovery but is not a full physical/displayed-pose research dataset or a guarantee against power-loss/storage errors. No camera imagery or participant identifiers are collected by this feature.

The 0.7.2 resting-contact renderer is retained: bounded visual hand/palm/elbow support is separate from raw tracking and movement mapping, and avatar rendering occurs once per frame after XR Hands. These changes are not proof of natural anatomy or stable frame rate for every wearer. Hardware acceptance with at least two differently sized wearers is required before presenting calibration as generalizable.

## Acceptance before the PI demo

- All four pages are readable in-headset; no other menu remains during alignment.
- Alignment does not follow head movement afterward; table height/edge match the real surface.
- Both resting wrists are connected; elbows do not bow or penetrate; lifting releases support without snapping.
- Normal and all three mirror modes behave as labeled; temporary occlusion never freezes a stale pose indefinitely.
- End demo, New wearer, headset removal/resume and recalibration require appropriate confirmation.
- Compare at least two wearers if time allows; stop and refit if the avatar does not match.

Fallback checkpoint: `codex/stable-table-rest` / `a4f0646` (0.7.2), which predates the guided UI. Legacy repository remains untouched.

## Build/install evidence (2026-10-15)

Unity 6000.6.2f1 Configure and BuildAndPreview succeeded. Existing mapping/invalidation/seam/contact assertions passed. Pilot preview checks passed for the running Settings-only view and fully hidden alignment UI; table-entry conversion verified 31 inches = 0.7874 m and rejected invalid/out-of-range values. Calibration, keypad and running-view images were inspected. These are editor checks, not wearer acceptance.

Final APK SHA-256: `7e9b7c095dc868330e54a4cdbf4b3f31b770c2d1b610aad160b09d44768483ee`. ADB installation returned Success and launch was requested successfully. On-device multi-wearer fit, smoothness and the full guided sequence remain pending wearer feedback. Existing SDK warnings do not constitute validated clinician-tracking capability.

On-device startup evidence: the final build's process was running, the Meta body service reported its high-fidelity request accepted, and an ADB headset screenshot visibly showed the new Welcome page in stereo. This verifies startup/UI rendering only; the wearer has not yet confirmed the calibration sequence, arm fit or latency.
