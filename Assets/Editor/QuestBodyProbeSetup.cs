using System.IO;
using System.Linq;
using Meta.XR;
using QuestResearch.BodyCalibration;
using QuestResearch.Hands;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.OpenXR;

public static class QuestBodyProbeSetup
{
    public const string ScenePath="Assets/Scenes/QuestBodyCalibrationProbe.unity";
    public const string AppId="org.mirrortherapy.questprototype.bodyprobe";
    [MenuItem("Quest Prototype/Body Calibration/Configure Probe")]
    public static void Configure()
    {
        QuestHandsSetup.Configure();
        var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        var feature=xr.GetFeature<MetaXRFeature>();
        if(feature==null)throw new BuildFailedException("Pinned Meta XR Feature missing.");
        feature.enabled=true;EditorUtility.SetDirty(feature);EditorUtility.SetDirty(xr);
        var config=OVRProjectConfig.CachedProjectConfig;
        config.bodyTrackingSupport=OVRProjectConfig.FeatureSupport.Supported;
        config.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.HandsOnly;
        OVRProjectConfig.CommitProjectConfig(config);
        PlayerSettings.productName="Quest Body Calibration Probe";
        PlayerSettings.bundleVersion="0.5.0";PlayerSettings.Android.bundleVersionCode=7;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,AppId);
        if(!File.Exists(ScenePath))
        {
            if(!AssetDatabase.CopyAsset(QuestHandsSetup.ScenePath,ScenePath))throw new BuildFailedException("Cannot copy hands baseline.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var origin=Object.FindAnyObjectByType<XROrigin>();
            foreach(var status in Object.FindObjectsByType<HandTrackingStatus>()) Object.DestroyImmediate(status);
            foreach(var text in Object.FindObjectsByType<TextMesh>()) Object.DestroyImmediate(text.gameObject);
            var source=origin.gameObject.AddComponent<BodyTrackingProbe>();
            var label=new GameObject("Body probe status").AddComponent<TextMesh>();
            label.transform.SetParent(origin.Camera.transform,false);label.transform.localPosition=new Vector3(0,.2f,1);
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
            label.fontSize=64;label.characterSize=.003f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
            label.text="BODY CALIBRATION PROBE - device required";
            const string materialPath="Assets/Materials/BodyProbeMarkers.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Unlit/Color")){color=new Color(.3f,1,.3f)};AssetDatabase.CreateAsset(material,materialPath);}
            origin.gameObject.AddComponent<BodyProbeDisplay>().Configure(source,origin.GetComponent<HandTrackingSource>(),origin.CameraFloorOffsetObject.transform,label,material);
            EditorSceneManager.SaveScene(scene);
        }
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();
        Debug.Log("[BodyProbe] Configured isolated diagnostic. No avatar or table settings changed.");
    }
    [MenuItem("Quest Prototype/Body Calibration/Build Probe APK")]
    public static void Build()
    {
        if(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)!=AppId)throw new BuildFailedException("Configure probe first.");
        QuestProjectSetup.Validate();
        if(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<MetaXRFeature>()?.enabled!=true)
            throw new BuildFailedException("Meta XR Feature disabled.");
        Directory.CreateDirectory("Builds");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.Android,
            locationPathName="Builds/QuestBodyCalibrationProbe.apk",options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Body probe build failed.");
        Debug.Log("[BodyProbe] Build succeeded; device acceptance still required.");
    }
}
