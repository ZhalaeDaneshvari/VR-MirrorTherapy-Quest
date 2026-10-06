# Milestone 2 source inventory

This worktree is based on the Milestone 1 branch. The following files include Unity metadata and generated project settings retained for reproducibility. Build outputs and caches are excluded.

```text
.gitignore
Assets/Editor.meta
Assets/Editor/QuestHandsSetup.cs
Assets/Editor/QuestHandsSetup.cs.meta
Assets/Editor/QuestProjectSetup.cs
Assets/Editor/QuestProjectSetup.cs.meta
Assets/Materials.meta
Assets/Materials/Blue.mat
Assets/Materials/Blue.mat.meta
Assets/Materials/Floor.mat
Assets/Materials/Floor.mat.meta
Assets/Materials/FloorGrid.mat
Assets/Materials/FloorGrid.mat.meta
Assets/Materials/Green.mat
Assets/Materials/Green.mat.meta
Assets/Materials/LeftHandCyan.mat
Assets/Materials/LeftHandCyan.mat.meta
Assets/Materials/Orange.mat
Assets/Materials/Orange.mat.meta
Assets/Materials/RightHandOrange.mat
Assets/Materials/RightHandOrange.mat.meta
Assets/Resources.meta
Assets/Scenes.meta
Assets/Scenes/QuestBootstrap.unity
Assets/Scenes/QuestBootstrap.unity.meta
Assets/Scenes/QuestHandsBootstrap.unity
Assets/Scenes/QuestHandsBootstrap.unity.meta
Assets/Scripts.meta
Assets/Scripts/Hands.meta
Assets/Scripts/Hands/HandSkeletonDisplay.cs
Assets/Scripts/Hands/HandSkeletonDisplay.cs.meta
Assets/Scripts/Hands/HandTrackingSource.cs
Assets/Scripts/Hands/HandTrackingSource.cs.meta
Assets/Scripts/Hands/HandTrackingStatus.cs
Assets/Scripts/Hands/HandTrackingStatus.cs.meta
Assets/Scripts/HeadTrackingDiagnostics.cs
Assets/Scripts/HeadTrackingDiagnostics.cs.meta
Assets/XR 1.meta
Assets/XR.meta
Assets/XR/Loaders.meta
Assets/XR/Loaders/OpenXRLoader.asset
Assets/XR/Loaders/OpenXRLoader.asset.meta
Assets/XR/Settings 1.meta
Assets/XR/Settings.meta
Assets/XR/Settings/OpenXR Editor Settings.asset
Assets/XR/Settings/OpenXR Editor Settings.asset.meta
Assets/XR/Settings/OpenXR Package Settings.asset
Assets/XR/Settings/OpenXR Package Settings.asset.meta
Assets/XR/XRGeneralSettingsPerBuildTarget.asset
Assets/XR/XRGeneralSettingsPerBuildTarget.asset.meta
CLASS_DEMO.md
FILES.md
MILESTONE1.md
MILESTONE1_HARDWARE_TEST.md
MILESTONE2_VERIFICATION.md
Packages/manifest.json
Packages/packages-lock.json
ProjectSettings/AudioManager.asset
ProjectSettings/ClusterInputManager.asset
ProjectSettings/DynamicsManager.asset
ProjectSettings/EditorBuildSettings.asset
ProjectSettings/EditorSettings.asset
ProjectSettings/GraphicsSettings.asset
ProjectSettings/InputManager.asset
ProjectSettings/MemorySettings.asset
ProjectSettings/MultiplayerManager.asset
ProjectSettings/NavMeshAreas.asset
ProjectSettings/PackageManagerSettings.asset
ProjectSettings/Physics2DSettings.asset
ProjectSettings/PhysicsCoreProjectSettings2D.asset
ProjectSettings/PresetManager.asset
ProjectSettings/ProjectAuditorSettings.asset
ProjectSettings/ProjectSettings.asset
ProjectSettings/ProjectVersion.txt
ProjectSettings/QualitySettings.asset
ProjectSettings/SceneTemplateSettings.json
ProjectSettings/TagManager.asset
ProjectSettings/TimeManager.asset
ProjectSettings/UnityConnectSettings.asset
ProjectSettings/VFXManager.asset
ProjectSettings/VersionControlSettings.asset
README.md
VERIFICATION.md
```

## Milestone 3 additions

- `Assets/Scripts/Embodiment/NormalHandMapping.cs`: separate displayed hand-pose copies, normal mapping only.
- `Assets/Scripts/Embodiment/SeatedSetup.cs`: validated dimensions, explicit fixed body frame and table alignment status.
- `Assets/Scripts/Embodiment/SeatedArmMath.cs`: analytic fixed-length two-segment IK with unreachable-target rejection.
- `Assets/Scripts/Embodiment/SeatedAvatarDisplay.cs`: procedural first-person upper body, tracked hand shapes and adjustable table.
- `Assets/Scripts/Embodiment/link.xml`: preserves primitive collider types requested internally by Unity during runtime shape creation.
- `Assets/Editor/QuestSeatedSetup.cs`: scene configuration, validation and dedicated APK build.
- `Assets/Scenes/QuestSeatedEmbodiment.unity`, `Assets/Materials/Seated*.mat`: minimal seated scene and materials.
- `Tools/seated_setup.py`: researcher-side ADB settings and explicit alignment command.
- `MILESTONE3.md`: setup and pending hardware acceptance checklist.

## Milestone 4 additions/changes

- `MovementRemapper.cs` replaces `NormalHandMapping.cs`: independent output poses, pure body-frame reflection math, both mapping directions and source visibility.
- `RemappedHandMesh.cs`: serialized anatomical joint references driven only by displayed poses.
- `ContinuousArmSurface.cs`: smooth tapered IK arm geometry.
- `SeatedAvatarDisplay.cs`: optional skinned hands/continuous arms, retains primitive fallback for older scene configuration.
- `SeatedSetup.cs`: condition configuration and single-consumption file handling.
- `Assets/Editor/QuestMirrorSetup.cs`: package model reconstruction, M4 scene/build, reflection checks and synthetic preview.
- `Assets/Scenes/QuestMovementMirror.unity`: isolated build scene.
- `Tools/seated_setup.py`: M3/M4 app selector, condition and source visibility controls.
- `MILESTONE4.md`, `THIRD_PARTY_ASSETS.md`: usage, limitations, asset provenance.
