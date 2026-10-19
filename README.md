# Quest mirror-therapy research prototype

Current guided demo: **0.12.0**, branch **codex/participant-movement-recording**. App: **Quest Body Driven Avatar**.

**Participant recording:** select P01–P20 before the demo. Head, hand, estimated arm and displayed/mapped movement are recorded on the headset and copied over USB to **Desktop → Research Movement Data Demo**. Eye tracking is unavailable on Quest 3. See [movement data instructions](MOVEMENT_DATA.md) and [recording verification](MOVEMENT_DATA_VERIFICATION.md).

**Start with [the guided demo instructions](GUIDED_DEMO.md)** and [verification status](GUIDED_DEMO_VERIFICATION.md).

Welcome → five skin tones → seated/table calibration → normal familiarisation → automatic bilateral mirror movement → future-touch information → finish. Large head-gaze buttons use a visible two-second selection ring. Pinch selection and the previous manual-touch interaction are absent from this flow. Clinician glove integration is deferred.

Open with Unity **6000.6.2f1**, Android support installed. Use **Quest Prototype → Body Avatar → Configure**, then **Build**. The environment/UI are created at runtime in `Assets/Scenes/QuestBodyDrivenAvatar.unity`. Build output is `Builds/QuestBodyDrivenAvatar.apk`.

The legacy project remains untouched. Hardware acceptance of this new guided flow is pending; earlier wearer feedback does not establish validation of this release. Body/table calibration is estimation and requires visual confirmation.

## Earlier implementation references


- [M1 head tracking](MILESTONE1.md), [headset observations](MILESTONE1_HARDWARE_TEST.md)
- [M2 hand-tracking guide](MILESTONE2.md), [verification](MILESTONE2_VERIFICATION.md)
- [M3 seated embodiment](MILESTONE3.md), [verification](MILESTONE3_VERIFICATION.md)


- [Body avatar](BODY_AVATAR.md), [body calibration](BODY_CALIBRATION.md)
- [Milestone 4 movement mapping](MILESTONE4.md)
- [Previous manual touch experiment](MANUAL_TOUCH_DEMO.md) — inactive in the guided release

Saved basic-pilot tags and historical branches remain available. Older guides describe their respective versions; use the guided instructions for the current app. APKs, caches, and local platform credentials are not committed.
