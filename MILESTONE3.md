# Milestone 3: seated normal embodiment

This is an engineering prototype for seated CRPS mirror-therapy research. It is not yet a mirrored condition, clinical intervention, or validated embodiment system. The legacy project is untouched. Milestones 1 and 2 remain separate installed apps and Git branches.

## Scope

A plain environment, adjustable virtual table, simple procedural torso, estimated upper/lower arms, and tracked articulated hands. Physical left drives displayed left; physical right drives displayed right. Tracking frames are copied into independent displayed-pose arrays before rendering. No packages were added beyond Milestone 2.

Shoulders are anthropometric estimates relative to the seated head at explicit alignment. Elbows are analytic two-segment IK estimates, not measured arm positions. Segment lengths remain fixed; unreachable wrist targets hide the corresponding arm rather than stretching it or altering the tracked hand. The avatar is a simple geometric prototype, not a realistic anatomical model.

The body frame stays fixed after alignment. Recenter/origin changes or application pause require re-alignment. Invalid hand joints disappear through the existing tracking-validity pipeline. Hand tracking on a table, contacting skin, or near another person's hands still needs headset testing.

## Start and align

1. Open the Hands worktree in Unity 6000.6.2f1 on branch `codex/milestone-3-seated-embodiment`.
2. Use **Quest Prototype → Milestone 3 → Validate Seated Embodiment**, then **Build Seated APK**. The build scene is `Assets/Scenes/QuestSeatedEmbodiment.unity`.
3. Install `Builds/QuestSeatedEmbodiment.apk` with adb. Its app ID is `org.mirrortherapy.questprototype.seated`; launch **Quest Seated Embodiment** from Unknown Sources.
4. Sit at a real table with the Quest boundary/floor correctly set. Initially the body/table await alignment. A virtual table provides no physical support.
5. Look comfortably straight ahead. From the project directory, the researcher runs:

    python3 Tools/seated_setup.py --align

6. Inspect the alignment in the headset. The default table top is 74 cm above the tracked floor; this is a placeholder, not a measurement. Adjust without rebuilding, for example:

    python3 Tools/seated_setup.py --table-height-cm 72 --near-edge-cm 20

   Values above are examples only. Near edge is forward distance from the head's floor projection at alignment. Width/depth and arm dimensions are also adjustable; run `--help`. Running without options reads the current settings. Use `--serial DEVICE` if multiple devices are connected.
7. Compare the virtual top and edges with the real table under researcher supervision. Only after checking them, run:

    python3 Tools/seated_setup.py --confirm-table

   Changing dimensions or requesting alignment clears this confirmation. The current implementation is a coarse setup, not precision calibration. Keep trial data collection disabled; no research trial logger exists yet.
8. Re-align after headset removal/pause/recenter or a change in sitting position. Do not lean during the capture. Do not use guessed arm dimensions as participant measurements.

The researcher tool writes an atomic replacement of `seated-setup.json` in the app's external files directory. The app polls every half-second, checks dimension ranges, and consumes the explicit alignment request. Delivery alone does not prove acceptance; check the headset message. Settings persist, but body alignment must be requested each launch.

## Headset acceptance checklist (pending)

- Plain room, readable status, no duplicate old grid/blocks.
- Seated forward alignment accepted only with valid head tracking and floor origin.
- Head turns do not rotate the calibrated body/table.
- Both tracked hands align with physical hands; left remains left and right remains right.
- Arms connect plausibly; no stretching/snapping at normal resting positions. Adjust measured segment lengths if needed.
- Table height/edge updates take effect and confirmation resets when changed.
- Tracking loss hides invalid fingers/arms and recovery is predictable. Test near the tabletop and with hand overlap.
- Recenter and headset removal require re-alignment.
- No sustained judder or discomfort. Stop the test if alignment is confusing or uncomfortable.

## Required future work: automatic clinician-hand tracking

Automatic clinician-hand tracking is a requirement, not an optional manual-touch replacement. It is NOT implemented in this milestone. The current XR Hands subsystem exposes participant left/right hands; it does not give this application a separately identified third clinician hand. A rendered participant-hand reflection cannot establish clinician contact.

Before selecting a sensing approach, a dedicated feasibility milestone must demonstrate: stable clinician/participant identity, a shared calibrated coordinate frame, contact versus proximity, anatomical touch location, timestamps and latency, and safe failure during occlusion and hand overlap. Evaluate an independent clinician tracker/camera or explicit sensing solution against these requirements; do not claim ordinary Quest hand tracking provides them automatically. Mirroring visual contact will require the same participant-centered mapping used for movement.

Relevant primary documentation: [Meta hand tracking](https://developers.meta.com/vr/design/hands-technology/), [tracking limitations](https://developers.meta.com/vr/design/hands-limitations-mitigations/). The installed XR Hands 1.9.0 package source defines the current left/right API. No clinician-tracking hardware choice has been validated.
