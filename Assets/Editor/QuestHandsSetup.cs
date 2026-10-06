using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using QuestResearch.Hands;

public static class QuestHandsSetup
{
    public const string ScenePath="Assets/Scenes/QuestHandsBootstrap.unity";
    public const string AppId="org.mirrortherapy.questprototype.hands";
    const string DataSourceType="UnityEngine.XR.Hands.OpenXR.HandTrackingDataSourceFeature";

    public static void Configure()
    {
        // Reuse the verified head-tracking baseline; add hand features only afterward.
        QuestProjectSetup.Configure();
        PlayerSettings.productName="Quest Hand Tracking Test";
        PlayerSettings.bundleVersion="0.2.0";PlayerSettings.Android.bundleVersionCode=2;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,AppId);
        var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        var hand=xr.GetFeature<HandTracking>();
        if(hand==null)throw new BuildFailedException("XR Hands feature unavailable after package import.");
        hand.enabled=true;
        var serializedHand=new SerializedObject(hand);
        serializedHand.FindProperty("m_AutoStartSubsystem").boolValue=true;
        serializedHand.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(hand);
        // XR Hands 1.9 keeps this feature class internal. Resolve its exact pinned type and
        // serialized property names; fail visibly if a future package changes the contract.
        var dataSource=xr.GetFeatures<OpenXRFeature>().Single(f=>f.GetType().FullName==DataSourceType);
        dataSource.enabled=true;
        var dataSettings=new SerializedObject(dataSource);
        foreach(string field in new[]{"m_LeftHandPreference","m_RightHandPreference"})
        {
            var property=dataSettings.FindProperty(field);
            if(property==null)throw new BuildFailedException("XR Hands data-source setting changed: "+field);
            property.enumValueIndex=Array.IndexOf(property.enumNames,"TrackedHand");
            if(property.enumValueIndex<0)throw new BuildFailedException("TrackedHand preference missing.");
        }
        dataSettings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(dataSource);EditorUtility.SetDirty(xr);
        if(!File.Exists(ScenePath))
        {
            if(!AssetDatabase.CopyAsset("Assets/Scenes/QuestBootstrap.unity",ScenePath))throw new BuildFailedException("Could not copy baseline scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var origin=UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            if(origin==null)throw new BuildFailedException("Baseline XR Origin missing.");
            var source=origin.gameObject.AddComponent<HandTrackingSource>();
            var display=origin.gameObject.AddComponent<HandSkeletonDisplay>();
            display.Configure(source,origin.CameraFloorOffsetObject.transform,
                Material("LeftHandCyan",new Color(.15f,.9f,1)),Material("RightHandOrange",new Color(1,.55f,.12f)));
            var label=new GameObject("Hand tracking status (diagnostics)").AddComponent<TextMesh>();
            label.transform.SetParent(origin.Camera.transform,false);
            label.transform.localPosition=new Vector3(0,.28f,1.15f);
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
            label.fontSize=64;label.characterSize=.022f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
            label.color=Color.white;label.text="M2 HAND TRACKING\nWaiting for OpenXR hand subsystem...";
            origin.gameObject.AddComponent<HandTrackingStatus>().Configure(source,label);
            EditorSceneManager.SaveScene(scene);
        }
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();Validate();
    }
    static Material Material(string name,Color color)
    {
        string path="Assets/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        material=new Material(Shader.Find("Unlit/Color")){name=name,color=color};
        AssetDatabase.CreateAsset(material,path);return material;
    }
    [MenuItem("Quest Prototype/Milestone 2/Validate Hands")]
    public static void Validate()
    {
        if(Application.unityVersion!="6000.6.2f1")throw new BuildFailedException("Use Unity 6000.6.2f1.");
        QuestProjectSetup.Validate();
        var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if(xr.GetFeature<HandTracking>()?.enabled!=true)throw new BuildFailedException("Hand Tracking Subsystem must be enabled.");
        if(!xr.GetFeatures<OpenXRFeature>().Any(f=>f.GetType().FullName==DataSourceType&&f.enabled))throw new BuildFailedException("Hand data source reporting must be enabled.");
        if(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)!=AppId)throw new BuildFailedException("Use the M2 application identifier to preserve the installed M1 app.");
        if(!File.Exists(ScenePath))throw new BuildFailedException("Hands scene missing.");
        CheckInvalidation();
        Debug.Log("[QuestHands] M2 settings and invalidation checks PASS. Headset tests still required.");
    }
    static void CheckInvalidation()
    {
        foreach(HandAvailability reason in new[]{HandAvailability.Unavailable,HandAvailability.Stale,HandAvailability.Suspended,HandAvailability.ControllerRejected})
        {
            var frame=new TrackingHandFrame();
            for(int i=0;i<frame.JointValid.Length;i++)frame.JointValid[i]=true;
            frame.Invalidate(reason);
            if(frame.JointValid.Any(v=>v)||frame.ValidJointCount!=0||frame.Availability!=reason)
                throw new BuildFailedException("Invalid tracking data must never remain displayable.");
        }
    }
    [MenuItem("Quest Prototype/Milestone 2/Build Hands APK")]
    public static void Build()
    {
        Validate();Directory.CreateDirectory("Builds");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/QuestHandTracking.apk",target=BuildTarget.Android,options=BuildOptions.Development});
        if(result.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("M2 Android build failed: "+result.summary.result);
        Debug.Log("[QuestHands] M2 APK build succeeded. Hardware validation pending.");
    }
}
