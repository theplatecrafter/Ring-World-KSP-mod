using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
namespace NivenRingworld
{
    [HarmonyPatch(typeof(ModuleResourceHarvester),"PrepareRecipe")]
    internal static class RingResourceHarvester
    {
        internal static float Abundance(ResourceMap map,AbundanceRequest request,ModuleResourceHarvester module)
        {
            RingworldSurfaceState state;
            if(!RingworldSurfaceApi.TryGetSurfaceState(module.vessel,out state))return map.GetAbundance(request);
            double value;
            return module.HarvesterType==0&&RingworldResourceApi.TryGetAbundance(module.vessel,module.ResourceName,out value)?(float)value:0;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var method=AccessTools.Method(typeof(ResourceMap),"GetAbundance",new[]{typeof(AbundanceRequest)});bool replaced=false;
            foreach(var code in source)
            {
                if(code.Calls(method))
                {
                    var arg=new CodeInstruction(OpCodes.Ldarg_0);arg.labels.AddRange(code.labels);code.labels.Clear();yield return arg;
                    code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingResourceHarvester),"Abundance");replaced=true;
                }
                yield return code;
            }
            if(!replaced)throw new InvalidOperationException("Stock harvester resource query not found");
        }
    }
}
