# Milestone 2 verification — 2026-10-06

**Implementation and build verified. Quest installation and hand-tracking acceptance pending.**

## Automatically verified

- Unity 6000.6.2f1 Apple Silicon resolved XR Hands 1.9.0 and compiled the project.
- OpenXR Android validation: zero issues.
- Invalidation checks passed for unavailable, stale, suspended and controller-rejected snapshots: valid-joint flags are cleared.
- Final Android Development APK build returned Success and process exit 0.
- APK signature verified using Android apksigner v2.
- APK size: 33,398,934 bytes.
- SHA-256: `ee2da721caad419962e9d7e41a2bf28a6de10ced7782207db62ff3045d7cc5c5`.
- Application: `org.mirrortherapy.questprototype.hands`, version 0.2.0 / code 2; GameActivity; ARM64 only; minimum API 32 / target API 34.
- Actual manifest includes hand-tracking permission and optional handtracking feature, plus the existing OpenXR permissions and development internet permission. No camera, microphone, or storage permission was present in the inspected manifest.
- The legacy repository and the class-demo working copy were not modified. The original QuestBootstrap scene is unchanged in this branch.

## Build issues encountered

- Initial build failed because the Mac ran out of disk space while writing IL2CPP debug symbols. Only previous generated Android build intermediates under the modern project's `Library/Bee` were removed; the source and saved Milestone 1 APK were preserved. Retrying succeeded. Less than 1 GB remained free after the build; future builds may require more free space.
- An authored editor-only obsolete API warning was fixed by replacing FindFirstObjectByType with FindAnyObjectByType before the final build.
- The native compiler emitted the existing `'-x c++' after last input file has no effect` warning in the initial build. This was not an authored C# error.
- No headset behavior is inferred from build success. The USB connection was unauthorized at the last check; no M2 installation was attempted at that point.

Logs: `/private/tmp/quest-m2-configure.log`, `/private/tmp/quest-m2-build.log` (failed disk-space attempt), `/private/tmp/quest-m2-build-retry.log` (successful build).

## Hardware evidence still required

See README checklist: independent hand/wrist movement, finger articulation, left/right identity, controller-free behavior, occlusion/loss/recovery, pause/resume, source reporting, and visual alignment. The hand provider can estimate hidden joints; a valid pose is not proof of direct observation. No elbow or arm tracking is claimed.
