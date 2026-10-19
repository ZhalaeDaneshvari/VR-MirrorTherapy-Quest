using System;
using System.IO;
using UnityEngine;
namespace QuestResearch.Embodiment
{
    // Optional USB/researcher control; the participant need not operate menus during a stroke.
    // New command IDs are required. Existing commands are ignored at app launch.
    public sealed class ManualTouchCommands : MonoBehaviour
    {
        [Serializable] public sealed class Command {public string id,action;public int physicalSide;public bool crossLimb=true;public float duration=4;}
        [Serializable] sealed class Reply {public string id,status,message;public double monotonicSeconds;}
        ForearmTouchStroke stroke;string path,lastId;DateTime lastWrite;double nextPoll;
        public void Configure(ForearmTouchStroke target)
        {
            stroke=target;path=Path.Combine(Application.persistentDataPath,"touch-command.json");
            if(File.Exists(path))lastWrite=File.GetLastWriteTimeUtc(path);
        }
        void Update()
        {
            if(stroke==null||Time.realtimeSinceStartupAsDouble<nextPoll)return;
            nextPoll=Time.realtimeSinceStartupAsDouble+.1;
            try
            {
                if(!File.Exists(path))return;var modified=File.GetLastWriteTimeUtc(path);if(modified==lastWrite)return;
                var c=JsonUtility.FromJson<Command>(File.ReadAllText(path));lastWrite=modified;
                if(c==null||string.IsNullOrWhiteSpace(c.id)||c.id.Length>64||c.id==lastId)return;
                lastId=c.id;bool accepted=false;
                if(c.action=="stop"){stroke.Stop();accepted=true;}
                if(c.action=="start"&&stroke.SetOptions((PhysicalHand)c.physicalSide,c.crossLimb,c.duration))accepted=stroke.StartStroke();
                File.WriteAllText(Path.Combine(Application.persistentDataPath,"touch-command-result.json"),JsonUtility.ToJson(new Reply{id=c.id,status=accepted?"accepted":"rejected",message=stroke.Message,monotonicSeconds=Time.realtimeSinceStartupAsDouble}));
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException)
            {Debug.LogWarning("[TouchCommands] Command could not be read/applied: "+e.GetType().Name);}
        }
    }
}
