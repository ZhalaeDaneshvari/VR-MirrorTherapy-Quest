using UnityEngine;
namespace QuestResearch.Embodiment
{
    public static class TableContactMath
    {
        public static bool Inside(Vector3 p,SeatedParameters table)=>Mathf.Abs(p.x)<=table.tableWidth*.5f && p.z>=table.tableNearEdge && p.z<=table.tableNearEdge+table.tableDepth;
    }
}
