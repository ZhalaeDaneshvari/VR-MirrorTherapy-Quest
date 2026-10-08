using UnityEngine;
namespace QuestResearch.Embodiment
{
    // Display-only tolerance for small surface intersections, not physical contact detection.
    public static class TableContactMath
    {
        public const float MaximumLift=.04f, Clearance=.002f;
        public static bool Inside(Vector3 p,SeatedParameters table)=>Mathf.Abs(p.x)<=table.tableWidth*.5f && p.z>=table.tableNearEdge && p.z<=table.tableNearEdge+table.tableDepth;
        public static float Lift(float lowest,float wristHeight,float tableHeight,float highest=float.PositiveInfinity)
        {
            // Deliberate below-table motion must never snap back onto the tabletop.
            if(highest<tableHeight || wristHeight<tableHeight-.025f || wristHeight>tableHeight+.12f || !float.IsFinite(lowest))return 0;
            float needed=tableHeight+Clearance-lowest;
            return needed>0 && needed<=MaximumLift?needed:0;
        }
    }
}
