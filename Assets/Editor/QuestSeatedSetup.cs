using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.OpenXR;
using Unity.XR.CoreUtils;
using QuestResearch.Hands;
using QuestResearch.Embodiment;

public static class QuestSeatedSetup
{
    public const string ScenePath="Assets/Scenes/QuestSeatedEmbodiment.unity";
    public const string AppId="org.mirrortherapy.questprototype.seated";
    public static void Configure()
    {
        QuestHandsSetup.Configure();
        PlayerSettings.productName="Quest Seated Embodiment";PlayerSettings.bundleVersion="0.3.0";PlayerSettings.Android.bundleVersionCode=3;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,AppId);
        if(!File.Exists(ScenePath))
        {
            if(!AssetDatabase.CopyAsset(QuestHandsSetup.ScenePath,ScenePath))throw new BuildFailedException("Could not create seated scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var origin=Object.FindAnyObjectByType<XROrigin>();
            foreach(var root in scene.GetRootGameObjects())if(root.name=="World References")Object.DestroyImmediate(root);
            var source=origin.GetComponent<HandTrackingSource>();
            Object.DestroyImmediate(origin.GetComponent<HandSkeletonDisplay>());
            var oldStatus=origin.GetComponent<HandTrackingStatus>();
            Object.DestroyImmediate(oldStatus);
            var text=origin.Camera.GetComponentInChildren<TextMesh>();
            text.text="M3 SEATED EMBODIMENT\nWaiting for researcher alignment";text.characterSize=.021f;
            origin.Camera.backgroundColor=new Color(.68f,.70f,.71f);
            RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            var light=new GameObject("Soft scene light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.7f;light.transform.rotation=Quaternion.Euler(45,20,0);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Plain floor";floor.transform.position=new Vector3(0,-.025f,0);floor.transform.localScale=new Vector3(12,.05f,12);
            Object.DestroyImmediate(floor.GetComponent<Collider>());floor.GetComponent<Renderer>().sharedMaterial=Material("SeatedFloor",new Color(.61f,.63f,.64f));
            var mapping=origin.gameObject.AddComponent<MovementRemapper>();mapping.Configure(source);
            var setup=origin.gameObject.AddComponent<SeatedSetup>();setup.Configure(origin.CameraFloorOffsetObject.transform,origin.Camera.transform,text);
            origin.gameObject.AddComponent<SeatedAvatarDisplay>().Configure(mapping,setup,origin.CameraFloorOffsetObject.transform,
                Material("SeatedSkin",new Color(.73f,.51f,.34f)),Material("SeatedShirt",new Color(.22f,.31f,.39f)),Material("SeatedTable",new Color(.74f,.71f,.65f)));
            EditorSceneManager.SaveScene(scene);
        }
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();Validate();
    }
    static Material Material(string name,Color color)
    {
        string path="Assets/Materials/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
        var material=new Material(Shader.Find("Standard")){name=name,color=color};material.SetFloat("_Glossiness",.1f);material.enableInstancing=true;AssetDatabase.CreateAsset(material,path);return material;
    }
    [MenuItem("Quest Prototype/Milestone 3/Validate Seated Embodiment")]
    public static void Validate()
    {
        if(Application.unityVersion!="6000.6.2f1")throw new BuildFailedException("Use Unity 6000.6.2f1.");
        QuestProjectSetup.Validate();
        if(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<HandTracking>()?.enabled!=true)throw new BuildFailedException("Hand subsystem missing.");
        if(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)!=AppId)throw new BuildFailedException("M3 app identifier missing.");
        if(!File.Exists(ScenePath))throw new BuildFailedException("Seated scene missing.");
        foreach(var wrist in new[]{new Vector3(-.2f,-.25f,.3f),new Vector3(.15f,-.25f,.3f),new Vector3(.1f,.2f,.3f)})
        {
            if(!SeatedArmMath.TryElbow(Vector3.zero,wrist,.3f,.26f,new Vector3(1,-1,-.3f),out var elbow))throw new BuildFailedException("Reachable IK target rejected.");
            if(Mathf.Abs(elbow.magnitude-.3f)>.0001f||Mathf.Abs(Vector3.Distance(elbow,wrist)-.26f)>.0001f)throw new BuildFailedException("IK segment length changed.");
        }
        if(SeatedArmMath.TryElbow(Vector3.zero,Vector3.one,.3f,.26f,Vector3.right,out _)||SeatedArmMath.TryElbow(Vector3.zero,Vector3.zero,.3f,.26f,Vector3.right,out _))throw new BuildFailedException("Invalid IK target accepted.");
        if(!new SeatedParameters().Valid()||new SeatedParameters{tableHeight=float.NaN}.Valid())throw new BuildFailedException("Parameter validation failed.");
        var input=new TrackingHandFrame();input.JointValid[0]=true;input.JointPoses[0]=new Pose(new Vector3(1,2,3),Quaternion.identity);
        var target=new DisplayedHandPose();target.CopyFrom(input);target.Joints[0]=Pose.identity;target.Valid[0]=false;
        if(!input.JointValid[0]||input.JointPoses[0].position!=new Vector3(1,2,3))throw new BuildFailedException("Displayed targets alias physical tracking data.");
        Debug.Log("[Seated] OpenXR, parameter validation, IK segment lengths, unreachable-target rejection and tracking/display separation checks PASS.");
    }
    [MenuItem("Quest Prototype/Milestone 3/Build Seated APK")]
    public static void Build()
    {
        Validate();Directory.CreateDirectory("Builds");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/QuestSeatedEmbodiment.apk",target=BuildTarget.Android,options=BuildOptions.Development});
        if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("M3 build failed: "+report.summary.result);
        Debug.Log("[Seated] APK built. Seated alignment and embodiment require hardware testing.");
    }
}
