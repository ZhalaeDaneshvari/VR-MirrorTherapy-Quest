# Body-driven seated avatar — 0.6.0

The wearer gave an initial visual acceptance of the body probe's joint placement. This build integrates that estimated skeleton into the seated avatar; it is not yet wearer-accepted as a complete embodied experience.

## Changes

- Separate app: **Quest Body Driven Avatar**, `org.mirrortherapy.questprototype.bodyavatar`. Keeps the diagnostic and old mirror apps available.
- Meta upper-arm origins and lower-arm origins now drive shoulder/elbow placement. The old assumed elbow pole and table-support IK are bypassed in this scene.
- BodyArmTargets maps source-side estimates through the same calibrated body reflection used by hand targets. Left-to-right and right-to-left arm mapping preserve physical source identity. Body estimates remain distinct from displayed targets.
- XR hand targets remain authoritative for the hands/wrists. We do not clamp them to the table or move them to conceal overlap. Invalid/stale body data hides the arm; it does not silently substitute legacy IK.
- Torso position and width follow the mapped shoulder span when both estimates are valid.
- Denser, smoother arm surfaces with fuller upper-arm/forearm profiles and the existing CPU-skinned wrist seam. These are still generic procedural surfaces, not a personalized scan or a new photorealistic character asset. Hand meshes remain the pinned Unity XR Hands sample models.
- Warm skin, darker neutral table, restrained shirt colour, soft directional lighting and arm shadows improve shape/depth contrast in the plain environment.
- Release build excludes Meta's development-only operator binary. Existing diagnostic app remains a development build.

## Setup and test

In Unity: **Quest Prototype > Body Avatar > Configure**, then **Build**. Generated hand assets are reconstructed by the existing mirror setup pipeline. Keep Unity 6000.6.2f1 and committed package versions.

Launch the new app and allow body permission if prompted. Sit at the same table and use **REALIGN (3 sec)**: look at the button, pinch thumb/index, then look straight ahead during the countdown. Table +/-2 cm controls remain available. Alignment still must be done after recentering/resume when invalidated.

First test Normal movement. Check shoulders/elbows, wrist continuity, comfortable small wrist rotations, both hands resting on the surface, and hands lifted a little above it. Check for flicker, arm twisting, intersections, and shadows. The table placement is still a visual fit until checked against the real surface; changing skin shading cannot establish physical contact accuracy.

Researcher CLI: `python3 Tools/seated_setup.py --app bodyavatar --condition normal`. The same CLI supports `--align`, table dimensions and `--arm-fullness`. Legacy shoulder/segment settings and table-support IK flags are rejected for this app because they no longer drive its arms. Mirrored conditions are available for subsequent testing after Normal looks correct.

## Limits

Body tracking is camera-assisted estimation, not independently measured joint-centre ground truth. Generic surface proportions remain adjustable, not automatically measured. Hand/body providers have different update timing; a disagreement can still distort a forearm. No smoothing or artificial table collision has been introduced to mask that discrepancy. Actual Quest performance, appearance, tracking loss and mirrored-arm behaviour require wearer tests.

This scene does not install the probe's diagnostic recorder. The final research logger is still pending; do not use this build to collect scientific trials. Automatic clinician-hand sensing remains separate and unimplemented.

## Verification — 2026-10-08

Configure and release ARM64 IL2CPP build passed in Unity 6000.6.2f1. Existing hand seam, reflection, source visibility and invalidation checks passed during setup; body-joint coordinate conversion/reflection was checked with a translated and rotated participant frame. Synthetic seated render inspected at `/private/tmp/quest-body-avatar-preview.png` (not a participant screenshot).

APK SHA-256: `d42cd01b5569fde1882803e89efb58c1c2ca109ee7164b325f0f4eb2ddc61d34`. Installed on connected Quest 3, version 0.6.0/code 8. Copied the old app's table dimensions (height approximately 88 cm) and appearance multiplier 1.3 into the separate app, selected Normal, disabled legacy support IK and left alignment unrequested. The old file was only read.

At startup-log inspection the new app lacked headset focus and had stopped its window; live avatar/arm rendering has not been verified on the wearer yet. Meta OpenXR initialization occurred. Known startup messages remain: optional spatial-function warnings and the Unity `AssetPackManager` ClassNotFoundException previously seen in the prototype. The APK retains INTERNET plus hand/body permissions, but no longer includes the development operator foreground-service/media-projection permissions. Final visual fit and performance are pending wearer acceptance.
