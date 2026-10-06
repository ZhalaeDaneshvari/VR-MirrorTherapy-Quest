using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;
using QuestResearch.Embodiment;
using QuestResearch.Hands;
using Object = UnityEngine.Object;

public static class QuestMirrorSetup
{
    public const string AppId = "org.mirrortherapy.questprototype.mirror";
    public const string ScenePath = "Assets/Scenes/QuestMovementMirror.unity";
    const string Generated = "Assets/GeneratedHands";

    [MenuItem("Quest Prototype/Milestone 4/Configure Movement Mirror")]
    public static void Configure()
    {
        QuestSeatedSetup.Configure();
        ImportHandModels();
        var left = CreateHand("Left");
        var right = CreateHand("Right");
        if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(QuestSeatedSetup.ScenePath, ScenePath))
            throw new BuildFailedException("Could not create mirror scene.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var origin = Object.FindAnyObjectByType<XROrigin>();
        var setup = origin.GetComponent<SeatedSetup>();
        origin.GetComponent<MovementRemapper>().ConfigureFrame(setup, origin.CameraFloorOffsetObject.transform);
        origin.GetComponent<SeatedAvatarDisplay>().ConfigureMeshes(left, right);
        var controls=origin.GetComponent<SeatedTestControls>();
        if(controls==null)controls=origin.gameObject.AddComponent<SeatedTestControls>();
        controls.Configure(setup,origin.GetComponent<HandTrackingSource>(),origin.Camera.transform,
            ControlMaterial("SetupButton",new Color(.12f,.18f,.22f)),ControlMaterial("SetupButtonHover",new Color(.12f,.5f,.55f)),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        origin.Camera.GetComponentInChildren<TextMesh>(true).GetComponent<MeshRenderer>().enabled=false;
        EditorSceneManager.SaveScene(scene);
        PlayerSettings.productName = "Quest Movement Mirror";
        PlayerSettings.bundleVersion = "0.4.2";
        PlayerSettings.Android.bundleVersionCode = 6;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Validate();
    }

    static Material ControlMaterial(string name,Color color)
    {
        string path="Assets/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Unlit/Color"));AssetDatabase.CreateAsset(material,path);}
        material.color=color;return material;
    }

    static void ImportHandModels()
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(XRHandSubsystem).Assembly);
        if (package == null || package.version != "1.9.0") throw new BuildFailedException("Expected pinned XR Hands 1.9.0.");
        Directory.CreateDirectory(Generated);
        foreach (string side in new[] { "Left", "Right" })
        {
            string name = side + "Hand.fbx";
            string source = Path.Combine(package.resolvedPath, "Samples~/HandVisualizer/Models", name);
            File.Copy(source, Generated + "/" + name, true);
            File.Copy(source + ".meta", Generated + "/" + name + ".meta", true);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string side in new[]{"Left","Right"})
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Generated+"/"+side+"Hand.fbx");
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
        }
    }

    static RemappedHandMesh CreateHand(string side)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + side + "Hand.fbx");
        if (model == null) throw new BuildFailedException("Hand model import failed.");
        var instance = Object.Instantiate(model);
        instance.name = side + " displayed hand";
        var all = instance.GetComponentsInChildren<Transform>(true);
        var bones = new Transform[XRHandJointID.EndMarker.ToIndex()];
        for (int i = 0; i < bones.Length; i++)
        {
            string joint = XRHandJointIDUtility.FromIndex(i).ToString();
            var matches = all.Where(t => t.name.StartsWith(joint, StringComparison.OrdinalIgnoreCase) || t.name.EndsWith(joint, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) throw new BuildFailedException(side + " hand: ambiguous/missing " + joint);
            bones[i] = matches[0];
        }
        var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (renderers.Length == 0) throw new BuildFailedException("No skinned hand mesh.");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SeatedSkin.mat");
        foreach (var renderer in renderers)
        {
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            renderer.updateWhenOffscreen = true;
            renderer.quality=SkinQuality.Bone4;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var driver = instance.AddComponent<RemappedHandMesh>();
        driver.Configure(bones, renderers);
        if(renderers.Length!=1)throw new BuildFailedException("Expected one hand mesh for wrist seam construction.");
        HandWristSeamBuilder.Build(renderers[0], bones[XRHandJointID.Wrist.ToIndex()], Generated+"/"+side+"OpenWrist.asset",out var seamPoints,out var seamNormals,out var seamIndices);
        driver.ConfigureSeam(seamPoints,seamNormals,seamIndices);
        string path = Generated + "/" + side + "DisplayedHand.prefab";
        // Stable generated-prefab GUIDs keep the checked-in scene reproducible on a fresh clone.
        if (!File.Exists(path + ".meta"))
            File.WriteAllText(path + ".meta", "fileFormatVersion: 2\nguid: " + (side == "Left" ? "91a1159a30d74516a6fb8e58a6100711" : "91a1159a30d74516a6fb8e58a6100712") + "\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Debug.Log($"[Mirror] {side} hand: {bones.Length} explicit bones, {renderers.Sum(x => x.sharedMesh.vertexCount)} vertices.");
        Object.DestroyImmediate(instance);
        return prefab.GetComponent<RemappedHandMesh>();
    }

    [MenuItem("Quest Prototype/Milestone 4/Validate Movement Mirror")]
    public static void Validate()
    {
        QuestProjectSetup.Validate();
        if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != AppId) throw new BuildFailedException("Not the M4 app configuration.");
        TestReflection();
        TestControlsAndSeam();
        foreach (string side in new[] { "Left", "Right" })
            if (AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated + "/" + side + "DisplayedHand.prefab") == null)
                throw new BuildFailedException("Use Milestone 4 Configure to generate meshes from the installed XR Hands package.");
        Debug.Log("[Mirror] PASS: body-frame reflection, orientation handedness, double reflection, mapping direction, source visibility and tracking invalidation.");
    }

    static void TestControlsAndSeam()
    {
        var pinch=new PinchLatch();
        Require(!pinch.UpdateDistance(.01f),"Acquired closed hand clicked.");
        Require(!pinch.UpdateDistance(.05f)&&pinch.UpdateDistance(.01f)&&!pinch.UpdateDistance(.01f),"Pinch release hysteresis failed.");
        pinch.Reset();Require(!pinch.UpdateDistance(.01f),"Tracking reacquisition clicked.");
        var centres=new[]{Vector3.zero};var sizes=new[]{new Vector2(.4f,.06f)};
        Require(SeatedTestControls.HitButton(new Vector3(0,0,-1),Vector3.forward,centres,sizes)==0,"Forward button ray missed.");
        Require(SeatedTestControls.HitButton(new Vector3(.3f,0,-1),Vector3.forward,centres,sizes)==-1,"Outside button ray hit.");
        Require(SeatedTestControls.HitButton(new Vector3(0,0,-1),Vector3.back,centres,sizes)==-1,"Backwards button ray hit.");
        Vector3 shoulder=new Vector3(-.19f,1.04f,-.07f),wristPosition=new Vector3(-.19f,.85f,.35f);
        Require(SeatedArmMath.TryElbow(shoulder,wristPosition,.3f,.26f,SeatedArmMath.RestingElbowPole(true),out var tucked),"Resting IK failed.");
        SeatedArmMath.TryElbow(shoulder,wristPosition,.3f,.26f,new Vector3(-1,-.6f,-.3f),out var old);
        Require(Mathf.Abs(tucked.x-shoulder.x)<Mathf.Abs(old.x-shoulder.x),"Elbow did not move toward torso.");
        foreach(float blend in new[]{0f,.25f,.5f,1f})
        {
            Require(SeatedArmMath.TrySupportedElbow(shoulder,new Vector3(-.19f,.90f,.35f),.3f,.26f,SeatedArmMath.RestingElbowPole(true),.92f,blend,out var supported),"Supported IK failed for reachable resting target.");
            Require(Mathf.Abs(Vector3.Distance(shoulder,supported)-.3f)<.0001f && Mathf.Abs(Vector3.Distance(supported,new Vector3(-.19f,.90f,.35f))-.26f)<.0001f,"Supported IK changed limb length.");
            if(blend==1)Require(supported.y>=.9199f,"Supported elbow remained below surface constraint.");
        }
        Require(!SeatedArmMath.TrySupportedElbow(shoulder,wristPosition,.3f,.26f,SeatedArmMath.RestingElbowPole(true),2f,1,out _),"Impossible surface fit was accepted.");
        foreach(string side in new[]{"Left","Right"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/"+side+"DisplayedHand.prefab");
            Require(prefab!=null,"Generate hand assets before validation.");
            var space=new GameObject("Seam test space").transform;
            space.SetPositionAndRotation(new Vector3(2,.4f,-3),Quaternion.Euler(0,61,0));
            var hand=Object.Instantiate(prefab,space,false);
            var pose=new DisplayedHandPose();
            for(int i=0;i<pose.Joints.Length;i++)
            {
                pose.Valid[i]=true;
                pose.Joints[i]=new Pose(space.InverseTransformPoint(hand.Joints[i].position),Quaternion.Inverse(space.rotation)*hand.Joints[i].rotation*Quaternion.Euler(i%3*12,0,0));
            }
            hand.Apply(pose,space,true);
            var renderer=hand.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();renderer.BakeMesh(baked);
            var vertices=baked.vertices;var wrist=hand.Joints[XRHandJointID.Wrist.ToIndex()];
            for(int i=0;i<hand.WristSeamPoints.Length;i++)
                Near(wrist.TransformPoint(hand.WristSeamPoints[i]),renderer.transform.TransformPoint(vertices[hand.SeamVertexIndices[i]]),"CPU wrist skinning differs from actual mesh.");
            var arm=new GameObject("Seam test arm").AddComponent<ContinuousArmSurface>();
            arm.Initialize(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SeatedSkin.mat"),hand.WristSeamPoints,hand.WristSeamNormals);
            arm.Apply(wrist.position+Vector3.back*.5f,wrist.position+Vector3.back*.25f,wrist.position,wrist.rotation);
            var armVertices=arm.GetComponent<MeshFilter>().sharedMesh.vertices;
            for(int i=0;i<hand.WristSeamPoints.Length;i++)
                Near(armVertices[armVertices.Length-hand.WristSeamPoints.Length+i],wrist.TransformPoint(hand.WristSeamPoints[i]),"Arm/hand seam detached.");
            Object.DestroyImmediate(arm.gameObject);Object.DestroyImmediate(baked);Object.DestroyImmediate(space.gameObject);
        }
        Debug.Log("[SetupFix] PASS: pinch debouncing, gaze hit tests, tucked elbow and deformed arm/hand seam positions and length-preserving table-supported IK.");
    }

    static void Require(bool pass, string message) { if (!pass) throw new BuildFailedException(message); }
    static void Near(Vector3 a, Vector3 b, string message) { Require(Vector3.Distance(a, b) < .0001f, message); }
    public static void TestReflection()
    {
        var frame = new Pose(new Vector3(2, .3f, -4), Quaternion.Euler(0, 73, 0));
        var random = new System.Random(42);
        for (int i = 0; i < 100; i++)
        {
            var local = new Vector3((float)random.NextDouble()-.5f, (float)random.NextDouble(), (float)random.NextDouble());
            var input = new Pose(frame.position + frame.rotation * local, Quaternion.Euler(i * 7, i * 13, i * 19));
            var reflected = MovementReflection.Reflect(input, frame);
            Near(Quaternion.Inverse(frame.rotation) * (reflected.position-frame.position), new Vector3(-local.x,local.y,local.z), "Reflection used the wrong plane.");
            var twice = MovementReflection.Reflect(reflected, frame);
            Near(input.position, twice.position, "Double reflection moved a joint.");
            Require(Quaternion.Angle(input.rotation, twice.rotation) < .06f, "Double reflection changed orientation.");
            var r = reflected.rotation;
            Near(Vector3.Cross(r*Vector3.right, r*Vector3.up), r*Vector3.forward, "Orientation basis became improper.");
            Vector3 sourceUp = Quaternion.Inverse(frame.rotation) * (input.rotation * Vector3.up);
            sourceUp.x = -sourceUp.x;
            Near(reflected.rotation * Vector3.up, frame.rotation * sourceUp, "Palm normal reflection failed.");
        }
        var left = new TrackingHandFrame(); var right = new TrackingHandFrame();
        for(int i=0;i<left.JointPoses.Length;i++)
        {
            left.JointPoses[i]=new Pose(new Vector3(-.2f+i*.001f,.8f,.3f),Quaternion.Euler(i*3,20,5));
            right.JointPoses[i]=new Pose(new Vector3(.25f+i*.001f,.9f,.4f),Quaternion.Euler(i*2,10,8));
            left.JointValid[i]=right.JointValid[i]=true;
        }
        var a=new DisplayedHandPose();var b=new DisplayedHandPose();
        foreach(var condition in new[]{MovementCondition.LeftToRight, MovementCondition.RightToLeft})
        {
            MovementReflection.Map(left,right,a,b,condition,false,true,frame);
            var output=condition==MovementCondition.LeftToRight?b:a;
            var hidden=condition==MovementCondition.LeftToRight?a:b;
            var source=condition==MovementCondition.LeftToRight?left:right;
            Require(!hidden.Valid.Any(x=>x),"Opposite-only mode showed source hand.");
            for(int i=0;i<output.Joints.Length;i++) Near(output.Joints[i].position,MovementReflection.Reflect(source.JointPoses[i],frame).position,"Finger mapping mismatch.");
            source.JointValid[3]=false;
            MovementReflection.Map(left,right,a,b,condition,true,true,frame);
            Require(!output.Valid[3],"Invalid source joint remained visible.");
            source.JointValid[3]=true;
        }
        MovementReflection.Map(left,right,a,b,MovementCondition.Normal,true,false,frame);
        Near(a.Joints[0].position,left.JointPoses[0].position,"Normal left changed.");
        Near(b.Joints[0].position,right.JointPoses[0].position,"Normal right changed.");
        a.Joints[0]=Pose.identity;Require(left.JointPoses[0].position!=Vector3.zero,"Output aliases provider data.");
        MovementReflection.Map(left,right,a,b,MovementCondition.LeftToRight,true,false,frame);
        Require(!a.Valid.Any(x=>x)&&!b.Valid.Any(x=>x),"Mirroring without calibration was allowed.");
    }

    [Serializable] public class MeshDump { public Vector3[] vertices; public int[] triangles; }
    public static void InspectWrist()
    {
        var hand=Object.Instantiate(AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/LeftDisplayedHand.prefab"));
        var renderer=hand.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh=new Mesh();renderer.BakeMesh(mesh);
        var wrist=hand.Joints[XRHandJointID.Wrist.ToIndex()];
        var vertices=mesh.vertices.Select(v=>wrist.InverseTransformPoint(renderer.transform.TransformPoint(v))).ToArray();
        File.WriteAllText("/private/tmp/quest-wrist-mesh.json",JsonUtility.ToJson(new MeshDump{vertices=vertices,triangles=mesh.triangles}));
        Object.DestroyImmediate(hand);Object.DestroyImmediate(mesh);
    }

    public static void RenderControlPreview()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Control preview camera").AddComponent<Camera>();
        camera.transform.position=new Vector3(0,1.2f,0);camera.fieldOfView=65;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.68f,.70f,.71f);
        var controls=new GameObject("Preview controls").AddComponent<SeatedTestControls>();
        controls.Configure(null,null,camera.transform,AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SetupButton.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SetupButtonHover.mat"),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        controls.SendMessage("Start");
        var panel=scene.GetRootGameObjects().Single(x=>x.name=="Testing controls - gaze and pinch");
        panel.transform.position=camera.transform.position+Vector3.forward*.65f+Vector3.up*.12f;panel.SetActive(true);
        var title=panel.GetComponentsInChildren<TextMesh>().Single(x=>x.text=="Setup controls");title.text="Normal / table 74 cm\nWaiting for alignment\nTable uncalibrated";
        var texture=new RenderTexture(1280,960,24);camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
        var png=new Texture2D(1280,960,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1280,960),0,0);png.Apply();File.WriteAllBytes("/private/tmp/quest-controls-preview.png",png.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(png);
    }

    public static void BuildAndPreview() { Build(); RenderPreview(); RenderControlPreview(); }

    // Editor-only synthetic pose preview; never substitutes for headset tracking validation.
    public static void RenderPreview()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var space = new GameObject("Preview tracking space").transform;
        var left = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/LeftDisplayedHand.prefab"), space);
        var right = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RemappedHandMesh>(Generated+"/RightDisplayedHand.prefab"), space);
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
        left.Apply(pose,space,true);right.Apply(mirrored,space,true);
        var skin=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SeatedSkin.mat");
        for(int side=0;side<2;side++)
        {
            var shoulder=new Vector3(side==0?-.19f:.19f,1.03f,-.07f);
            var target=side==0?pose:mirrored;
            var wristPose=target.Joints[XRHandJointID.Wrist.ToIndex()];
            if(!SeatedArmMath.TryElbow(shoulder,wristPose.position,.30f,.26f,SeatedArmMath.RestingElbowPole(side==0),out var elbow))throw new BuildFailedException("Preview IK failed.");
            if(SeatedArmMath.TrySupportedElbow(shoulder,wristPose.position,.30f,.26f,SeatedArmMath.RestingElbowPole(side==0),.88f+.04f*1.3f,1,out var supported))elbow=supported;
            var arm=new GameObject("Preview arm").AddComponent<ContinuousArmSurface>();arm.Initialize(skin,(side==0?left:right).WristSeamPoints,(side==0?left:right).WristSeamNormals);
            arm.Apply(shoulder,elbow,wristPose.position,wristPose.rotation);
        }
        var table=GameObject.CreatePrimitive(PrimitiveType.Cube);table.transform.position=new Vector3(0,.86f,.505f);table.transform.localScale=new Vector3(1.1f,.04f,.65f);
        table.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SeatedTable.mat");
        var light=new GameObject("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-25,0);light.intensity=.8f;
        RenderSettings.ambientLight=new Color(.65f,.65f,.65f);RenderSettings.skybox=null;
        var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.68f,.70f,.71f);
        camera.transform.position=new Vector3(0,1.28f,-.09f);camera.transform.LookAt(new Vector3(0,.76f,.36f));camera.fieldOfView=65;camera.nearClipPlane=.02f;
        var texture=new RenderTexture(1280,960,24);camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
        var png=new Texture2D(1280,960,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1280,960),0,0);png.Apply();
        File.WriteAllBytes("/private/tmp/quest-m4-preview.png",png.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(png);
        Debug.Log("[Mirror] Synthetic preview saved to /private/tmp/quest-m4-preview.png");
    }

    [MenuItem("Quest Prototype/Milestone 4/Build Movement Mirror APK")]
    public static void Build()
    {
        Validate();
        Directory.CreateDirectory("Builds");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/QuestMovementMirror.apk",target=BuildTarget.Android,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded) throw new BuildFailedException("M4 build failed: "+report.summary.result);
        Debug.Log("[Mirror] APK built. Movement mapping and mesh appearance need wearer acceptance.");
    }
}
