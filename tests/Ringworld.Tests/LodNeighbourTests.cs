using System;
using System.Collections.Generic;
using Ringworld.Core;
internal static class LodNeighbourTests
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(double along in new[]{0.0,-23456789,53993885100000.0})
        foreach(double range in new[]{200000.0,160000000})
        foreach(double tile in new[]{1024.0,1000.1,750.25})
        {
            var layout=TerrainLodPlan.Create(along,1700,tile,3,range,80250000,Math.PI*15300000000,Math.Sqrt(8*15300000000.0*250)*8);
            var index=new LodNeighbourIndex(layout);
            foreach(var b in layout)
            {
                var expected=new List<LodBlock>();
                foreach(var other in layout)if(other.Size>b.Size&&other.X<=b.X+b.Size&&other.X+other.Size>=b.X&&other.Y<=b.Y+b.Size&&other.Y+other.Size>=b.Y)expected.Add(other);
                var actual=index.CoarserNeighbours(b);check(actual.Count==expected.Count,"LOD feather neighbour count unchanged");
                for(int i=0;i<expected.Count;i++)check(actual[i].Key==expected[i].Key,"LOD feather colour order unchanged");
            }
        }
    }
}
