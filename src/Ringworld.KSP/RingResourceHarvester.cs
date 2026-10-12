using System;
using System.Reflection;
using HarmonyLib;
namespace NivenRingworld
{
    [HarmonyPatch(typeof(ModuleResourceHarvester),"PrepareRecipe")]
    internal static class RingResourceHarvester
    {
        [ThreadStatic] internal static ModuleResourceHarvester Current;
        internal struct QueryScope {internal ModuleResourceHarvester Previous;internal bool Entered;}
        private static bool Prepare(){return RingAdapterOptions.Enabled("stockHarvesters");}
        [HarmonyPriority(Priority.First)]
        internal static void Prefix(ModuleResourceHarvester __instance,out QueryScope __state)
        {__state=new QueryScope{Previous=Current,Entered=true};Current=__instance;RingSurfaceCaches.Publish(__instance.vessel);}
        internal static void Finalizer(QueryScope __state){if(__state.Entered)Current=__state.Previous;}
        internal static bool TryAbundance(AbundanceRequest request,PartModule module,out float value)
        {
            value=0;var harvester=module as ModuleResourceHarvester;
            if(harvester==null||harvester!=Current||request.ResourceName!=harvester.ResourceName||
                (int)request.ResourceType!=harvester.HarvesterType||harvester.vessel==null||harvester.vessel.mainBody==null||
                request.BodyId!=harvester.vessel.mainBody.flightGlobalsIndex)return false;
            RingworldSurfaceState state;
            if(!RingworldSurfaceApi.TryGetSurfaceState(harvester.vessel,out state))return false;
            double abundance;
            if(harvester.HarvesterType==0&&RingworldResourceApi.TryGetAbundance(harvester.vessel,harvester.ResourceName,out abundance))value=(float)abundance;
            return true;
        }
        internal static float Abundance(ResourceMap map,AbundanceRequest request,ModuleResourceHarvester module)
        {
            var previous=Current;Current=module;
            try{float value;return TryAbundance(request,module,out value)?value:map.GetAbundance(request);}
            finally{Current=previous;}
        }
    }
    // Scope the resource query instead of requiring a particular PrepareRecipe
    // IL body. Other optimizers may wrap the call or move stock field reads.
    [HarmonyPatch(typeof(ResourceMap),"GetAbundance",new[]{typeof(AbundanceRequest)})]
    internal static class RingHarvesterResourceQuery
    {
        private static bool Prepare(){return RingAdapterOptions.Enabled("stockHarvesters");}
        private static bool Prefix(AbundanceRequest __0,ref float __result)
        {float value;if(!RingResourceHarvester.TryAbundance(__0,RingResourceHarvester.Current,out value))return true;__result=value;return false;}
    }
    // KSPCF's cache key omits ring identity/biome. Bypass only a ring harvester's
    // scoped query, including cache hits. Leave its stock-body cache untouched.
    [HarmonyPatch]
    internal static class RingCommunityFixesResourceCache
    {
        internal static MethodBase TargetMethod()
        {
            var type=AccessTools.TypeByName("KSPCommunityFixes.Performance.ResourceConverterPerf");
            return type==null?null:AccessTools.Method(type,"GetAbundanceCached",new[]{typeof(ResourceMap),typeof(AbundanceRequest),typeof(PartModule)});
        }
        private static bool Prepare()
        {
            if(!RingAdapterOptions.Enabled("stockHarvesters"))return false;
            if(AccessTools.TypeByName("KSPCommunityFixes.Performance.ResourceConverterPerf")==null)return false;
            if(TargetMethod()==null)throw new NotSupportedException("KSPCF ResourceConverterPerf cache signature changed; ring cache isolation is unavailable.");
            return true;
        }
        private static bool Prefix(AbundanceRequest __1,PartModule __2,ref float __result)
        {float value;if(!RingResourceHarvester.TryAbundance(__1,__2,out value))return true;__result=value;return false;}
    }
}
