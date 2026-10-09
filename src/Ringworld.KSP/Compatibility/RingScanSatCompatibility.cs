using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
namespace NivenRingworld
{
    // Local part display only. SCANsat's longitude/latitude orbital map and
    // survey coverage are spherical and cannot represent cylindrical terrain.
    [HarmonyPatch]
    internal static class RingScanSatCompatibility
    {
        static System.Type Type=>AccessTools.TypeByName("SCANsat.SCAN_PartModules.SCANresourceDisplay");
        static bool Prepare()=>Type!=null&&RingAdapterOptions.Enabled("scanSatLocalResources");
        static IEnumerable<MethodBase> TargetMethods(){yield return AccessTools.Method(Type,"Update");yield return AccessTools.Method(Type,"FixedUpdate");}
        static bool Prefix(PartModule __instance,bool ___activated,ref float ___abundanceValue)
        {
            RingworldSurfaceState state;if(!RingworldSurfaceApi.TryGetSurfaceState(__instance.vessel,out state))return true;
            var display=__instance.Fields["abundance"];display.guiActive=___activated;
            if(!___activated){___abundanceValue=-1;return false;}
            string resource=(string)__instance.Fields["ResourceName"].GetValue(__instance);
            double abundance;bool known=RingworldResourceApi.TryGetAbundance(__instance.vessel,resource,out abundance);
            double max=(float)AccessTools.Property(Type,"MaxAbundanceAltitude").GetValue(__instance,null);
            bool tooHigh=!__instance.vessel.LandedOrSplashed&&state.Altitude-state.TerrainElevation>max;
            ___abundanceValue=known&&!tooHigh?(float)abundance:-1;
            display.guiName=resource+" [Ring surface]";
            display.SetValue(tooHigh?"Too high above ring terrain":known?abundance.ToString("P2")+" / "+state.Biome:"Ring resource definition unavailable",__instance);
            return false;
        }
    }
}
