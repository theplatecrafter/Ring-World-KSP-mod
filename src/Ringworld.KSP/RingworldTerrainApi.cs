using System.Collections.Generic;
using UnityEngine;
namespace NivenRingworld
{
    /// <summary>Loaded close-range terrain only; no PQS emulation. Call on Unity main thread.
    /// Meshes/transforms belong to Ringworld: never modify or destroy them. Refresh the snapshot
    /// after streaming; disappeared IDs mean release associated GPU buffers. Mesh positions and
    /// normals are local to Transform, which changes with the floating origin/rotating frame.</summary>
    public static class RingworldTerrainApi
    {
        public const int Version=1;
        private static readonly Dictionary<int,RingworldTerrainChunk> chunks=new Dictionary<int,RingworldTerrainChunk>();
        public static RingworldTerrainChunk[] GetLoadedChunks()
        {var result=new RingworldTerrainChunk[chunks.Count];chunks.Values.CopyTo(result,0);return result;}
        internal static void Add(string ringId,GameObject root,Mesh mesh,double along,double across,double size)
        {chunks[root.GetInstanceID()]=new RingworldTerrainChunk{Id=root.GetInstanceID(),RingId=ringId,Transform=root.transform,Mesh=mesh,Along=along,Across=across,Size=size};}
        internal static void Remove(GameObject root){if(root!=null)chunks.Remove(root.GetInstanceID());}
    }
    public struct RingworldTerrainChunk
    {
        public int Id;public string RingId;public Transform Transform;public Mesh Mesh;
        public double Along,Across,Size;
    }
}
