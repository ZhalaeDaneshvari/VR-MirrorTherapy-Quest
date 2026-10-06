using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Adapt only the pinned Unity sample's closed forearm cap. The generated mesh remains
// local/ignored with the upstream model. The hand's tracked joint positions never move.
public static class HandWristSeamBuilder
{
    public static void Build(SkinnedMeshRenderer renderer, Transform wrist, string outputPath,
        out Vector3[] seamPoints, out Vector3[] seamNormals, out int[] seamIndices)
    {
        var baked=new Mesh();renderer.BakeMesh(baked);
        var source=renderer.sharedMesh;
        Vector3[] positions=baked.vertices.Select(v=>wrist.InverseTransformPoint(renderer.transform.TransformPoint(v))).ToArray();
        var kept=new List<int>();int removed=0;
        // Sample 1.9.0's wrist cap is behind the wrist (z< -7 mm), facing back.
        const float capFrontLimit=-.007f, backFacingCosine=.8f;
        var indices=source.triangles;
        for(int i=0;i<indices.Length;i+=3)
        {
            int a=indices[i],b=indices[i+1],c=indices[i+2];
            Vector3 normal=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]).normalized;
            float z=(positions[a].z+positions[b].z+positions[c].z)/3;
            if(z<capFrontLimit && normal.z < -backFacingCosine){removed++;continue;}
            kept.Add(a);kept.Add(b);kept.Add(c);
        }
        if(removed<10 || removed>60)throw new BuildFailedException("Unexpected wrist cap topology; inspect the source model.");
        var mesh=Object.Instantiate(source);mesh.name=source.name+" open wrist";
        mesh.triangles=kept.ToArray();mesh.RecalculateNormals();
        renderer.sharedMesh=mesh;renderer.BakeMesh(baked);
        var normals=baked.normals;
        var keys=new Dictionary<Vector3Int,int>();var weld=new int[positions.Length];
        var representatives=new Dictionary<int,int>();
        for(int i=0;i<positions.Length;i++)
        {
            Vector3 p=positions[i]*100000;
            var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
            if(!keys.TryGetValue(key,out int id)){id=keys.Count;keys.Add(key,id);}
            weld[i]=id;
        }
        var edges=new Dictionary<(int,int),int>();
        for(int i=0;i<kept.Count;i+=3)
        {
            for(int j=0;j<3;j++)
            {
                int original=kept[i+j];representatives[weld[original]]=original;
                int a=weld[original], b=weld[kept[i+(j+1)%3]];
                var edge=a<b?(a,b):(b,a);edges.TryGetValue(edge,out int count);edges[edge]=count+1;
            }
        }
        var boundary=edges.Where(x=>x.Value==1).SelectMany(x=>new[]{x.Key.Item1,x.Key.Item2}).Distinct().ToArray();
        if(boundary.Length<12 || boundary.Length>80)throw new BuildFailedException("Unexpected open wrist boundary.");
        var degree=boundary.ToDictionary(x=>x,x=>0);
        foreach(var edge in edges.Where(x=>x.Value==1)){degree[edge.Key.Item1]++;degree[edge.Key.Item2]++;}
        if(degree.Values.Any(x=>x!=2))throw new BuildFailedException("Wrist seam is not a closed ring.");
        var ordered=boundary.Select(x=>representatives[x]).OrderBy(i=>Mathf.Atan2(positions[i].y,positions[i].x)).ToArray();
        seamIndices=ordered;
        seamPoints=ordered.Select(i=>positions[i]).ToArray();
        seamNormals=ordered.Select(i=>wrist.InverseTransformDirection(renderer.transform.TransformDirection(normals[i])).normalized).ToArray();
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
        else AssetDatabase.CreateAsset(mesh,outputPath);
        renderer.sharedMesh=mesh;Object.DestroyImmediate(baked);
        Debug.Log($"[WristSeam] Removed {removed} cap faces; matched {ordered.Length} skinned boundary vertices.");
    }
}
