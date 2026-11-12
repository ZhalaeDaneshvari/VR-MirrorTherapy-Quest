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
        var left=AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/LeftDisplayedHand.prefab");
        var right=AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/RightDisplayedHand.prefab");
        var display=origin.GetComponent<SeatedAvatarDisplay>();
        foreach(var c in new Object[]{left,right,display,origin.GetComponent<SeatedSetup>(),origin.GetComponent<HandTrackingSource>()})
            if(c==null)throw new BuildFailedException("Pilot scene or generated hands are missing something the clinician test needs.");
        // The checked-in pilot scene holds the hand-prefab object IDs of the machine that first generated them;
        // regenerated prefabs keep their GUIDs but get new IDs. Re-point the copy (never the pilot scene) at the
        // local prefabs, as QuestMirrorSetup does for its own scene, so participant hands render in the test app.
        display.ConfigureMeshes(left,right);
        var item=new GameObject("Clinician tracker (test feature, off by default)");item.transform.SetParent(origin.transform,false);
        var link=item.AddComponent<ClinicianTrackerLink>();
        var feature=item.AddComponent<ClinicianTrackerFeature>();
        feature.Configure(link,origin.GetComponent<SeatedSetup>(),origin.GetComponent<HandTrackingSource>(),display,
            origin.CameraFloorOffsetObject.transform,origin.Camera.transform,left,right,AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SeatedSkin.mat"));
        // Test build only: brief hold of the participant's resting displayed arm while the clinician hand occludes it.
        var remapper=origin.GetComponent<MovementRemapper>();
        if(remapper==null)throw new BuildFailedException("Pilot scene has no MovementRemapper.");
        var hold=item.AddComponent<ParticipantArmTouchHold>();
        hold.Configure(remapper,origin.GetComponent<HandTrackingSource>(),origin.GetComponent<SeatedSetup>(),feature);
        feature.ConfigureHold(hold);
        // Pilot mode: affected-arm prompt, clinician touch stage, clinician hand only in the mirror/touch stages.
        var pilot=origin.GetComponent<PilotWalkthrough>();
        if(pilot==null)throw new BuildFailedException("Pilot scene has no PilotWalkthrough.");
        feature.ConfigurePilot(pilot);
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
        // Contact correction: a point inside a straight, level forearm needs lift; points above, beside or beyond it do not.
        var top=new Vector3[21];for(int k=0;k<21;k++)top[k]=new Vector3(0,.80f,.30f+k*.0125f);
        float inside=ClinicianTrackerFeature.Penetration(new Vector3(0,.79f,.42f),top);
        if(Mathf.Abs(inside-.013f)>.0005f||ClinicianTrackerFeature.Penetration(new Vector3(0,.81f,.42f),top)!=0
            ||ClinicianTrackerFeature.Penetration(new Vector3(.05f,.79f,.42f),top)!=0||ClinicianTrackerFeature.Penetration(new Vector3(0,.79f,.60f),top)!=0)
            throw new BuildFailedException("Forearm contact correction check failed (inside lift "+inside+").");
        // Carry-over: after a Quest origin shift D, a calibrated tracker pose must map to D applied to its old place.
        var carried=new ClinicianCalibration{yawDegrees=40,translation=new Vector3(.2f,0,.5f),stageValid=true,stagePosition=new Vector3(1,0,-2),stageRotation=Quaternion.Euler(0,15,0)};
        var probe=new Pose(new Vector3(.3f,.8f,-.1f),Quaternion.Euler(10,70,-5));var before=carried.TrackerInSpace(probe);
        var shiftRotation=Quaternion.Euler(0,-35,0);var shiftOffset=new Vector3(-.4f,0,.9f);// D: new = shiftRotation*old + shiftOffset
        var movedStage=new Pose(shiftRotation*carried.stagePosition+shiftOffset,shiftRotation*carried.stageRotation);
        if(!carried.FollowStage(movedStage,out _,out _))throw new BuildFailedException("Carry-over rejected a pure yaw shift.");
        var after=carried.TrackerInSpace(probe);var expected=new Pose(shiftRotation*before.position+shiftOffset,shiftRotation*before.rotation);
        if(Vector3.Distance(after.position,expected.position)>1e-4f||Quaternion.Angle(after.rotation,expected.rotation)>.05f)
            throw new BuildFailedException("Calibration carry-over does not follow the origin shift.");
        var tilted=new Pose(movedStage.position,Quaternion.Euler(8,0,0)*movedStage.rotation);
        if(carried.FollowStage(tilted,out _,out _))throw new BuildFailedException("Carry-over accepted a tilted shift.");
        Debug.Log("[Clinician] Conversion, calibration recovery ("+message+"), sample gate, reflection, contact-lift and carry-over checks PASS.");
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
