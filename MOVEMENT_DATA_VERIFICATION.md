# Movement recording verification — 0.12.0 / Android code 23

2026-10-19, Unity 6000.6.2f1, Quest 3.

Verified:

- Android build and existing avatar/reflection/table checks passed.
- Recording tests passed: P01/P20 boundaries and invalid IDs, durable ordered writes, final chunk closure/count, refusal to overwrite a session, rejection of writes after closure, visible initialization and worker filesystem errors, physical-source sequence propagation, nonfinite pose sanitization.
- Guided UI pages rendered and interaction checks passed; participant picker was visually reviewed.
- Python transfer tests passed: checksum-verified copy, idempotent repeats, preservation of conflicting local files, exclusion of partial files, and final record count.
- CSV test passed: 157 pose rows per sample, invalid coordinates blank, no export overwrite.
- Real USB test passed using an isolated synthetic fixture in a temporary Quest Download subfolder and temporary Mac folder. The fixture was deleted afterward; it was never put in the participant Desktop dataset.
- Local macOS transfer LaunchAgent installed and observed exiting successfully. Desktop folder and manual sync command created.
- APK installed with `adb install -r`; package manager confirmed **0.12.0 / 23**; launch requested successfully.

APK SHA-256: `992f9536439c1df6c237a4625514fd7b83668ea16fbd95910ef0f9385388ebad`

Live headset smoke verification:

- A real participant-labelled session reached the Desktop through the transfer service/tool.
- The first inspected 51.41-second segment contained 1,543 samples (29.99 Hz observed), 18 events, valid head and both-hand tracking, correct 26-joint array lengths, finite JSON, and zero physical/mapped source-sequence mismatches.
- Stages included calibration, table/arm fit and initial normal-movement activity. The session was still active at this inspection; closure and the complete mirrored segment were not yet verified.
- No null-reference, fatal, writer-buffer or disk-write failure was found in the captured app log.
- No raw participant movement records are included in this repository.

Pending wearer/data acceptance:

- Physical/mapped/displayed trajectories compared through normal and mirrored stages.
- Observed sampling rate and recording overhead during a complete headset run.
- Recording failure UX under actual device storage exhaustion and recovery following a forced crash. Automated I/O tests are not equivalent to these device tests.

No eye tracking, clinical validation, or complete sensor-exposure timestamps are claimed. Nominal 30 Hz recording is a sampled view of provider/rendered state. The prior accepted guided flow remains in commit `76516f8` on `codex/guided-mirror-pilot`; no legacy repository files were changed.
