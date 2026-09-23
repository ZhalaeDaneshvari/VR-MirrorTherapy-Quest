using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

// Editor-only setup/build utility. Never reads or modifies the legacy repository.
public static class QuestProjectSetup
{
    const string ScenePath = "Assets/Scenes/QuestBootstrap.unity";
    const string AppId = "org.mirrortherapy.questprototype";

    public static void Configure()
    {
        if (Application.unityVersion != "6000.6.2f1")
            throw new BuildFailedException("Use the pinned Unity 6000.6.2f1 editor.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new BuildFailedException("Switch the active build target to Android first.");
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");
        Directory.CreateDirectory("Assets/XR");
        AssetDatabase.Refresh();
        PlayerSettings.companyName = "MirrorTherapyResearch";
        PlayerSettings.productName = "Quest Head Tracking Test";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = false;
        PlayerSettings.Android.forceInternetPermission = false;
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 2;
        EditorUserBuildSettings.buildAppBundle = false;

        var general = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        if (general == null)
        {
            general = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(general, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        }
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, general, true);
        if (!general.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            general.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var settings = general.SettingsForBuildTarget(BuildTargetGroup.Android);
        settings.InitManagerOnStart = true;
        if (!XRPackageMetadataStore.AssignLoader(settings.Manager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android))
            throw new BuildFailedException("Could not assign the OpenXR loader.");
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        foreach (var feature in xr.GetFeatures<OpenXRFeature>())
            feature.enabled = false;
        var quest = xr.GetFeature<MetaQuestFeature>();
        quest.enabled = true;
        // Built-in profile satisfies OpenXR validation; no controller actions or objects are used.
        xr.GetFeature<OculusTouchControllerProfile>().enabled = true;
        xr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        xr.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.None;
        xr.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
        // Quest-only device declaration, using the package's serialized device list.
        var serializedQuest = new SerializedObject(quest);
        var devices = serializedQuest.FindProperty("targetDevices");
        for (int i = 0; i < devices.arraySize; ++i)
        {
            var device = devices.GetArrayElementAtIndex(i);
            device.FindPropertyRelative("enabled").boolValue = device.FindPropertyRelative("manifestName").stringValue == "eureka";
        }
        serializedQuest.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(xr);
        EditorUtility.SetDirty(quest);
        AssetDatabase.SaveAssets();

        if (!File.Exists(ScenePath)) CreateScene();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Validate();
    }

    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        var originObject = new GameObject("XR Origin");
        var offset = new GameObject("Camera Offset");
        offset.transform.SetParent(originObject.transform, false);
        var cameraObject = new GameObject("Head Camera", typeof(Camera), typeof(AudioListener), typeof(TrackedPoseDriver));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(offset.transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 30f;
        camera.allowHDR = false;
        var origin = originObject.AddComponent<XROrigin>();
        origin.Origin = originObject;
        origin.CameraFloorOffsetObject = offset;
        origin.Camera = camera;
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        origin.CameraYOffset = 1.6f; // Device-origin fallback only; floor mode uses zero offset.
        var driver = cameraObject.GetComponent<TrackedPoseDriver>();
        driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        driver.positionInput = new InputActionProperty(new InputAction("Head Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        driver.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        driver.trackingStateInput = new InputActionProperty(new InputAction("Head Tracking State", InputActionType.Value, "<XRHMD>/trackingState", expectedControlType: "Integer"));
        originObject.AddComponent<HeadTrackingDiagnostics>();

        var references = new GameObject("World References");
        var floor = Material("Floor", new Color(0.30f, 0.33f, 0.36f));
        var grid = Material("FloorGrid", new Color(0.48f, 0.50f, 0.52f));
        Cube("Floor", new Vector3(0,-0.05f,0), new Vector3(10,0.1f,10), floor, references.transform);
        for (int i = -5; i <= 5; i++)
        {
            Cube("Grid X " + i, new Vector3(i,0.002f,0), new Vector3(0.015f,0.004f,10), grid, references.transform);
            Cube("Grid Z " + i, new Vector3(0,0.002f,i), new Vector3(10,0.004f,0.015f), grid, references.transform);
        }
        Cube("Near Left", new Vector3(-0.8f,0.5f,1.7f), new Vector3(0.35f,1,0.35f), Material("Blue",new Color(0.2f,0.5f,0.9f)),references.transform);
        Cube("Middle Right", new Vector3(0.9f,0.75f,2.8f), new Vector3(0.5f,1.5f,0.5f), Material("Orange",new Color(0.9f,0.5f,0.15f)),references.transform);
        Cube("Far Centre", new Vector3(0,1,4.2f), new Vector3(0.6f,2,0.6f), Material("Green",new Color(0.3f,0.7f,0.4f)),references.transform);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static Material Material(string name, Color color)
    {
        var material = new Material(Shader.Find("Unlit/Color")) { name = name, color = color };
        AssetDatabase.CreateAsset(material, "Assets/Materials/" + name + ".mat");
        return material;
    }

    static void Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
    }

    [MenuItem("Quest Prototype/Validate Milestone 1")]
    public static void Validate()
    {
        var issues = new List<OpenXRFeature.ValidationRule>();
        OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
        foreach (var issue in issues)
        {
            if (issue.error) Debug.LogError(issue.message);
            else Debug.LogWarning(issue.message);
        }
        if (issues.Any(issue => issue.error)) throw new BuildFailedException("OpenXR validation failed.");
        var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (general == null || !general.InitManagerOnStart || !general.Manager.activeLoaders.OfType<OpenXRLoader>().Any())
            throw new BuildFailedException("OpenXR startup/loader is missing.");
        Debug.Log($"M1 validation: {issues.Count} OpenXR issues; hardware tests still required.");
    }

    [MenuItem("Quest Prototype/Build Milestone 1 APK")]
    public static void Build()
    {
        Validate();
        Directory.CreateDirectory("Builds");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/QuestHeadTracking.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        if (result.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("APK build failed: " + result.summary.result);
        Debug.Log($"M1 APK built: {result.summary.totalSize} bytes. Not yet tested on Quest hardware.");
    }
}
