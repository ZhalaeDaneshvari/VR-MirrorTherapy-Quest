using UnityEngine;
namespace QuestResearch.Embodiment
{
    // Hysteresis for visual resting support, NOT a touch detector. Metres, body-local Y.
    public sealed class TableRestState
    {
        public bool Active { get; private set; }
        public float Offset { get; private set; }
        public float FlatBlend { get; private set; }
        public void Reset(){Active=false;Offset=FlatBlend=0;}
        public void BeginFrame(bool eligible,float dt)
        {
            if(!eligible)Active=false;
            FlatBlend=Mathf.MoveTowards(FlatBlend,Active?1:0,Mathf.Max(0,dt)/.06f);
        }
        public float UpdateSurface(float lowest,float highest,float height,bool eligible,float dt)
        {
            if(!float.IsFinite(lowest)||highest<height){Reset();return 0;}
            float gap=lowest-height;
            if(!eligible || gap>.025f || gap<-.10f)Active=false;
            else if(!Active && gap<=.006f && gap>=-.06f)Active=true;
            float target=Active?Mathf.Clamp(height+.0005f-lowest,-.01f,.06f):0;
            float alpha=1-Mathf.Exp(-Mathf.Max(0,dt)/.045f);
            // Resolve upward penetration promptly; ease release/downward settling without a hard cap discontinuity.
            Offset=Active&&target>Offset?target:Mathf.Lerp(Offset,target,alpha);
            if(Mathf.Abs(Offset)<.00005f)Offset=0;
            return Offset;
        }
    }
}
