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

## Device startup

Final APK installed and launched on the connected Quest 3. Logs reached seated settings initialization and OpenXR Hands subscription; no primitive-collider stripping errors appeared in the inspected process log. The existing Google Play AssetPackManager ClassNotFoundException still appears at startup; the app continues initializing, but that message remains unresolved.

Researcher configuration files were delivered for a left-to-right/hidden-source check and then restored to Normal with source shown. No running-loop condition acknowledgement appeared during this observation; do not count file delivery as a wearer or condition-switch acceptance test. No alignment was automatically requested. The app awaits the user wearing it and explicitly aligning.

Legacy working tree remained clean. Implementation and verification are pushed to the private modern repository on the M4 branch.

## 0.4.1 wearer-feedback fixes

User reported the table too low, disconnected-looking wrists and overly splayed elbows. These observations supersede any implied visual acceptance of 0.4.0.

- Added physical-pinch/head-gaze test buttons: three-second Realign, table up/down 2 cm. No new packages.
- Resting elbow pole now prefers down/back with small lateral bias. Fixed IK segment lengths remain unchanged.
- Removed wrist caps in locally generated models and joined the forearm to the exact deformed boundary. Matched 30 seam vertices per hand. The seam follows four-weight skinning, rather than assuming every vertex is rigidly attached to the wrist.
- Editor checks passed: pinch initial/reacquired closure rejection, release-to-arm one-shot clicking, front/back/outside button hit tests, less lateral elbow placement, CPU-skinned seam positions versus Unity BakeMesh under a rotated/translated frame, and exact arm seam positions. Existing reflection checks also passed.
- Rendered and inspected synthetic arm and control-panel previews. Corrected oversized text found in the first UI preview; final labels fit the buttons. These previews are not headset acceptance.
- Final Unity Android build and APK signature verification passed. Version 0.4.1 (5), same mirror app ID. SHA-256: `ea7e060efd5674535992f2ea08034b79f7ed7bee8fea3cbfc123b3cdf13a5898`.
- Remaining wearer checks: button highlighting/clicking, once-only countdown alignment, tabletop height match, wrist appearance through motion, elbows at rest, and mirrored conditions. Table height remains uncalibrated; no physical measurement was invented.

0.4.1 final APK installed and launched on Quest. The inspected startup log reached seated initialization and OpenXR Hands subscription; it showed the previously recorded AssetPackManager exception and no new control/mesh exceptions in that observation. Actual hand deformation, button operation and alignment still require wearer confirmation.
