using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
namespace NivenRingworld
{
    // Optional SunkWorks/Buffalo call-site bridge, never a global ocean flag.
    [HarmonyPatch]
    internal static class RingAquaticCompatibility
    {
        private static IEnumerable<MethodBase> Targets()
        {
            if(!RingAdapterOptions.Enabled("aquaticModules"))yield break;
            foreach(var name in new[]{"SunkWorks.Submarine.WBIBallastTank","SunkWorks.Submarine.WBIAquaticEngine","WildBlueIndustries.WBIBallastTank","WildBlueIndustries.WBIAquaticEngine"})
            {
                var type=AccessTools.TypeByName(name);if(type==null||!typeof(PartModule).IsAssignableFrom(type))continue;
                foreach(var method in new[]{"updateBallastResource","checkUnderwater"})
                {var m=AccessTools.DeclaredMethod(type,method);if(m!=null&&!m.IsStatic)yield return m;}
            }
        }
        private static bool Prepare(){foreach(var m in Targets())return true;return false;}
        private static IEnumerable<MethodBase> TargetMethods(){return Targets();}
        internal static bool Ocean(CelestialBody body,PartModule module)
        {return StockIntegration.Applies(module.vessel)||body.ocean;}
        internal static double Altitude(Vector3d position,CelestialBody body,PartModule module)
        {
            if(!StockIntegration.Applies(module.vessel))return FlightGlobals.getAltitudeAtPos(position,body);
            var f=RingworldFlight.Instance;var c=f.Settings.Geometry.Coordinates(ConvertVector.Core(position-f.Center));var t=f.Settings.Terrain.Sample(c.Along,c.Across);
            return t.Wet?c.Altitude-t.WaterHeight:Math.Max(1,c.Altitude-t.Height);
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var ocean=AccessTools.Field(typeof(CelestialBody),"ocean");var altitude=AccessTools.Method(typeof(FlightGlobals),"getAltitudeAtPos",new[]{typeof(Vector3d),typeof(CelestialBody)});
            foreach(var code in source)
            {
                string replacement=code.opcode==OpCodes.Ldfld&&Equals(code.operand,ocean)?"Ocean":code.Calls(altitude)?"Altitude":null;
                if(replacement!=null)
                {
                    var arg=new CodeInstruction(OpCodes.Ldarg_0);arg.labels.AddRange(code.labels);code.labels.Clear();yield return arg;
                    code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingAquaticCompatibility),replacement);
                }
                yield return code;
            }
        }
    }
}
