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
        var dwell=new PilotDwell();int count=0;
        for(int i=0;i<60;i++)if(dwell.Update(0,.1f))count++;
        if(count!=1)throw new BuildFailedException("Dwell repeated while gaze was held.");
        dwell.Block(0);for(int i=0;i<30;i++)if(dwell.Update(0,.1f))throw new BuildFailedException("Dwell crossed page without release.");
        for(int i=0;i<30;i++)if(dwell.Update(1,.1f))throw new BuildFailedException("Page change armed a different button without gaze exit.");
        dwell.Update(-1,.1f);count=0;for(int i=0;i<25;i++)if(dwell.Update(0,.1f))count++;
        if(count!=1)throw new BuildFailedException("Dwell did not rearm after gaze exit.");
        var activity=new GuidedDemoActivity();activity.Begin(false);
        for(int i=0;i<100;i++)activity.Advance(.1f);
        if(activity.Elapsed<9.9f||activity.Complete)throw new BuildFailedException("Familiarisation clock failed.");
        float before=activity.Elapsed;activity.Advance(5);activity.Advance(float.NaN);
        if(activity.Elapsed!=before)throw new BuildFailedException("Activity advanced through stalled/invalid timing.");
        activity.SkipPrompt();activity.SkipPrompt();if(!activity.Complete)throw new BuildFailedException("Skip did not finish practice.");
        activity.Begin(true);for(int i=0;i<6;i++)activity.SkipPrompt();
        if(!activity.Complete||activity.Remaining!=0)throw new BuildFailedException("Mirror sequence length failed.");
        // A missing contralateral hand must not be needed to produce the requested opposite pose.
        foreach(var profile in new[]{ParticipantHands.LeftOnly,ParticipantHands.RightOnly})
        {
            var parameters=new SeatedParameters{participantHands=profile};
            int sourceSide=profile==ParticipantHands.LeftOnly?0:1;
            if(!parameters.Valid()||!parameters.UsesSide(sourceSide)||parameters.UsesSide(1-sourceSide))throw new BuildFailedException("One-hand source selection failed.");
            var left=new QuestResearch.Hands.TrackingHandFrame();var right=new QuestResearch.Hands.TrackingHandFrame();
            var source=sourceSide==0?left:right;source.JointValid[0]=true;source.JointPoses[0]=new Pose(new Vector3(.2f,.8f,.3f),Quaternion.Euler(10,20,30));
            var a=new DisplayedHandPose();var b=new DisplayedHandPose();
            MovementReflection.Map(left,right,a,b,sourceSide==0?MovementCondition.LeftToRight:MovementCondition.RightToLeft,false,true,Pose.identity);
            var target=sourceSide==0?b:a;var hidden=sourceSide==0?a:b;
            if(!target.Valid[0]||hidden.Valid[0]||Vector3.Distance(target.Joints[0].position,new Vector3(-.2f,.8f,.3f))>.00001f)throw new BuildFailedException("Single-hand opposite-only mapping failed.");
            source.Invalidate(QuestResearch.Hands.HandAvailability.Waiting);
            MovementReflection.Map(left,right,a,b,sourceSide==0?MovementCondition.LeftToRight:MovementCondition.RightToLeft,false,true,Pose.identity);
            if(a.Valid[0]||b.Valid[0])throw new BuildFailedException("Lost selected hand must hide both outputs.");
            activity.Begin(true,profile);
            for(int i=0;i<6;i++){if(!activity.Prompt.Contains(sourceSide==0?"REAL LEFT":"REAL RIGHT"))throw new BuildFailedException("One-hand cue requests wrong physical side.");activity.SkipPrompt();}
            if(!activity.Complete)throw new BuildFailedException("One-hand activity duration failed.");
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Pilot preview camera").AddComponent<Camera>();
        camera.fieldOfView=65;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.60f,.64f,.67f);
        var pilot=new GameObject("Pilot preview").AddComponent<PilotWalkthrough>();
        pilot.Configure(null,null,null,camera.transform,AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SetupButton.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SetupButtonHover.mat"),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        pilot.BuildUI();
        var panel=scene.GetRootGameObjects().Single(x=>x.name=="Pilot walkthrough UI");panel.transform.position=Vector3.forward*1.15f;
        var texture=new RenderTexture(1280,960,24);camera.targetTexture=texture;
        var png=new Texture2D(1280,960,TextureFormat.RGB24,false);
        foreach(PilotWalkthrough.Page page in System.Enum.GetValues(typeof(PilotWalkthrough.Page)))
        {
            if(page==PilotWalkthrough.Page.Aligning)continue;
            pilot.Show(page);
            camera.Render();RenderTexture.active=texture;png.ReadPixels(new Rect(0,0,1280,960),0,0);png.Apply();
            File.WriteAllBytes("/private/tmp/quest-pilot-"+page+".png",png.EncodeToPNG());
        }
        pilot.Show(PilotWalkthrough.Page.Welcome);
        var ring=panel.GetComponentInChildren<GazeProgressRing>(true);
        ring.SetProgress(new Vector3(.275f,-.205f,-.02f),.65f,true);
        camera.Render();RenderTexture.active=texture;png.ReadPixels(new Rect(0,0,1280,960),0,0);png.Apply();
        Color reference=png.GetPixel(640,480);
        var occluder=GameObject.CreatePrimitive(PrimitiveType.Cube);occluder.transform.position=new Vector3(0,0,.3f);occluder.transform.localScale=new Vector3(2,2,.05f);
        camera.Render();RenderTexture.active=texture;png.ReadPixels(new Rect(0,0,1280,960),0,0);png.Apply();
        Color actual=png.GetPixel(640,480);
        if(Vector4.Distance(reference,actual)>.01f)throw new BuildFailedException("Scene geometry occluded overlay UI.");
        int bright=0;
        foreach(var pixel in png.GetPixels())if(pixel.r>.8f&&pixel.g>.8f&&pixel.b>.8f)bright++;
        if(bright<500)throw new BuildFailedException("Overlay text lost its readable white color.");
        File.WriteAllBytes("/private/tmp/quest-pilot-overlay-ring.png",png.EncodeToPNG());
        pilot.BeginAlignment();
        if(!pilot.AlignmentUIHidden || panel.GetComponentsInChildren<Renderer>().Length!=0)
            throw new BuildFailedException("Alignment must hide all menu renderers.");
        camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(png);
        Debug.Log("[Pilot] PASS: guided pages render; overlay readable; alignment hides the entire UI.");
    }
}
