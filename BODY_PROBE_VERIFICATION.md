# Body probe verification — 2026-10-08

## Verified

- Unity 6000.6.2f1 Apple Silicon imports Meta Core 207.0.0 with the dependencies documented in BODY_PROBE.md. OpenXR 1.18.0 and XR Hands 1.9.0 retained.
- Configure succeeded, scripts compiled, OpenXR validation reported zero issues, and Android ARM64 IL2CPP development build succeeded.
- APK identifier `org.mirrortherapy.questprototype.bodyprobe`, version 0.5.0, code 7. SHA-256 `8ae3520ec24407c054b81c3a8b0fff0151ac0d8d8e8f7e61b8204e0dcd8bf30d`.
- Final APK manifest contains body and hand tracking permissions and UnityPlayerGameActivity.
- APK installed and launched on the connected Quest 3. Existing mirror application was not replaced and its saved table settings were not written.
- Device log confirmed Meta OpenXR initialization, OpenXR Hands subscription and accepted high-fidelity request.
- An early 256-sample diagnostic snapshot contained 120 Valid/High body samples, 106 Calibrating/High samples, 28 unavailable samples and 2 suspended samples. The last sample had all eight body joints valid and both XR hand wrists valid, body confidence 1.0 and origin revision 5. These are provider reports, not independent anatomical accuracy measurements. No participant pose records are committed here.

## Not yet verified

Participant-confirmed elbow/shoulder alignment, agreement at the physical table, extended occlusion behaviour, long-run stability/performance, visible text/marker usability, measured-height override, and rigorous focus/recenter recovery. No personalized avatar or automatic clinician-hand sensing is implemented by this probe. Do not mark the embodiment milestone passed.

## Warnings and limitations

- Meta's editor validator warns about GameActivity because its pre-build manifest check returns false without a custom manifest. PlayerSettings specifies GameActivity and the final APK manifest was checked for UnityPlayerGameActivity. Do not add a duplicate custom activity to silence this warning.
- Meta XR Feature logs unsupported optional spatial/anchor function warnings at startup. This probe does not use those functions; body and hand sources subsequently produced data.
- This is a Development build. Meta Core automatically includes its operator development library and generates local debugger resources. DevAgent and Immersive Debugger are disabled in generated settings. The APK includes SDK/development-related internet and foreground-service permissions in addition to hand/body permissions; this is not a minimal release research build. Local debugger credentials and machine-specific generated metadata are excluded from Git. Review/remove development-only tooling before participant study distribution.
- Height remains automatic until the participant provides a known height. Valid calibration alone is not proof that its inferred height or proportions are correct.
- Diagnostic writer flushes each line, but does not guarantee durable persistence through power loss. This is not the final trial logger.

Logs used: `/private/tmp/quest-body-configure.log`, `/private/tmp/quest-body-build.log`, app-scoped ADB logcat and app-local diagnostic records. APK is an ignored local build artifact; reproduce it with QuestBodyProbeSetup.
