using System;
using System.Collections.Generic;
using Ringworld.Core;
namespace NivenRingworld
{
    /// <summary>Ring-aware crustal resource query. Fractional abundance (0..1), not percent.
    /// Uses stock ResourceCache after ModuleManager configuration processing.
    /// Does not implement orbital survey locks, depletion or packed/background extraction.</summary>
    public static class RingworldResourceApi
    {
        public const int Version=1;
        public static bool TryGetAbundance(Vessel vessel,string resource,out double abundance)
        {
            abundance=0;RingworldSurfaceState state;
            if(!RingworldSurfaceApi.TryGetSurfaceState(vessel,out state))return false;
            return Sample(RingworldFlight.Instance.Settings,state.Along,state.Across,state.Biome,resource,out abundance);
        }
        internal static bool Sample(Settings s,double along,double across,string biome,string resource,out double abundance)
        {
            abundance=0;var cache=ResourceCache.Instance;if(cache==null||string.IsNullOrEmpty(resource))return false;
            ResourceData found=null;
            Select(cache.GlobalResources,s,biome,resource,0,ref found);
            Select(cache.PlanetaryResources,s,biome,resource,1,ref found);
            Select(cache.BiomeResources,s,biome,resource,2,ref found);
            if(found==null||found.Distribution==null)return false;
            var d=found.Distribution;
            foreach(float v in new[]{d.PresenceChance,d.MinAbundance,d.MaxAbundance,d.Variance,d.Dispersal})
                if(float.IsNaN(v)||float.IsInfinity(v))return false;
            // Stable hashes, not CLR string hashes, retain deposits after process restarts.
            int salt=Hash(s.RingId+"/"+resource+"/"+biome);
            if(s.Terrain.Scatter(salt,0,1801)*100>=d.PresenceChance)return true;
            double mean=d.MinAbundance+(d.MaxAbundance-d.MinAbundance)*s.Terrain.Scatter(salt,1,1801);
            double scale=100000/Math.Max(.1,Math.Min(100,d.Dispersal));
            double noise=s.Terrain.Noise(along,across,scale,salt);
            abundance=Math.Max(0,Math.Min(1,mean*(1+(noise-.5)*2*Math.Max(0,Math.Min(100,d.Variance))/100)/100));
            return true;
        }
        private static void Select(List<ResourceData> nodes,Settings s,string biome,string resource,int level,ref ResourceData found)
        {
            foreach(var n in nodes)if(n.ResourceType==0&&n.ResourceName==resource&&
                (level==0||n.PlanetName==s.RingId||n.PlanetName==s.RingName||n.PlanetName=="Ringworld:"+s.RingId)&&
                (level<2||n.BiomeName==biome))found=n;
        }
        private static int Hash(string text){unchecked{uint h=2166136261;foreach(char c in text){h^=c;h*=16777619;}return (int)(h&0x7fffffff);}}
    }
}
