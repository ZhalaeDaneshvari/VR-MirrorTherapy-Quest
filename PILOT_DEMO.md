# Guided PI demonstration — 0.8.3 / code 16

This build is a guided **movement/embodiment demonstration**, not a complete clinical study system. Automatic clinician-hand identification and sensed touch are not implemented. Keep other people's hands out of the headset's hand-tracking view during this demonstration. The provider does not give us a reliable person identity, so we cannot guarantee that it will reject a clinician's hand.

## Prepare (helper)

- Charge the headset, use a well-lit room, and prepare a stable chair and real table.
- Measure floor-to-tabletop height with a tape measure. The app does not scan the glass tabletop.
- If known, note the wearer's standing height. Do not ask them to stand or adopt a painful calibration pose just to obtain it. Automatic body estimation is available but must be visually checked.
- Put the controllers aside for the body-driven demonstration. Do not enable simultaneous controllers/hands: the installed Meta Core 207.0.0 SDK and Meta documentation mark it incompatible with body tracking.
- Launch **Quest Body Driven Avatar**. Each launch requires explicit setup confirmation. The saved table dimensions are suggestions, not confirmation for the next wearer.

## In-headset walkthrough

Look at a button for two seconds to select it, or use head gaze plus a thumb/index pinch. Move gaze away before selecting the same button again. Release the pinch between selections.

1. **Welcome.** Enter known standing height with the +/- buttons, or leave automatic. Sit comfortably, centered at the table, facing it squarely. Select Begin alignment, then look straight ahead with relaxed shoulders. The entire menu disappears for four seconds; the next page returns when alignment finishes. Pinching is optional: gaze-hold selection supports people who cannot comfortably pinch. Researcher-operated remote controls remain a future accessibility improvement.
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


### Menu accessibility update (0.8.1)

The wearer reported the first menu was not visible/operable. This update gates placement on OpenXR user presence, brings an off-screen Welcome card back after one second, and adds two-second head-gaze dwell selection with a one-activation latch. Dwell and pinch use physical inputs; neither changes movement mapping. Alignment still hides all UI. Wearer confirmation is pending.

0.8.1 Configure and BuildAndPreview passed, including dwell single-activation/rearm tests and alignment visibility assertions. Release APK SHA-256: `222ff05287c4f5b1bd6140ad66db78b71ffa75398410b765a6a83d98f8c4c646`.

### Table blocking and gaze feedback (0.8.2)

The wearer accepted the accessible alignment flow but reported arm penetration, no circular dwell feedback, and menus occluded by the table. This checkpoint adds:

- An unconditional display-mesh table constraint after arm generation. Every triangle whose horizontal bounds overlap the tabletop has all three vertices projected to at least 1 mm above its top. Unlike the former shallow rest adjustment, there is no depth cap and it runs even when optional palm-rest assistance is OFF. The conservative footprint test also covers triangle interiors at edges, not just sampled vertices.
- A rigid correction of a partially intersecting displayed hand, using its deformed surface minimum, to keep the wrist seam above the table. Raw physical poses and mapped targets remain unchanged. Hands entirely below the tabletop retain their actual pose and occlusion; the corresponding arm is withheld to avoid a visibly intersecting bridge. This is a deliberate display policy, not physics or sensed contact.
- A turquoise circular fill for gaze selection, including Settings, using the existing two-second dwell timer.
- Stereo-compatible overlay rendering for the entire menu and selection ring, with explicit background/button/text ordering. The world/table cannot depth-occlude these controls; all remain hidden during alignment.

A strict display constraint can visibly flatten/lift geometry when calibration or body estimates are wrong. It does not make those estimates anatomically correct. Large corrections should trigger refitting, not be treated as measured movement. The existing correction diagnostics remain separate from raw tracking. No claim of universal naturalness or physical collision detection is made.

Validation includes 500 deterministic triangle/table cases, including conservative edge-crossing coverage, plus an occluder render test and preview of the circular selection indicator. Headset appearance still needs wearer confirmation after installation.

0.8.2 Configure and BuildAndPreview passed. The final occlusion/ring preview was visually inspected; an additional brightness assertion protects text readability. Final APK SHA-256: `98bfcdc1543b96cdcfbc71f5691c829af44a0a479761e7da49c5874d2866ad38`. ADB installation returned Success; wearer appearance confirmation remains pending.


### Quick surface-height adjustment (0.8.3)

Open Settings > Surface height, then use Lower 1 cm / Raise 1 cm. Changes apply immediately and save to the existing setup file; each step invalidates surface confirmation and records a `surface_height_step` demo event. Move gaze away and back to repeat a dwell selection. Confirm surface once the physical and virtual surfaces agree. Exact numeric entry remains available, including the keypad cm/inches switch. No realignment or body-frame change is needed for a height-only correction.

The same controls are present in initial setup. On a bed, match the surface where the resting arms actually lie: a mattress is deformable, while this prototype still represents a single horizontal plane. It does not sense or reconstruct mattress compression. Moving to a new location requires normal alignment and surface confirmation.

0.8.3 build and existing validation passed; the surface-height page was visually inspected. ADB install returned Success. APK SHA-256: `965f9fe637059f0769f85f5ad3c4c2a24caaab54bc84712e6448317c4e495f37`. Surface fit at the new bed location remains a wearer check.

## Alignment recovery (0.8.4 / Android 17)

A headset report showed alignment rejecting the combined head/floor-origin check and returning silently to Welcome. Begin alignment now requests Floor tracking again, with bounded retries during the four-second hidden-menu interval. Alignment still requires an actual Floor origin; device-relative tracking is never treated as a measured floor. The failure screen now displays the reason prominently and advises resetting the Quest floor/boundary when floor tracking is unavailable. Diagnostic logs distinguish tracking status, supported origins, current origin and request acceptance. A failed realignment invalidates the old alignment. Saved support-surface height is preserved.

Hardware acceptance: select Begin alignment while seated and looking ahead. Expect Fit the support surface after four seconds. If Alignment needs attention appears, record its message and inspect `[Seated]` logs; restore Quest floor/boundary setup before retrying. This change does not establish the physical floor height or table position automatically.
