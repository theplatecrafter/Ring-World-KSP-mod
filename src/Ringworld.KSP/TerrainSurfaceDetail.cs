using System.Collections.Generic;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class TerrainSurfaceDetail
    {
        internal static void Bind(Material material,AssetBundle bundle)
        {
            material.SetFloat("_SurfaceDetailEnabled",1);
        }
        private static Vector4 Pack(double value){unchecked{ulong bits=(ulong)(long)(value/TerrainSurface.OriginStep);return new Vector4(bits&65535,(bits>>16)&65535,(bits>>32)&65535,(bits>>48)&65535);}}
        internal static void Origin(MaterialPropertyBlock block,DVec origin,int seed){
            block.SetVector("_SurfaceOriginX",Pack(origin.X));block.SetVector("_SurfaceOriginY",Pack(origin.Y));block.SetVector("_SurfaceOriginZ",Pack(origin.Z));
            uint s=unchecked((uint)seed);block.SetVector("_SurfaceSeed",new Vector4(s&65535,s>>16,0,0));
        }
        internal static void Add(List<Vector4> coordinates,List<Vector4> weights,List<Vector3> points,TerrainSample sample,double along,double across,DVec origin,double radius)
        {
            int kind=TerrainSurface.Kind(sample.Biome,sample.Wet);
            coordinates.Add(new Vector4(0,0,kind==4?1:0,kind==5?1:0));
            weights.Add(new Vector4(kind==0?1:0,kind==1?1:0,kind==2?1:0,kind==3?1:0));
            points.Add(ConvertVector.Unity(TerrainSurface.NoisePoint(along,across,sample.Height,radius)-origin));
        }
    }
}
