# Desktop cross-limb embodiment demonstration

**Branch: `codex/class-embodiment-demo`. Simulated input only. No headset needed.**

This is a separate presentation prototype. The accepted Milestone 1 snapshot remains on `codex/milestone-1`; headset acceptance is still pending. This demo does not complete the research milestones.

## Open and present

1. Open the modern project in Unity **6000.6.2f1**.
2. Choose **Quest Prototype → Class Demo → Create or Open Demo Scene**. Alternatively open `Assets/ClassDemo/Scenes/ClassEmbodimentDemo.unity`.
3. Press the top-center **Play** button and select the **Game** tab. The mannequins are generated when Play starts; an empty editing view before Play is expected.
4. Maximize the Game panel for class (double-click its tab). A wide landscape view is recommended.
5. The cyan mannequin on the left represents **simulated physical input**. The gold arms on the right show the **displayed virtual body**. Both are seen from behind so left/right agree with the participant's perspective.
6. Select **Normal / congruent**, **LEFT → RIGHT**, and **RIGHT → LEFT** to compare mappings. In normal mode the left source arm animates while the right rests. In mirror modes the source-side virtual arm remains congruent: one simulated source drives both displayed arms.
7. Uncheck **Animate simulated source** to move the source arm using the height slider or Up/Down arrow keys. This is synthetic input, not hand tracking.
8. Enable **First-person avatar view** to look down at the virtual torso, arms and hands. This is a fixed desktop camera, not headset tracking.
9. Click **Start visual touch**. Pink markers follow the source and mapped forearms. Switch **Physical side** and toggle **Cross-limb touch** to compare congruent versus opposite-side visual contact. Click **Stop visual touch** to hide the markers.
10. Press Play again to stop. The original `QuestBootstrap` remains the only Android build scene; the existing APK does not contain this class demo scene.

## Two-minute demonstration script

“I'm investigating cross-limb virtual embodiment: whether seeing an arm respond to movement from the opposite physical arm can influence ownership or agency. Today this is a desktop simulation, not a tested headset experience.”

Show **Normal**: “The cyan figure represents physical input; the gold figure represents the avatar. Normal movement preserves the left/right relationship.”

Show **LEFT → RIGHT**, then **RIGHT → LEFT**: “Here the source arm drives the opposite virtual arm. Positions and orientations are reflected in a defined body frame, not by arbitrarily negating world coordinates. The source-side virtual arm also follows, so this example drives both arms.”

Enable **First-person avatar view**: “This is the intended viewing relationship: looking down at a body with arms and hands. A real participant will need calibration, reliable tracking and better anatomical alignment.”

Start touch: “The pink marker is a simple controllable visual stimulus. In a future session a clinician would touch the participant and manually trigger visual contact on the mapped arm. Here even the physical touch is only simulated.”

Ask: “Which cues would make this virtual arm feel like your own? Would showing both arms moving help or undermine that experience?”

## Implementation and limitations

- Procedural seated mannequin with torso, head, legs, arms, palms and fixed fingers. Original geometry; no legacy assets, third-party avatar files or extra packages imported.
- Analytic two-bone position IK with fixed outward elbow poles and reach clamping. Not a calibrated anatomical model. Fingers do not track or articulate.
- Input poses, movement mapping, and mannequin rendering are separate code responsibilities. All simulation poses use the authored participant-local frame (+X right, +Y up, +Z forward).
- Position reflection is `S p`; orientation reflection is `S R S`, `S = diag(-1,1,1)`. Our procedural hand geometry has mirrored thumb placement. This does not establish bone mappings for an arbitrary imported avatar.
- The yellow floor line marks the authored midline. No real participant calibration is implemented.
- Touch is a fixed repeating forearm stroke, independent of movement mapping. It is neither clinician sensing nor a measurement of synchronization. Pink markers identify corresponding locations; no virtual clinician hand is simulated.
- Two camera views are isolated using demo-only layer indices 8 and 9. No project layer definitions or Quest settings were changed.
- No automatic touch sensing, real hand tracking, researcher trial state, scientific logger, or clinical validation.
- The separate demo scene is deliberately excluded from the Quest bootstrap build configuration. Desktop playback does not verify Quest compatibility or performance.

## Verification

Use **Quest Prototype → Class Demo → Check Reflection Math** for position/orientation involution, the `S R S` identity, and all three mapping routes. Results are recorded in `CLASS_DEMO_VERIFICATION.md`.

## Git checkpoints

- `codex/milestone-1`: original head-tracked Quest bootstrap; hardware acceptance pending.
- `codex/class-embodiment-demo`: desktop presentation prototype, independently reviewable.

Do not merge this demonstration into the research application solely because it looks plausible in class.
