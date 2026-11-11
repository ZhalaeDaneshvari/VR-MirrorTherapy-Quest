using System;
using System.IO;
using Meta.XR;
using QuestResearch.ClinicianProbe;
using QuestResearch.Hands;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

// Run through Tools/build_controller_probe.py: it restores the exact pre-build pilot settings.
public static class QuestClinicianControllerSetup
{
    public const string ScenePath="Assets/Scenes/QuestClinicianControllerProbe.unity";
    public const string AppId="org.mirrortherapy.questprototype.controllerprobe";
    public static void BuildIsolatedProbe()
    {
        if(!Application.isBatchMode)throw new BuildFailedException("Use Tools/build_controller_probe.py to preserve pilot settings.");
        QuestHandsSetup.Configure();
        var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        xr.GetFeature<MetaXRFeature>().enabled=true;
        bool detached=false;
        foreach(var f in xr.GetFeatures<OpenXRFeature>())
        {
            if(f.GetType().Name=="DetachedMetaQuestTouchPlusControllerProfile")
            {f.enabled=true;EditorUtility.SetDirty(f);detached=true;}
        }
        if(!detached)throw new BuildFailedException("Pinned Meta detached Touch Plus profile is missing.");
        EditorUtility.SetDirty(xr);
        var config=OVRProjectConfig.CachedProjectConfig;
        config.bodyTrackingSupport=OVRProjectConfig.FeatureSupport.None;
        config.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
        config.handTrackingFrequency=OVRProjectConfig.HandTrackingFrequency.LOW;
        OVRProjectConfig.CommitProjectConfig(config);
        if(!File.Exists(ScenePath)&&!AssetDatabase.CopyAsset(QuestHandsSetup.ScenePath,ScenePath))throw new BuildFailedException("Could not create diagnostic scene.");
        var scene=EditorSceneManager.OpenScene(QuestHandsSetup.ScenePath);
        var origin=UnityEngine.Object.FindAnyObjectByType<XROrigin>();
        foreach(var old in UnityEngine.Object.FindObjectsByType<HandTrackingStatus>())UnityEngine.Object.DestroyImmediate(old);
        foreach(var old in UnityEngine.Object.FindObjectsByType<TextMesh>())UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach(var old in UnityEngine.Object.FindObjectsByType<ClinicianControllerProbe>())UnityEngine.Object.DestroyImmediate(old);
        var manager=origin.GetComponent<OVRManager>()??origin.gameObject.AddComponent<OVRManager>();
        manager.SimultaneousHandsAndControllersEnabled=true;
        manager.launchSimultaneousHandsControllersOnStartup=true;
        manager.trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;
        var label=new GameObject("Probe instructions").AddComponent<TextMesh>();
        label.transform.SetParent(origin.Camera.transform,false);label.transform.localPosition=new Vector3(0,.22f,1.3f);
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.GetComponent<Renderer>().sharedMaterial=label.font.material;
        label.fontSize=48;label.characterSize=.018f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
        label.text="Controller probe starting...";
        var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.name="RIGHT controller grip - not skin contact";
        marker.transform.SetParent(origin.CameraFloorOffsetObject.transform,false);marker.transform.localScale=new Vector3(.035f,.035f,.09f);
        UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        string matPath="Assets/Materials/ClinicianProbeMarker.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(mat==null){mat=new Material(Shader.Find("Unlit/Color")){color=new Color(.85f,.2f,1)};AssetDatabase.CreateAsset(mat,matPath);}
        marker.GetComponent<Renderer>().sharedMaterial=mat;marker.SetActive(false);
        origin.gameObject.AddComponent<ClinicianControllerProbe>().Configure(origin.GetComponent<HandTrackingSource>(),origin.CameraFloorOffsetObject.transform,label,marker.transform);
        EditorSceneManager.SaveScene(scene,ScenePath);
        PlayerSettings.productName="Quest Clinician Controller Test";PlayerSettings.bundleVersion="0.1.0";PlayerSettings.Android.bundleVersionCode=1;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,AppId);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();
        VerifyGate();
        Directory.CreateDirectory("Builds");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/QuestClinicianControllerProbe.apk",target=BuildTarget.Android,options=BuildOptions.Development});
        if(result.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Controller probe build failed.");
        Debug.Log("[ClinicianProbe] APK build succeeded. Hardware feasibility NOT yet verified.");
    }
    static void VerifyGate()
    {
        var p=Vector3.zero;var q=Quaternion.identity;
        if(!ClinicianControllerProbe.Accept(true,true,3,.01,p,q)
            ||ClinicianControllerProbe.Accept(false,true,3,.01,p,q)
            ||ClinicianControllerProbe.Accept(true,false,3,.01,p,q)
            ||ClinicianControllerProbe.Accept(true,true,1,.01,p,q)
            ||ClinicianControllerProbe.Accept(true,true,3,.151,p,q)
            ||ClinicianControllerProbe.Accept(true,true,3,-1,p,q)
            ||ClinicianControllerProbe.Accept(true,true,3,.01,new Vector3(float.NaN,0,0),q)
            ||ClinicianControllerProbe.Accept(true,true,3,.01,p,new Quaternion()))throw new BuildFailedException("Controller validity gate failed.");
        Debug.Log("[ClinicianProbe] Focus, validity, stale and nonfinite pose gates PASS.");
    }
}
