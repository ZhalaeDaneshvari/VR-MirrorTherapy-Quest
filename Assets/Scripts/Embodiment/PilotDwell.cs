using UnityEngine;
namespace QuestResearch.Embodiment
{
    // One activation per gaze entry. Page changes require leaving the previous target.
    public sealed class PilotDwell
    {
        int target=-1,blocked=-1;float seconds;bool fired;
        public float Progress=>Mathf.Clamp01(seconds/2);
        public void Block(int previous){blocked=previous;target=-1;seconds=0;fired=false;}
        public bool Update(int hit,float delta)
        {
            if(hit<0){blocked=-1;target=-1;seconds=0;fired=false;return false;}
            if(hit==blocked)return false;
            blocked=-1;
            if(hit!=target){target=hit;seconds=0;fired=false;}
            seconds+=Mathf.Clamp(delta,0,.1f);
            if(!fired&&seconds>=2){fired=true;return true;}
            return false;
        }
    }
}
