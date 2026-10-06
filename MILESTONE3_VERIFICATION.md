# Milestone 3 verification — 2026-10-06

- Unity 6000.6.2f1 Android IL2CPP build succeeded.
- Project OpenXR validation: zero reported issues.
- Editor checks passed: parameter bounds/NaN rejection, fixed IK segment lengths, unreachable-target rejection, and physical/displayed pose array independence.
- APK signature verification passed. App ID `org.mirrortherapy.questprototype.seated`, version 0.3.0 (3), min SDK 32, target SDK 34.
- Final APK SHA-256: `2ab5d91e63c7495de52b6299dc4fccc76dba158b804e1ec278cd02b3294df30a`.
- First device launch exposed stripped SphereCollider/CapsuleCollider types requested by runtime CreatePrimitive. Added an explicit linker preservation file and rebuilt; do not use the first APK.
- Researcher tool can read the device's settings file. No alignment or table confirmation was requested automatically.
- Remaining native compiler warning: `-x c++ after last input file has no effect` from generated Unity build commands. No authored C# compilation errors.
- Full seated alignment, table adjustment, arm appearance, occlusion, resume and performance acceptance remain pending wearer testing. This is not evidence of clinical suitability or automatic clinician-hand sensing.

Corrected APK installed and launched on the connected Quest 3. Startup reached the seated settings initialization and OpenXR Hands subscription; the earlier primitive/collider errors did not recur in the inspected process log. Startup still logged a missing optional Google Play AssetPackManager class; the application continued starting, but this message is not yet resolved. A head-tracking-unavailable observation occurred while awaiting wearer testing. No calibration was triggered. Neither startup nor a successful APK build establishes visual alignment or tracking acceptance.
