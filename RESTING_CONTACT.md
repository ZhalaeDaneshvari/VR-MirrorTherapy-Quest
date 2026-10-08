# Resting contact — 0.7.2 (Android version code 12)

This is a display correction for engineering tests, not detection of physical touch or a measured arm surface. The glass table and automatic body estimation still require wearer validation.

## Why this checkpoint exists

The wearer reported flicker, upward-angled wrists, hovering hands and arm/table intersections in 0.7.1. App diagnostics at a 0.900 m table showed hand corrections around 3–4 cm and residual arm penetration of 4–12 cm. The old correction switched off abruptly when a required lift exceeded 4 cm. Arm correction also skipped vertices beyond that threshold. Rendering was repeated from tracking callbacks and LateUpdate, including repeated CPU hand mesh baking. These are verified code/log findings; they do not prove the cause of every reported flicker.

## Changes

- Render the avatar once per frame in ordered BeforeRender after XR Hands updates, instead of repeated callbacks. `previousRenderMs` measures this avatar CPU work only, not full frame time or GPU performance.
- Keep a separate resting state for each displayed hand, reset on invalid tracking, mapping-source changes, disabled support or table-height changes.
- For near-horizontal palms near the tabletop, blend limited palm leveling (maximum 30 degrees, 60 ms blend) around the wrist, retaining relative finger articulation.
- Query the deformed hand skin. Support targets 0.5 mm clearance with bounded signed correction: at most 6 cm upward and 1 cm downward. Rest entry/release use different thresholds to reduce toggling; downward settling/release uses a 45 ms time constant. This deliberately changes displayed pose; raw tracking and remapped targets remain unchanged.
- Lift the estimated elbow vertically during rest, without moving it outward, by at most 16 cm. This compensates for unreliable estimated elbow height; it is not evidence of the participant's true elbow position. Finish small underside intersections up to 2.5 cm while preserving the exact hand/arm seam.
- Hands fully below the table bypass support and remain occluded by the opaque table. Free-space motion is not flattened. Gross misalignment is not silently corrected without bounds; residual overlap still raises a fit warning.
- Settings now includes **Table rest ON/OFF**, persisted independently of movement condition. Default ON for this engineering checkpoint. Switch OFF to compare uncorrected display. Existing table placement is preserved.

The eligibility thresholds are engineering tolerances, not scientifically validated calibration parameters. The arm surface is generic. Support can briefly ease out during a lift, can be inappropriate for an intended near-surface gesture, and cannot guarantee zero overlap for all tracking errors. Any research recording must include the enabled state, hand translation, palm rotation, elbow correction, residual clearance and final rendered poses in addition to raw/remapped poses. Current periodic diagnostics are not a research dataset.

## Validation

Unity validation covers reflection and source isolation, hand invalidation, seam continuity, rest entry, continuity across the former 4 cm cutoff, hysteresis, raised-hand release, below-table bypass and invalid-data reset. A synthetic flat-rest preview separately checks skin translation and residual arm clearance; it does not test actual tracking or replace headset acceptance.

## Wearer test

1. Open Quest Body Driven Avatar. In Settings, leave Table rest ON, confirm the existing table height, then Realign while looking forward.
2. Rest both palms and forearms naturally on the physical surface. Check wrist continuity, palm angle and whether the hand looks supported rather than hovering.
3. Slowly lift and replace each hand. Check for snapping or rapid appearance/disappearance. Repeat in Normal and Swap both sides.
4. Move a hand below the table; it should not be pulled back onto the tabletop.
5. Compare Table rest OFF and ON. If flicker remains, distinguish whole-hand disappearance (tracking validity) from pose jitter or frame stutter.

Wearer acceptance and headset performance measurements remain pending for this checkpoint.

## Build evidence

Unity 6000.6.2f1 completed Configure and BuildAndPreview successfully. The mathematical checks and synthetic contact/arm-clearance assertions passed; expanded/closed settings previews were generated and expanded layout inspected. Release APK SHA-256: `b6545280f593c8d704577dd8bf61351f4206cd98612ee1e10e1525d0e8d7b287`.

ADB `install -r` returned Success and the launcher command succeeded. The device disconnected before the subsequent app-scoped startup-log and saved-settings readback, so this run does not establish runtime performance or fit acceptance. The install did not clear app data. Existing optional Meta/Unity build warnings remain; there were no compilation/build failures.
