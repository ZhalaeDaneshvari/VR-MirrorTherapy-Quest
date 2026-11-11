using UnityEngine;
namespace QuestResearch.Embodiment
{
    // Paused frames never enter Advance. A long stall must not consume a whole cue.
    public sealed class GuidedDemoActivity
    {
        static readonly string[] Normal={"Gently open and relax your fingers.\nNotice each hand moving on its own side.","Slowly lift and lower your hands a little.\nRest whenever you need to."};
        static readonly string[] Mirror={"Gently move your fingers.\nNotice the opposite virtual hand respond.","Move your REAL LEFT hand a little.\nWatch the virtual right hand.","Rest, then move your REAL RIGHT hand.\nWatch the virtual left hand.","Slowly open and relax one hand.\nThen try the other, if comfortable.","Gently lift one hand, then lower it.\nKeep the movement small and comfortable.","Rest both hands on the table.\nNotice how the virtual arms feel."};
        static readonly string[] SingleNormal={"Gently open and relax your fingers.\nNotice your selected hand on its own side.","Slowly lift and lower your selected hand.\nRest whenever you need to."};
        static readonly string[] SingleMirror={"Gently move your REAL {0} fingers.\nWatch the virtual {1} hand respond.","Slowly lift your REAL {0} hand a little.\nWatch the virtual {1} hand.","Lower your REAL {0} hand to the table.\nKeep the movement small and comfortable.","Slowly open and relax your REAL {0} hand.\nRest whenever you need to.","Gently turn your REAL {0} hand, if comfortable.\nNotice the opposite virtual hand.","Rest your REAL {0} hand on the table.\nNotice how the virtual {1} arm feels."};
        ParticipantHands profile;
        bool mirror;public float Elapsed{get;private set;}
        string[] Prompts=>profile==ParticipantHands.Both?(mirror?Mirror:Normal):(mirror?SingleMirror:SingleNormal);
        public int PromptIndex=>Mathf.Min((int)(Elapsed/10),Prompts.Length-1);
        public string Prompt=>profile==ParticipantHands.Both?Prompts[PromptIndex]:string.Format(Prompts[PromptIndex],profile==ParticipantHands.LeftOnly?"LEFT":"RIGHT",profile==ParticipantHands.LeftOnly?"RIGHT":"LEFT");
        public int Remaining=>Mathf.CeilToInt(Mathf.Max(0,Prompts.Length*10-Elapsed));
        public bool Complete=>Elapsed>=Prompts.Length*10;
        public void Begin(bool mirrored,ParticipantHands hands=ParticipantHands.Both){mirror=mirrored;profile=hands;Elapsed=0;}
        public void Advance(float seconds){if(float.IsFinite(seconds)&&seconds>0&&seconds<=.25f)Elapsed=Mathf.Min(Elapsed+seconds,Prompts.Length*10);}
        public void SkipPrompt(){Elapsed=Mathf.Min((PromptIndex+1)*10,Prompts.Length*10);}
    }
    public static class DemoSkinPalette
    {
        public static readonly string[] Names={"Fair","Light","Medium","Brown","Deep"};
        public static readonly Color[] Colours={new Color(.94f,.77f,.65f),new Color(.83f,.63f,.46f),new Color(.69f,.46f,.32f),new Color(.43f,.27f,.18f),new Color(.24f,.14f,.10f)};
    }
}
