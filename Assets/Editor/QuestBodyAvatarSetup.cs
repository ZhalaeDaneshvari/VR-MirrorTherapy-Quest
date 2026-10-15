using System.IO;
using Meta.XR;
using QuestResearch.BodyCalibration;
using QuestResearch.Embodiment;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.Hands;

public static class QuestBodyAvatarSetup
{
    public const string ScenePath="Assets/Scenes/QuestBodyDrivenAvatar.unity";
    public const string AppId="org.mirrortherapy.questprototype.bodyavatar";
    [MenuItem("Quest Prototype/Body Avatar/Configure")]
    public static void Configure()
    {
        QuestMirrorSetup.Configure();
        var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        xr.GetFeature<MetaXRFeature>().enabled=true;EditorUtility.SetDirty(xr.GetFeature<MetaXRFeature>());
        var config=OVRProjectConfig.CachedProjectConfig;
        config.bodyTrackingSupport=OVRProjectConfig.FeatureSupport.Supported;
        config.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.HandsOnly;
        OVRProjectConfig.CommitProjectConfig(config);
        if(!File.Exists(ScenePath)&&!AssetDatabase.CopyAsset(QuestMirrorSetup.ScenePath,ScenePath))throw new BuildFailedException("Cannot create body-avatar scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var origin=Object.FindAnyObjectByType<XROrigin>();
        var setup=origin.GetComponent<SeatedSetup>();
        var body=origin.GetComponent<BodyTrackingProbe>()??origin.gameObject.AddComponent<BodyTrackingProbe>();
        var targets=origin.GetComponent<BodyArmTargets>()??origin.gameObject.AddComponent<BodyArmTargets>();
        targets.Configure(body,setup,origin.CameraFloorOffsetObject.transform);
        origin.GetComponent<SeatedAvatarDisplay>().ConfigureBodyTargets(targets);
        origin.GetComponent<SeatedTestControls>().enabled=false;
        var pilot=origin.GetComponent<PilotWalkthrough>()??origin.gameObject.AddComponent<PilotWalkthrough>();
        pilot.Configure(setup,origin.GetComponent<QuestResearch.Hands.HandTrackingSource>(),body,origin.Camera.transform,
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SetupButton.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SetupButtonHover.mat"),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        // Brown table and light gray floor, as requested.
        Surface("SeatedTable",new Color(.30f,.16f,.075f),.18f);
        Surface("SeatedSkin",new Color(.69f,.46f,.32f),.28f);
        Surface("SeatedShirt",new Color(.12f,.17f,.20f),.10f);
        Surface("SeatedFloor",new Color(.72f,.73f,.74f),.05f);
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.72f,.76f,.8f);
        RenderSettings.ambientEquatorColor=new Color(.45f,.47f,.49f);
        RenderSettings.ambientGroundColor=new Color(.28f,.27f,.26f);
        foreach(var light in Object.FindObjectsByType<Light>())
        {light.intensity=.85f;light.color=new Color(1,.95f,.9f);light.shadows=LightShadows.Soft;light.shadowStrength=.45f;light.shadowBias=.035f;light.transform.rotation=Quaternion.Euler(50,-30,0);}
        QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.Medium;QualitySettings.shadowDistance=4;
        origin.Camera.backgroundColor=new Color(.60f,.64f,.67f);
        EditorSceneManager.SaveScene(scene);
        PlayerSettings.productName="Quest Body Driven Avatar";PlayerSettings.bundleVersion="0.10.0";PlayerSettings.Android.bundleVersionCode=20;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,AppId);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();Validate();
    }
    static void Surface(string name,Color color,float smoothness)
    {var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/"+name+".mat");m.color=color;m.SetFloat("_Glossiness",smoothness);EditorUtility.SetDirty(m);}
    public static void Validate()
    {
        QuestProjectSetup.Validate();
        if(ForearmTouchStroke.CountdownAt(0)!=3||ForearmTouchStroke.CountdownAt(.999)!=3||ForearmTouchStroke.CountdownAt(1)!=2||ForearmTouchStroke.CountdownAt(2)!=1||ForearmTouchStroke.CountdownAt(3)!=0
            ||ForearmTouchStroke.ProgressAt(-1,4)!=0||ForearmTouchStroke.ProgressAt(2,4)!=.5f||ForearmTouchStroke.ProgressAt(5,4)!=1)
            throw new BuildFailedException("Touch countdown/progress boundary checks failed.");
        var heights=new System.Collections.Generic.List<float>();
        for(int i=0;i<40;i++)heights.Add(.741f+(i%3-1)*.001f);
        heights[0]=.1f;heights[39]=1.2f;
        if(!RestingSurfaceCalibration.TryEstimate(heights,out float estimated)||Mathf.Abs(estimated-.740f)>.0011f)
            throw new BuildFailedException("Resting-surface median rejected a stable window with outliers.");
        heights[10]=float.NaN;
        if(RestingSurfaceCalibration.TryEstimate(heights,out _))throw new BuildFailedException("Nonfinite surface sample accepted.");
        heights.Clear();for(int i=0;i<40;i++)heights.Add(.6f+i*.002f);
        if(RestingSurfaceCalibration.TryEstimate(heights,out _))throw new BuildFailedException("Moving hands accepted as stable table.");
        Vector3 shoulder=new Vector3(-.2f,1.1f,0),wrist=new Vector3(-.2f,.78f,.3f);
        if(!SeatedArmMath.TryElbow(shoulder,wrist,.30f,.26f,Vector3.down,out var elbow)
            || Mathf.Abs(Vector3.Distance(shoulder,elbow)-.30f)>.0001f || Mathf.Abs(Vector3.Distance(elbow,wrist)-.26f)>.0001f)
            throw new BuildFailedException("Fixed-length arm fitting stretched a segment.");
        if(SeatedArmMath.TryElbow(shoulder,shoulder+Vector3.forward,.30f,.26f,Vector3.down,out _))
            throw new BuildFailedException("Unreachable arm was stretched.");
        var frame=new Pose(new Vector3(2,.4f,-3),Quaternion.Euler(0,67,0));
        var physical=new Pose(frame.position+frame.rotation*new Vector3(-.2f,.7f,.3f),Quaternion.identity);
        var normal=BodyArmTargets.ToBody(physical,false,frame);var reflected=BodyArmTargets.ToBody(physical,true,frame);
        if(Vector3.Distance(normal,new Vector3(-.2f,.7f,.3f))>.00001f||Vector3.Distance(reflected,new Vector3(.2f,.7f,.3f))>.00001f)
            throw new BuildFailedException("Body target reflection/coordinate conversion failed.");
        // Edge-crossing triangles must be blocked even if their vertices begin outside the footprint.
        var table=new SeatedParameters();var random=new System.Random(943);
        for(int n=0;n<500;n++)
        {
            var points=new Vector3[3];
            for(int j=0;j<3;j++)points[j]=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble(),(float)random.NextDouble());
            bool overlap=ContinuousArmSurface.OverlapsTable(points[0],points[1],points[2],table);
            ContinuousArmSurface.EnforceTableTop(points,new[]{0,1,2},table);
            if(overlap)foreach(var point in points)if(point.y<table.tableHeight+.00099f)throw new BuildFailedException("Hard tabletop constraint left an intersecting triangle.");
        }
        var rest=new TableRestState();
        float lift=rest.UpdateSurface(.87f,.91f,.88f,true,.014f);
        if(!rest.Active || Mathf.Abs(lift-.0105f)>.00001f)throw new BuildFailedException("Rest entry failed.");
        float a=rest.UpdateSurface(.841f,.91f,.88f,true,.014f);
        float b=rest.UpdateSurface(.839f,.91f,.88f,true,.014f);
        if(b<a || Mathf.Abs(b-a-.002f)>.00001f)throw new BuildFailedException("Rest support discontinuity at old 4 cm boundary.");
        rest.BeginFrame(true,.1f);
        if(rest.FlatBlend!=1)throw new BuildFailedException("Palm blend failed.");
        rest.UpdateSurface(.89f,.94f,.88f,true,.014f);
        if(!rest.Active)throw new BuildFailedException("Rest hysteresis failed.");
        rest.UpdateSurface(.91f,.96f,.88f,true,.014f);
        if(rest.Active)throw new BuildFailedException("Raised hand did not release.");
        if(rest.UpdateSurface(.82f,.87f,.88f,true,.014f)!=0 || rest.FlatBlend!=0)
            throw new BuildFailedException("Below-table hand must bypass support.");
        rest.UpdateSurface(.87f,.91f,.88f,false,.014f);
        if(rest.Active||rest.Offset!=0)throw new BuildFailedException("Ineligible hand acquired support.");
        rest.UpdateSurface(float.NaN,.91f,.88f,true,.014f);
        if(rest.Active||rest.Offset!=0)throw new BuildFailedException("Invalid sample retained support.");
        Debug.Log("[BodyAvatar] Body-frame mapping, rest continuity, hysteresis, release and invalid-data checks PASS. Wearer fit acceptance pending.");
    }
    [MenuItem("Quest Prototype/Body Avatar/Build")]
    public static void Build()
    {
        Validate();
        if(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)!=AppId)throw new BuildFailedException("Configure body avatar first.");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/QuestBodyDrivenAvatar.apk",target=BuildTarget.Android,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Body avatar build failed.");
        Debug.Log("[BodyAvatar] APK build succeeded.");
    }
    public static void BuildAndPreview(){Build();Preview();QuestMirrorSetup.RenderControlPreview();PilotPreview.Render();}
    public static void Preview()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var space = new GameObject("Preview tracking space").transform;
        var left = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RemappedHandMesh>("Assets/GeneratedHands"+"/LeftDisplayedHand.prefab"), space);
        var right = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RemappedHandMesh>("Assets/GeneratedHands"+"/RightDisplayedHand.prefab"), space);
        var pose = new DisplayedHandPose();
        var mirrored = new DisplayedHandPose();
        var wrist = left.Joints[XRHandJointID.Wrist.ToIndex()];
        Vector3 root = wrist.position;
        Quaternion inverse = Quaternion.Inverse(wrist.rotation);
        for (int i=0;i<pose.Joints.Length;i++)
        {
            pose.Valid[i]=mirrored.Valid[i]=true;
            pose.Joints[i]=new Pose(new Vector3(-.19f,.94f,.35f)+inverse*(left.Joints[i].position-root), inverse*left.Joints[i].rotation);
            mirrored.Joints[i]=MovementReflection.Reflect(pose.Joints[i],Pose.identity);
        }
        // Synthetic flat-rest geometry check (does not substitute for runtime tracking or wearer testing).
        var tableParameters=new SeatedParameters { tableHeight=.88f };
        foreach(var pair in new[]{(mesh:left,pose:pose),(mesh:right,pose:mirrored)})
        {
            var h=pair.pose;int w=XRHandJointID.Wrist.ToIndex();var pivot=h.Joints[w].position;
            var along=h.Joints[XRHandJointID.MiddleProximal.ToIndex()].position-pivot;
            var across=h.Joints[XRHandJointID.IndexProximal.ToIndex()].position-h.Joints[XRHandJointID.LittleProximal.ToIndex()].position;
            var normal=Vector3.Cross(along,across).normalized;if(normal.y<0)normal=-normal;
            var flat=Quaternion.FromToRotation(normal,Vector3.up);
            for(int i=0;i<h.Joints.Length;i++)h.Joints[i]=new Pose(pivot+flat*(h.Joints[i].position-pivot),flat*h.Joints[i].rotation);
            pair.mesh.Apply(h,space,true);
            float lowest=pair.mesh.LowestOverTable(space,tableParameters);
            if(!float.IsFinite(lowest))throw new BuildFailedException("Synthetic hand has no table surface.");
            for(int i=0;i<h.Joints.Length;i++)h.Joints[i].position+=Vector3.up*(tableParameters.tableHeight+.0005f-lowest);
            pair.mesh.Apply(h,space,true);
            if(Mathf.Abs(pair.mesh.LowestOverTable(space,tableParameters)-tableParameters.tableHeight-.0005f)>.0001f)
                throw new BuildFailedException("Hand skin contact translation failed.");
        }
        var skin=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SeatedSkin.mat");
        for(int side=0;side<2;side++)
        {
            var shoulder=new Vector3(side==0?-.19f:.19f,1.03f,-.07f);
            var target=side==0?pose:mirrored;
            var wristPose=target.Joints[XRHandJointID.Wrist.ToIndex()];
            var elbow=new Vector3(side==0?-.21f:.21f,.925f,.09f); // Synthetic resting pose, not wearer measurements.
            var arm=new GameObject("Preview arm").AddComponent<ContinuousArmSurface>();arm.Initialize(skin,(side==0?left:right).WristSeamPoints,(side==0?left:right).WristSeamNormals);
            arm.Apply(shoulder,elbow,wristPose.position,wristPose.rotation);
            arm.ResolveSmallTableOverlap(tableParameters);
            if(!arm.TryTopForearm(0,out var strokeStart)||!arm.TryTopForearm(1,out var strokeEnd)||strokeEnd.z<=strokeStart.z)
                throw new BuildFailedException("Touch path must run elbow toward wrist on both arms.");
            arm.transform.SetPositionAndRotation(new Vector3(2,0,-3),Quaternion.Euler(0,63,0));
            if(!arm.TryTopForearm(.5f,out var moved)||!float.IsFinite(moved.x))throw new BuildFailedException("Touch path lost transformed arm.");
            arm.gameObject.SetActive(false);
            if(arm.TryTopForearm(.5f,out _))throw new BuildFailedException("Hidden arm exposed a touch path.");
            arm.gameObject.SetActive(true);arm.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            if(arm.MinimumClearance(tableParameters)<-.001f)throw new BuildFailedException("Synthetic resting arm intersects table.");
        }
        var table=GameObject.CreatePrimitive(PrimitiveType.Cube);table.transform.position=new Vector3(0,.86f,.505f);table.transform.localScale=new Vector3(1.1f,.04f,.65f);
        table.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SeatedTable.mat");
        var light=new GameObject("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-25,0);light.intensity=.8f;
        RenderSettings.ambientLight=new Color(.65f,.65f,.65f);RenderSettings.skybox=null;
        var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.68f,.70f,.71f);
        camera.transform.position=new Vector3(0,1.28f,-.09f);camera.transform.LookAt(new Vector3(0,.76f,.36f));camera.fieldOfView=65;camera.nearClipPlane=.02f;
        var texture=new RenderTexture(1280,960,24);camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
        var png=new Texture2D(1280,960,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1280,960),0,0);png.Apply();
        File.WriteAllBytes("/private/tmp/quest-body-avatar-preview.png",png.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(png);
        Debug.Log("[Mirror] Synthetic preview saved to /private/tmp/quest-body-avatar-preview.png");
    }

}
