# Milestone 4 verification — 2026-10-06

## Verified in editor/build

- Unity 6000.6.2f1; unchanged XR package manifest/lock.
- OpenXR project validation reports zero issues.
- Reflection tests passed for 100 deterministic orientations/positions in a translated, 73-degree rotated body frame: expected sagittal position, double-reflection identity, proper orientation basis and reflected palm direction.
- Both mapping directions checked across all 26 joint poses, including opposite-only source hiding and invalid source-joint propagation.
- Normal output preserves tracked positions; output arrays do not alias provider arrays. Uncalibrated reflected targets are invalidated.
- Both imported models resolve exactly 26 explicit joints and contain 1,616 vertices each.
- Synthetic editor preview rendered and inspected for hand handedness, anatomical surfaces and continuous arms. This is an authored rest-pose preview, not a headset tracking test. Surface proportions and wrist seams remain generic and need wearer review.
- Android IL2CPP build succeeded and APK signature verification passed.
- App ID `org.mirrortherapy.questprototype.mirror`, version 0.4.0 (4), min SDK 32, target SDK 34.
- Final APK SHA-256: `312b886165759a83e5df8bf6f37c194a01d8d1dd8e1d6df41fed80efd70d1074`.
- `git diff --check` passed. Researcher CLI help verified.

## Pending

The full MILESTONE4.md acceptance checklist requires wearer testing: normal hand alignment, finger deformation, both mapping directions, body-centered orientation, single-shot seated alignment, source loss/recovery, table correspondence, comfort and sustained performance. Build success is not evidence of these outcomes. Automatic clinician-hand/contact sensing is not implemented.
