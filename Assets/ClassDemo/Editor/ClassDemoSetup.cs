using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ClassEmbodimentDemo;

public static class ClassDemoSetup
{
    public const string ScenePath="Assets/ClassDemo/Scenes/ClassEmbodimentDemo.unity";
    [MenuItem("Quest Prototype/Class Demo/Create or Open Demo Scene")]
    public static void CreateOrOpen()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        if(System.IO.File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath);return;}
        System.IO.Directory.CreateDirectory("Assets/ClassDemo/Scenes");AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var go=new GameObject("Class demo — press Play (simulated input)");go.AddComponent<global::ClassEmbodimentDemo.ClassEmbodimentDemo>();
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        Debug.Log("Class demo scene ready. Press Play, then open Game view. QuestBootstrap build scene is unchanged.");
    }
    [MenuItem("Quest Prototype/Class Demo/Check Reflection Math")]
    public static void CheckMath()
    {
        foreach(var p in new[]{Vector3.zero,new Vector3(-.4f,1.2f,.3f),new Vector3(.2f,.7f,-.5f)})
            Require(Vector3.Distance(p,DemoMovementMapping.Reflect(DemoMovementMapping.Reflect(p)))<.00001f,"Position reflection must be involutive.");
        foreach(var q in new[]{Quaternion.identity,Quaternion.Euler(23,-47,81),Quaternion.Euler(-75,150,13)})
        {
            var reflected=DemoMovementMapping.Reflect(q);
            Require(Quaternion.Angle(q,DemoMovementMapping.Reflect(reflected))<.02f,"Rotation reflection must be involutive.");
            foreach(var v in new[]{Vector3.right,Vector3.up,Vector3.forward,new Vector3(.2f,-.3f,.7f)})
                Require(Vector3.Distance(reflected*v,DemoMovementMapping.Reflect(q*DemoMovementMapping.Reflect(v)))<.00001f,"Rotation must implement S R S.");
        }
        var l=new Pose(new Vector3(-.35f,1.3f,.2f),Quaternion.Euler(10,20,30));
        var r=new Pose(new Vector3(.4f,.9f,.3f),Quaternion.Euler(20,10,-15));
        foreach(MovementCondition condition in Enum.GetValues(typeof(MovementCondition)))
        {
            DemoMovementMapping.Map(condition,l,r,out var dl,out var dr);
            var expectedL=condition==MovementCondition.RightToLeft?DemoMovementMapping.Reflect(r.position):l.position;
            var expectedR=condition==MovementCondition.LeftToRight?DemoMovementMapping.Reflect(l.position):r.position;
            Require(Vector3.Distance(dl.position,expectedL)<.00001f && Vector3.Distance(dr.position,expectedR)<.00001f,"Condition routing failed.");
        }
        Debug.Log("Class demo reflection checks PASS: involution, S R S orientation, and three condition routes. Hardware and anatomical validation not performed.");
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
