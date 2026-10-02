using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace NivenRingworld
{
    // Preserve stock helmet pressure/temperature thresholds and safety margins.
    // Only substitute the local environment read by that check; never edit Sun.
    [HarmonyPatch(typeof(KerbalEVA),"CheckHelmetOffSafe")]
    internal static class RingHelmetEnvironment
    {
        internal static bool Atmosphere(CelestialBody body,KerbalEVA eva)
        {return StockIntegration.Applies(eva.vessel)?RingAir.Applies(eva.vessel)&&RingAir.Sample(eva.vessel).Density>0:body.atmosphere;}
        internal static bool Oxygen(CelestialBody body,KerbalEVA eva)
        {return StockIntegration.Applies(eva.vessel)?RingAir.ContainsOxygen(eva.vessel):body.atmosphereContainsOxygen;}
        internal static double Depth(CelestialBody body,KerbalEVA eva)
        {return StockIntegration.Applies(eva.vessel)?double.PositiveInfinity:body.atmosphereDepth;}
        internal static double Temperature(CelestialBody body,double altitude,KerbalEVA eva)
        {return RingAir.Applies(eva.vessel)?RingAir.Sample(eva.vessel).Temperature:body.GetTemperature(altitude);}
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var atmosphere=AccessTools.Field(typeof(CelestialBody),"atmosphere");
            var oxygen=AccessTools.Field(typeof(CelestialBody),"atmosphereContainsOxygen");
            var depth=AccessTools.Field(typeof(CelestialBody),"atmosphereDepth");
            var temperature=AccessTools.Method(typeof(CelestialBody),"GetTemperature",new[]{typeof(double)});
            foreach(var instruction in instructions)
            {
                string helper=instruction.LoadsField(atmosphere)?"Atmosphere":instruction.LoadsField(oxygen)?"Oxygen":instruction.LoadsField(depth)?"Depth":instruction.Calls(temperature)?"Temperature":null;
                if(helper!=null)
                {
                    var load=new CodeInstruction(OpCodes.Ldarg_0);load.labels.AddRange(instruction.labels);instruction.labels.Clear();
                    load.blocks.AddRange(instruction.blocks);instruction.blocks.Clear();yield return load;
                    instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(RingHelmetEnvironment),helper);
                }
                yield return instruction;
            }
        }
    }
    [HarmonyPatch(typeof(KerbalEVA),"GetPreLoadPressure")]
    internal static class RingHelmetPreloadPressure
    {
        private static bool Prefix(KerbalEVA __instance,ref double __result)
        {
            if(!StockIntegration.Applies(__instance.vessel))return true;
            __result=RingAir.Applies(__instance.vessel)?RingAir.Sample(__instance.vessel).PressureKPa/101.325:0;return false;
        }
    }
}
