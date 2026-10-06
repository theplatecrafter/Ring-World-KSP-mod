#if RINGWORLD_SMOKE_TEST
using System;
using System.IO;
using UnityEngine;
namespace NivenRingworld
{
    internal static class ConfigPackSmoke
    {
        internal static void Run()
        {
            foreach(string suffix in new[]{"Standard","FullSize"})
            {
                string id="NivenRingworldKerbol"+suffix;
                string file=Path.Combine(KSPUtil.ApplicationRootPath,"..","Ringworld Configs","packs",id,"GameData",id,"Rings.cfg");
                var packs=ConfigNode.Load(file).GetNodes("NIVEN_RINGWORLD_SYSTEM");
                var rings=RingConfigPacks.Parse(packs);
                if(rings.Count!=1)throw new Exception("Config pack ring count");
                var settings=Settings.Load();settings.Apply(rings[0]);
                bool full=suffix=="FullSize";
                if(settings.Geometry.P.Radius!=(full?150000000000:15300000000)||settings.Geometry.P.WallHeight!=(full?1600000:160000)||settings.Geometry.P.DaySeconds!=(full?108000:10800))throw new Exception("Config dimensions/day cycle mismatch");
                var restored=Settings.Load();restored.Apply(settings.Save());
                if(restored.Geometry.P.WallHeight!=settings.Geometry.P.WallHeight||restored.Geometry.P.AtmosphereHeight!=settings.Geometry.P.AtmosphereHeight)throw new Exception("Config construction persistence");
                bool rejected=false;try{RingConfigPacks.Parse(new[]{packs[0],packs[0].CreateCopy()});}catch(ArgumentException){rejected=true;}
                if(!rejected)throw new Exception("Duplicate replacement config accepted");
                var invalid=packs[0].CreateCopy();invalid.GetNode("RING").SetValue("anchorId","body:MissingRegressionHost",true);
                rejected=false;try{RingConfigPacks.Parse(new[]{invalid});}catch(ArgumentException){rejected=true;}
                if(!rejected)throw new Exception("Missing anchor silently accepted");
                Debug.Log("[RingworldSmoke] CONFIG pack "+id+" loaded; dimensions, day cycle, persistence and invalid input rejection passed");
            }
        }
    }
}
#endif
