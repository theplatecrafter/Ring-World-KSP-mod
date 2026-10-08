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
        public const int Version=2;
        /// <summary>Unity main-thread notifications after complete mesh publication and before retirement.
        /// Borrowed resources must not be retained after retirement; copy data for asynchronous work.</summary>
        public static event System.Action<RingworldTerrainChunk> ChunkReady,ChunkRetiring;
        private static readonly Dictionary<int,RingworldTerrainChunk> chunks=new Dictionary<int,RingworldTerrainChunk>();
        public static RingworldTerrainChunk[] GetLoadedChunks()
        {var result=new RingworldTerrainChunk[chunks.Count];chunks.Values.CopyTo(result,0);return result;}
        internal static void Add(string ringId,GameObject root,Mesh mesh,double along,double across,double size)
        {var c=new RingworldTerrainChunk{Id=root.GetInstanceID(),RingId=ringId,Transform=root.transform,Mesh=mesh,Along=along,Across=across,Size=size};chunks[c.Id]=c;Notify(ChunkReady,c);}
        internal static void Remove(GameObject root){RingworldTerrainChunk c;if(root!=null&&chunks.TryGetValue(root.GetInstanceID(),out c)){Notify(ChunkRetiring,c);chunks.Remove(c.Id);}}
        private static void Notify(System.Action<RingworldTerrainChunk> callback,RingworldTerrainChunk chunk)
        {if(callback==null)return;foreach(System.Action<RingworldTerrainChunk> handler in callback.GetInvocationList())try{handler(chunk);}catch(System.Exception e){Debug.LogError("[NivenRingworld] Terrain consumer failed: "+e);}}
        /// <summary>Sample arbitrary cylindrical coordinates in the currently loaded ring, independently of mesh LOD.
        /// Supports ecology weights, dry/wet state, elevation and local inward up. No spherical host-body sampling.</summary>
        public static bool TrySample(string ringId,double along,double across,out RingworldTerrainSample result)
        {
            result=default(RingworldTerrainSample);var f=RingworldFlight.Instance;
            if(f==null||!f.Active||f.Settings.RingId!=ringId||double.IsNaN(along)||double.IsInfinity(along)||double.IsNaN(across)||double.IsInfinity(across))return false;
            var s=f.Settings;if(System.Math.Abs(across)>s.Geometry.P.Width/2)return false;
            var t=s.Terrain.Sample(along,across);var e=Ringworld.Core.Ecology.Sample(s.Terrain,along,across);var p=s.Geometry.Position(along,across,t.Height);
            result=new RingworldTerrainSample{Biome=t.Biome.ToString(),Elevation=t.Height,WaterElevation=t.WaterHeight,Wet=t.Wet,Shore=t.Shore,
                SurfaceUp=ConvertVector.Ksp(s.Geometry.Up(p)),WorldPosition=s.Center+ConvertVector.Ksp(p),GroundColor=new Color((float)t.GroundColor.X,(float)t.GroundColor.Y,(float)t.GroundColor.Z),
                Forest=e.Forest,Meadow=e.Meadow,Desert=e.Desert,Cold=e.Cold,Highland=e.Highland,ForestMargin=Ringworld.Core.BiomePresentation.ForestMargin(s.Terrain,along,across)};return true;
        }
    }
    public struct RingworldTerrainChunk
    {
        public int Id;public string RingId;public Transform Transform;public Mesh Mesh;
        public double Along,Across,Size;
    }
    public struct RingworldTerrainSample {
        public string Biome;public double Elevation,WaterElevation,Shore,Forest,Meadow,Desert,Cold,Highland,ForestMargin;
        public bool Wet;public Vector3d SurfaceUp,WorldPosition;public Color GroundColor;
    }
}
