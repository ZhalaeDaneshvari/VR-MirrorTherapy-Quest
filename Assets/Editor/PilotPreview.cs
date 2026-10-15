using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using QuestResearch.Embodiment;

public static class PilotPreview
{
    public static void Render()
    {
        if(!PilotWalkthrough.TryTableHeight("31",true,out float h)||Mathf.Abs(h-.7874f)>.00001f || PilotWalkthrough.TryTableHeight("NaN",false,out _) || PilotWalkthrough.TryTableHeight("-10",false,out _) || PilotWalkthrough.TryTableHeight("200",false,out _))
            throw new BuildFailedException("Table numeric entry conversion/range validation failed.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Pilot preview camera").AddComponent<Camera>();
        camera.fieldOfView=65;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.60f,.64f,.67f);
        var pilot=new GameObject("Pilot preview").AddComponent<PilotWalkthrough>();
        pilot.Configure(null,null,null,camera.transform,AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SetupButton.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SetupButtonHover.mat"),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        pilot.BuildUI();
        var panel=scene.GetRootGameObjects().Single(x=>x.name=="Pilot walkthrough UI");panel.transform.position=Vector3.forward*.9f;
        var texture=new RenderTexture(1280,960,24);camera.targetTexture=texture;
        var png=new Texture2D(1280,960,TextureFormat.RGB24,false);
        foreach(var page in new[]{PilotWalkthrough.Page.Welcome,PilotWalkthrough.Page.Table,PilotWalkthrough.Page.HeightEntry,PilotWalkthrough.Page.Fit,PilotWalkthrough.Page.Ready,PilotWalkthrough.Page.Settings,PilotWalkthrough.Page.Running})
        {
            pilot.Show(page);
            if(page==PilotWalkthrough.Page.Running && (panel.GetComponentsInChildren<TextMesh>().Length!=1 || panel.GetComponentsInChildren<TextMesh>()[0].text!="Settings"))
                throw new BuildFailedException("Pilot running view leaked setup UI.");
            camera.Render();RenderTexture.active=texture;png.ReadPixels(new Rect(0,0,1280,960),0,0);png.Apply();
            File.WriteAllBytes("/private/tmp/quest-pilot-"+page+".png",png.EncodeToPNG());
        }
        pilot.BeginAlignment();
        if(!pilot.AlignmentUIHidden || panel.GetComponentsInChildren<Renderer>().Length!=0)
            throw new BuildFailedException("Alignment must hide all menu renderers.");
        camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(png);
        Debug.Log("[Pilot] PASS: setup pages render; running view contains Settings only; alignment hides the entire UI.");
    }
}
