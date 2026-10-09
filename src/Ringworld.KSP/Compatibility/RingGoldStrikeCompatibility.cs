using System.Reflection;
using HarmonyLib;
namespace NivenRingworld
{
    // GoldStrike inherits the stock crustal harvester. Its additional lode
    // lookup is planet-indexed; do not borrow rich lodes from the host star.
    // Ordinary ring deposits still use the shared stock harvesting bridge.
    [HarmonyPatch]
    internal static class RingGoldStrikeCompatibility
    {
        static System.Type Type=>AccessTools.TypeByName("WBIResources.WBIGoldStrikeDrill");
        static bool Prepare()=>Type!=null&&RingAdapterOptions.Enabled("goldStrikeDrilling");
        static MethodBase TargetMethod()=>AccessTools.Method(Type,"findNearestLode");
        static bool Prefix(PartModule __instance)
        {
            if(!StockIntegration.Applies(__instance.vessel))return true;
            AccessTools.Field(Type,"nearestLode").SetValue(__instance,null);
            __instance.Fields["lodeStatus"].SetValue("Ring deposits; planetary lodes unavailable",__instance);
            return false;
        }
    }
}
