# Milestone 4 — body-centered movement mirroring

Engineering prototype; not a validated clinical intervention. The user saw the M3 table and authorized continuation. Their real table is glass and exact alignment remains unverified. No clinical efficacy claim is made.

## What changed

- Normal movement: physical left → displayed left; physical right → displayed right.
- Left-to-right: physical left drives the reflected displayed right. Physical right is not used for that target.
- Right-to-left: physical right drives the reflected displayed left. Physical left is not used for that target.
- In either mirrored condition, show the source-side hand too (one source drives both virtual hands), or hide it (opposite side only). Normal always shows both independently tracked hands.
- Anatomical skinned hand meshes replace the joint-sphere display; continuous tapered arm surfaces replace separate capsules. These are generic hands and estimated arms, not personalized anatomy or photorealistic skin.
- The body frame remains fixed until explicit re-alignment. Missing calibration hides mirrored targets, without silently reverting to normal mapping.
- Invalid source joints invalidate the corresponding displayed joints. A skinned hand hides entirely on partial joint data, avoiding stale/deformed fingers. IK arms can remain visible if their wrist target is still valid; unreachable arms hide rather than stretch.
- Settings acknowledgements are guarded against write failures so a failed save cannot repeatedly re-align the body.

No new packages, legacy Oculus Integration, clinician-hand sensing, touch rendering, or scientific trial logger were added. Automatic clinician-hand tracking remains REQUIRED future work; there is no manual-touch substitute hidden in this milestone.

## Unity and reproducible asset setup

Use Unity **6000.6.2f1**, Android support, and the pinned package lock. Working folder: `VR-MirrorTherapy-Quest-Hands`, branch `codex/milestone-4-movement-mirroring`.

After cloning/checking out this branch, wait for package resolution, then use:

1. **Quest Prototype → Milestone 4 → Configure Movement Mirror**
2. **Quest Prototype → Milestone 4 → Validate Movement Mirror**
3. **Quest Prototype → Milestone 4 → Build Movement Mirror APK**

Configure imports only the left/right hand models already supplied in **XR Hands 1.9.0**, resolves all 26 joint references at edit time, and creates display-only prefabs under ignored `Assets/GeneratedHands`. It reassigns generated prefab references in the M4 scene. Run Configure on a fresh clone before opening/testing the scene; missing generated prefabs before this step are expected. Raw Unity sample assets are not redistributed in GitHub; the reconstruction code, source, scene and package pins are committed. See `THIRD_PARTY_ASSETS.md`.

Output: `Builds/QuestMovementMirror.apk`. App ID `org.mirrortherapy.questprototype.mirror`, name **Quest Movement Mirror**, version 0.4.0 (4). M1–M3 app IDs are unchanged. APKs/build caches are not source-controlled.

## Researcher workflow

Launch the new app. Seat the wearer facing the real table; correct the headset's floor/boundary first. The table is still uncalibrated until explicitly checked. From the project directory:

    python3 Tools/seated_setup.py --align

The wearer must be looking forward at the moment this command is sent. Default condition is Normal; inspect normal hand appearance/alignment first. The app has no voice recognition: when working with Codex, type “ready to align” here in chat.

Switch conditions without changing calibration:

    python3 Tools/seated_setup.py --condition left-to-right --source-hand hide
    python3 Tools/seated_setup.py --condition left-to-right --source-hand show
    python3 Tools/seated_setup.py --condition right-to-left --source-hand hide
    python3 Tools/seated_setup.py --condition normal

The headset status shows the selected mapping and visibility policy. `show` means the source hand appears in its normal position AND drives the opposite reflected hand. `hide` shows only the opposite hand/arm. Hidden targets remain invalid, not frozen in space.

Run the tool without arguments to read current settings. Use `--app seated` to control the older M3 app; M4 is this branch's default. Use `--serial DEVICE` if multiple devices are connected. Table dimensions and estimated arm lengths remain adjustable in centimetres (`--help`). Do not treat placeholder dimensions as measurements. `--confirm-table` is only for a researcher who has checked real/virtual correspondence.

Recenter, pause/removal, and a change in seating position require a fresh alignment. This prototype does not track torso motion independently. A glass table may be awkward to use as an alignment reference; visible physical edges or a stable opaque tabletop surface can help visual comparison, but still require verification. Never rely on virtual geometry for physical support.

## Acceptance test — wearer confirmation required

1. Normal: identify left/right, spread fingers, curl fingers, turn palms up/down. Check that hand surfaces do not twist or collapse and wrists meet forearms reasonably.
2. Left-to-right, source hidden: move ONLY the real left hand slowly; only the displayed right hand/arm should move. The real right hand should not drive that target.
3. Left-to-right, source shown: the same real left hand should drive BOTH displayed hands, reflected across your body midline.
4. Right-to-left: repeat with the real right hand driving the displayed left. Test source show/hide.
5. Return to Normal: both physical hands again drive their own side independently.
6. Re-align facing a different direction in the room; repeat slow mapping tests. Reflection must follow the calibrated body, not a room axis.
7. Occlude the source hand briefly, move out of tracking range, then recover. Targets should hide when source data is invalid; the other physical hand must not take over. Runtime-valid inferred joints can still occur during occlusion: API validity is not proof of direct optical measurement.
8. Recenter/remove headset: mirrored targets must wait for re-alignment. Stop if movement is confusing or uncomfortable.

Do not perform clinician-contact testing as evidence of automatic touch tracking. Quest participant hand tracking does not establish a separately identified clinician hand or physical contact.

## Reflection math and code

`MovementReflection` in `MovementRemapper.cs` works in metre-scale XR tracking coordinates. Let the calibrated body frame be translation b and rotation B; S = diag(-1,1,1) is reflection about its sagittal plane.

- Position: p' = b + B S B⁻¹ (p − b).
- Orientation: R' = B S (B⁻¹ R) S.

The second S repairs handedness in the destination joint frame; a single reflection has determinant −1 and cannot be encoded as a rotation quaternion. Code reconstructs the reflected forward/up basis with LookRotation, equivalent to this proper rotation. Every finger joint uses the same spatial mapping and anatomical index on the opposite hand. No world-X shortcut or negative model scale is used. Imported sample joint frames are consumed directly after Unity's OpenXR coordinate conversion; do not apply another OpenXR-to-Unity conversion.

Physical snapshots remain in `HandTrackingSource`; displayed poses are independent arrays with source-hand identity, reflection flag and input sequence. The skin driver reads only these displayed targets, not the live XR subsystem. Shoulder/elbow IK uses the displayed wrist, so the whole arm follows the mapped side. The renderer is not a scientific measurement of the limb surface.

Future logging must capture both streams plus calibration/condition and timing. Current Debug logs are development diagnostics only; they are not a durable research dataset.
