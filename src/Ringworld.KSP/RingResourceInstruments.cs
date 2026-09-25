using HarmonyLib;
namespace NivenRingworld
{
    // Stock scanner coordinates identify the host star. Never unlock its resource
    // biomes or report its abundances as ring deposits. The shared ring provider
    // feeds local scanners and stock harvester abundance queries.
    internal static class RingResourceInstruments
    {
        internal static bool Context(PartModule module,out RingworldSurfaceState state)
        { return RingworldSurfaceApi.TryGetSurfaceState(module.vessel,out state); }
    }
    [HarmonyPatch(typeof(ModuleBiomeScanner),"RunAnalysis")]
    internal static class RingBiomeAnalysis
    {
        private static bool Prefix(ModuleBiomeScanner __instance)
        {
            RingworldSurfaceState state;
            if(!RingResourceInstruments.Context(__instance,out state))return true;
            if(!__instance.vessel.LandedOrSplashed||__instance.vessel.CurrentControlLevel<=Vessel.ControlLevel.NONE)return false;
            Ringworld.Core.ResearchLocation location;
            string site=RingworldResearchApi.TryGetLocation(__instance.vessel,out location)?location.Name:state.Biome;
            ScreenMessages.PostScreenMessage(state.RingName+" / "+site+" — biome: "+state.Biome+". Local resource sampling available; orbital survey mapping is not implemented.",8f,ScreenMessageStyle.UPPER_CENTER);
            return false;
        }
    }
    [HarmonyPatch(typeof(ModuleBiomeScanner),"FixedUpdate")]
    internal static class RingBiomeScannerAvailability
    {
        private static bool Prefix(ModuleBiomeScanner __instance)
        {
            RingworldSurfaceState state;if(!RingResourceInstruments.Context(__instance,out state))return true;
            __instance.Events["RunAnalysis"].active=__instance.vessel.LandedOrSplashed;
            return false;
        }
    }
    [HarmonyPatch(typeof(ModuleResourceScanner),"Update")]
    internal static class RingAbundanceDisplay
    {
        private static bool Prefix(ModuleResourceScanner __instance,ref double ___abundanceValue)
        {
            RingworldSurfaceState state;if(!RingResourceInstruments.Context(__instance,out state))return true;
            double abundance=0;
            bool known=__instance.ScannerType==0&&RingworldResourceApi.TryGetAbundance(__instance.vessel,__instance.ResourceName,out abundance);
            if(!known)abundance=0;
            ___abundanceValue=abundance;
            __instance.abundanceDisplay=known?(abundance*100).ToString("F2")+"% (ring)":"Ring resource definition unavailable";
            __instance.Fields["abundanceDisplay"].guiActive=true;
            return false;
        }
    }
}
