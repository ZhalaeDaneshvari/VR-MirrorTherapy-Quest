using System;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEngine;
using QuestResearch.Recording;
public static class RecordingVerification
{
    public static void Run()
    {
        if(!MovementRecorder.ValidParticipant("P01")||!MovementRecorder.ValidParticipant("P20")||MovementRecorder.ValidParticipant("P00")||MovementRecorder.ValidParticipant("P21")||MovementRecorder.ValidParticipant("../P01"))throw new BuildFailedException("Participant validation failed");
        string root=Path.Combine(Path.GetTempPath(),"quest-recording-test-"+Guid.NewGuid().ToString("N"));
        try
        {
            var writer=new MovementSessionWriter(root,"{\"schemaVersion\":1}");
            for(int i=0;i<100;i++)if(!writer.Append("{\"sample\":"+i+"}"))throw new BuildFailedException("Unexpected recording backpressure");
            writer.Complete("test_finished");
            if(!writer.Wait(5000)||writer.Error!=null||writer.RecordsWritten!=100)throw new BuildFailedException("Writer drain failed");
            var lines=Directory.GetFiles(root,"*.jsonl").OrderBy(x=>x).SelectMany(File.ReadAllLines).ToArray();
            if(lines.Length!=100||lines[0]!="{\"sample\":0}"||lines[99]!="{\"sample\":99}"||!File.Exists(Path.Combine(root,"closed.json"))||Directory.GetFiles(root,"*.partial").Length!=0)throw new BuildFailedException("Recording finalization/order failed");
            if(writer.Append("{}"))throw new BuildFailedException("Closed session accepted a record");
            bool overwriteRejected=false;try{new MovementSessionWriter(root,"{}");}catch(IOException){overwriteRejected=true;}
            if(!overwriteRejected)throw new BuildFailedException("Session metadata overwritten");
            string invalid=Path.Combine(root,"not-a-directory");File.WriteAllText(invalid,"existing file");
            bool failureVisible=false;try{new MovementSessionWriter(invalid,"{}");}catch(IOException){failureVisible=true;}
            if(!failureVisible)throw new BuildFailedException("Filesystem failure not surfaced");
            string failedPath=Path.Combine(root,"worker-failure");var failed=new MovementSessionWriter(failedPath,"{}");
            Directory.Delete(failedPath,true);failed.Append("{}");
            if(!failed.Wait(3000)||failed.Error==null)throw new BuildFailedException("Worker IO failure was not surfaced");
            var frame=new QuestResearch.Hands.TrackingHandFrame();
            typeof(QuestResearch.Hands.TrackingHandFrame).GetProperty("Sequence").SetValue(frame,(ulong)42);
            var displayed=new QuestResearch.Embodiment.DisplayedHandPose();displayed.CopyFrom(frame);
            if(displayed.InputSequence!=42)throw new BuildFailedException("Mapped pose lost physical source sequence");
            var method=typeof(MovementRecorder).GetMethod("Read",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static,null,new[]{typeof(Pose),typeof(bool)},null);
            var invalidPose=(MovementRecorder.PoseValue)method.Invoke(null,new object[]{new Pose(new Vector3(float.NaN,0,0),Quaternion.identity),true});
            if(invalidPose.valid||JsonUtility.ToJson(invalidPose).Contains("NaN"))throw new BuildFailedException("Nonfinite pose reached JSON");
            Debug.Log("[Recording] PASS participant IDs, durable drain, record order, closure, overwrite protection, and filesystem failures.");
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
