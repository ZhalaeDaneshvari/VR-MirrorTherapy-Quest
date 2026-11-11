using System.Collections.Generic;
using System.IO;
using QuestResearch.Clinician;
using QuestResearch.Embodiment;
using QuestResearch.Hands;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Clinician-tracker test build. The accepted pilot scene, its package ID and its build settings are never
// modified: the test scene is a regenerated copy of the pilot scene plus the optional clinician feature, and the
// test APK installs beside the pilot under its own package ID.
public static class ClinicianTrackerSetup
{
    public const string ScenePath="Assets/Scenes/QuestBodyAvatarClinicianTest.unity";
    public const string AppId="org.mirrortherapy.questprototype.bodyavatar.cliniciantest";
    public const string ProductName="Mirror Pilot Clinician Test";
    public const string ApkPath="Builds/QuestBodyAvatarClinicianTest.apk";
    const string Generated="Assets/GeneratedHands";

    [MenuItem("Quest Prototype/Clinician Tracker (test)/Configure Test Scene")]
    public static void Configure()
    {
        if(AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/RightDisplayedHand.prefab")==null)
        {
            Debug.Log("[Clinician] Generated hands missing (fresh checkout): running the pilot's Body Avatar Configure first.");
            QuestBodyAvatarSetup.Configure();
        }
        if(File.Exists(ScenePath))AssetDatabase.DeleteAsset(ScenePath);
        if(!AssetDatabase.CopyAsset(QuestBodyAvatarSetup.ScenePath,ScenePath))throw new BuildFailedException("Cannot copy the pilot scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var origin=Object.FindAnyObjectByType<XROrigin>();
        if(origin==null)throw new BuildFailedException("Pilot scene has no XR Origin.");
        var item=new GameObject("Clinician tracker (test feature, off by default)");item.transform.SetParent(origin.transform,false);
        var link=item.AddComponent<ClinicianTrackerLink>();
        var feature=item.AddComponent<ClinicianTrackerFeature>();
        feature.Configure(link,origin.GetComponent<SeatedSetup>(),origin.GetComponent<HandTrackingSource>(),origin.GetComponent<SeatedAvatarDisplay>(),
            origin.CameraFloorOffsetObject.transform,origin.Camera.transform,
            AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/LeftDisplayedHand.prefab"),AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/RightDisplayedHand.prefab"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SeatedSkin.mat"));
        foreach(var c in new Object[]{feature,origin.GetComponent<SeatedSetup>(),origin.GetComponent<HandTrackingSource>(),origin.GetComponent<SeatedAvatarDisplay>()})
            if(c==null)throw new BuildFailedException("Pilot scene is missing a component the clinician feature needs.");
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Clinician] Test scene regenerated from the pilot scene: "+ScenePath);
    }

    [MenuItem("Quest Prototype/Clinician Tracker (test)/Validate Math")]
    public static void Validate()
    {
        var random=new System.Random(20261111);
        float R(float range)=>(float)(random.NextDouble()*2-1)*range;
        Quaternion Q(float degrees)=>Quaternion.Euler(R(degrees),R(180),R(degrees));
        // OpenVR -> Unity conversion must be one consistent mirror: convert(q*p) == convert(q)*convert(p).
        for(int i=0;i<100;i++)
        {
            var q=Quaternion.Euler(R(180),R(180),R(180));var p=new Vector3(R(1),R(1),R(1));var rotated=q*p;
            var f=new ClinicianTrackerFrame{RawPosition=p,RawRotation=q};var g=new ClinicianTrackerFrame{RawPosition=rotated};
            if(Vector3.Distance(f.Rotation*f.Position,g.Position)>1e-4f)throw new BuildFailedException("OpenVR-to-Unity conversion is not a consistent reflection.");
        }
        // Calibration: recover a known alignment and mount from noisy synthetic co-motion samples.
        var truth=new ClinicianCalibration{yawDegrees=37,translation=new Vector3(.4f,-.2f,1.1f),mountPosition=new Vector3(.03f,-.05f,.08f),mountRotation=Quaternion.Euler(10,-70,25)};
        var samples=new List<ClinicianCalibrationSolver.Sample>();
        for(int i=0;i<600;i++)
        {
            var tracker=new Pose(new Vector3(R(.25f),1+R(.15f),.4f+R(.2f)),Q(45));var wrist=truth.WristInSpace(tracker);
            samples.Add(new ClinicianCalibrationSolver.Sample{trackerPosition=tracker.position,trackerRotation=tracker.rotation,
                wristPosition=wrist.position+new Vector3(R(.003f),R(.003f),R(.003f)),wristRotation=wrist.rotation*Quaternion.Euler(R(1),R(1),R(1))});
        }
        if(!ClinicianCalibrationSolver.TrySolve(samples,"synthetic","right",out var solved,out var message))throw new BuildFailedException("Calibration solver rejected synthetic data: "+message);
        if(Mathf.Abs(Mathf.DeltaAngle(solved.yawDegrees,truth.yawDegrees))>.5f||Vector3.Distance(solved.translation,truth.translation)>.005f
            ||Vector3.Distance(solved.mountPosition,truth.mountPosition)>.005f||Quaternion.Angle(solved.mountRotation,truth.mountRotation)>2)
            throw new BuildFailedException("Calibration solver did not recover the synthetic transform: "+message);
        var fewer=samples.GetRange(0,ClinicianCalibrationSolver.MinimumSamples-1);
        if(ClinicianCalibrationSolver.TrySolve(fewer,"synthetic","right",out _,out _))throw new BuildFailedException("Calibration accepted too few samples.");
        // Cross-limb display reuses the pilot's reflection; it must be an involution about the body midline.
        var body=new Pose(new Vector3(.1f,.9f,.3f),Quaternion.Euler(0,25,0));
        for(int i=0;i<50;i++)
        {
            var pose=new Pose(new Vector3(R(.5f),1+R(.3f),R(.5f)),Q(90));var twice=MovementReflection.Reflect(MovementReflection.Reflect(pose,body),body);
            if(Vector3.Distance(twice.position,pose.position)>1e-4f||Quaternion.Angle(twice.rotation,pose.rotation)>.05f)throw new BuildFailedException("Reflection is not an involution.");
        }
        Debug.Log("[Clinician] Conversion, calibration recovery ("+message+"), sample gate and reflection checks PASS.");
    }

    [MenuItem("Quest Prototype/Clinician Tracker (test)/Build Test APK")]
    public static void Build()
    {
        Validate();Configure();
        // Snapshot everything the build touches so the pilot's own configuration is restored afterwards.
        string product=PlayerSettings.productName,id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool internet=PlayerSettings.Android.forceInternetPermission;var scenes=EditorBuildSettings.scenes;
        try
        {
            PlayerSettings.productName=ProductName;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,AppId);
            PlayerSettings.Android.forceInternetPermission=true;// TCP to the PC probe through adb reverse
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=ApkPath,target=BuildTarget.Android,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Clinician test build failed.");
            Debug.Log("[Clinician] Test APK built: "+ApkPath+" ("+AppId+"); the pilot app is not replaced by installing it.");
        }
        finally
        {
            PlayerSettings.productName=product;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,id);
            PlayerSettings.Android.forceInternetPermission=internet;EditorBuildSettings.scenes=scenes;AssetDatabase.SaveAssets();
        }
    }
}
