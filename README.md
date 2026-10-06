# Quest mirror-therapy research prototype

Current branch: **codex/milestone-4-movement-mirroring**. Current app: **Quest Movement Mirror**.

Seated controller-free prototype with an adjustable table, anatomical hand meshes, estimated arms, normal movement, left-to-right reflection, and right-to-left reflection. Each mirrored condition can show both hands or only the opposite hand.

**Headset acceptance is pending for Milestone 4.** Earlier table visibility was confirmed, but precise real/virtual table alignment remains unverified. Automatic clinician-hand/contact sensing is required future work and is not implemented.

## Start here

1. Open `VR-MirrorTherapy-Quest-Hands` with Unity **6000.6.2f1**, Android support installed.
2. After package resolution, choose **Quest Prototype → Milestone 4 → Configure Movement Mirror**. This also reconstructs the ignored hand model assets from the pinned Unity XR Hands package.
3. Choose **Validate Movement Mirror**, then **Build Movement Mirror APK**.
4. Launch **Quest Movement Mirror** on Quest. Sit facing a real table and request explicit alignment before testing.

Read [MILESTONE4.md](MILESTONE4.md) for exact controls, reflection math, limitations and headset tests. [Verification](MILESTONE4_VERIFICATION.md) separates build/editor evidence from pending wearer checks. [Asset provenance](THIRD_PARTY_ASSETS.md) explains how fresh clones reconstruct the Unity hand meshes. No extra XR packages or legacy Oculus Integration were added.

The researcher command defaults to M4:

    python3 Tools/seated_setup.py --align
    python3 Tools/seated_setup.py --condition left-to-right --source-hand hide
    python3 Tools/seated_setup.py --condition normal

Only send alignment while the wearer is seated and looking forward. The app does not listen for voice commands. Version 0.4.1 also has a gaze-and-pinch **REALIGN (3 sec)** button and **Table ±2 cm** controls in the headset. The table cannot provide physical support.

## Earlier checkpoints

- [M1 head tracking](MILESTONE1.md), [headset observations](MILESTONE1_HARDWARE_TEST.md)
- [M2 hand-tracking guide](MILESTONE2.md), [verification](MILESTONE2_VERIFICATION.md)
- [M3 seated embodiment](MILESTONE3.md), [verification](MILESTONE3_VERIFICATION.md)

The legacy project remains untouched. Previous apps have separate identifiers and remain available. Switch to the corresponding Git branch for a previous milestone's configuration; the current project configuration builds M4. APKs and regenerable build caches are excluded from Git.
