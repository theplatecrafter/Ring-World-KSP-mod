using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
namespace NivenRingworld
{
    // Shared, vessel-scoped stock module environment reads. Never modify the host body.
    // Keep stock occlusion, flow, Mach, resource and thermal calculations.
    [HarmonyPatch]
    internal static class RingIntakeEnvironment
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ModuleResourceIntake),"FixedUpdate");
            yield return AccessTools.Method(typeof(ModuleEngines),"CalculateThrust");
            yield return AccessTools.Method(typeof(ModuleEngines),"CheckTransformsUnderwater");
            yield return AccessTools.Method(typeof(ModuleDeployableSolarPanel),"PostCalculateTracking");
            yield return AccessTools.Method(typeof(ModuleEnviroSensor),"FixedUpdate");
        }
        internal static Vector3d Gravity(Vector3d position,PartModule module)
        {
            RingworldPointEnvironment state;
            return RingworldSurfaceApi.TryGetEnvironmentAtPosition(module.vessel,position,out state)?state.EffectiveGravity:FlightGlobals.getGeeForceAtPosition(position);
        }
        // This field is only a validity cutoff in the stock gravity instrument.
        internal static double SensorRadius(CelestialBody body,PartModule module)
        {return StockIntegration.Applies(module.vessel)?double.PositiveInfinity:body.Radius;}

        internal static bool Oxygen(CelestialBody body,PartModule module)
        {return StockIntegration.Applies(module.vessel)?RingAir.ContainsOxygen(module.vessel):body.atmosphereContainsOxygen;}
        internal static bool Ocean(CelestialBody body,PartModule module)
        {return RingAquaticCompatibility.Ocean(body,module);}
        internal static double Altitude(Vector3d position,CelestialBody body,PartModule module)
        {return RingAquaticCompatibility.Altitude(position,body,module);}
        internal static double OceanDensity(CelestialBody body,PartModule module)
        {return StockIntegration.Applies(module.vessel)?1.0:body.oceanDensity;}
        internal static double Speed(Vessel v)
        {return StockIntegration.Applies(v)?RingworldFlight.Instance.Velocity(v).Length:v.srfSpeed;}
        internal static Vector3d Direction(Vessel v)
        {return StockIntegration.Applies(v)?ConvertVector.Ksp(RingworldFlight.Instance.Velocity(v)).normalized:v.srf_vel_direction;}
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source,MethodBase __originalMethod)
        {
            var oxygen=AccessTools.Field(typeof(CelestialBody),"atmosphereContainsOxygen");
            var ocean=AccessTools.Field(typeof(CelestialBody),"ocean");
            var density=AccessTools.Field(typeof(CelestialBody),"oceanDensity");
            var altitude=AccessTools.Method(typeof(FlightGlobals),"getAltitudeAtPos",new[]{typeof(Vector3d),typeof(CelestialBody)});
            var speed=AccessTools.Field(typeof(Vessel),"srfSpeed");
            var direction=AccessTools.Field(typeof(Vessel),"srf_vel_direction");
            var gravity=AccessTools.Method(typeof(FlightGlobals),"getGeeForceAtPosition",new[]{typeof(Vector3d)});
            var radius=AccessTools.Field(typeof(CelestialBody),"Radius");
            int oxygenReads=0;
            foreach(var code in source)
            {
                if(code.LoadsField(speed)||code.LoadsField(direction))
                {
                    var method=code.LoadsField(speed)?"Speed":"Direction";
                    code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingIntakeEnvironment),method);
                    yield return code;continue;
                }
                string helper=code.LoadsField(oxygen)?"Oxygen":code.LoadsField(ocean)?"Ocean":code.LoadsField(density)?"OceanDensity":code.Calls(altitude)?"Altitude":code.Calls(gravity)?"Gravity":__originalMethod.DeclaringType==typeof(ModuleEnviroSensor)&&code.LoadsField(radius)?"SensorRadius":null;
                if(helper!=null)
                {
                    if(helper=="Oxygen")oxygenReads++;
                    var arg=new CodeInstruction(OpCodes.Ldarg_0);arg.labels.AddRange(code.labels);code.labels.Clear();arg.blocks.AddRange(code.blocks);code.blocks.Clear();yield return arg;
                    code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingIntakeEnvironment),helper);
                }
                yield return code;
            }
            if(__originalMethod.DeclaringType==typeof(ModuleResourceIntake)&&oxygenReads==0)throw new System.InvalidOperationException("Stock intake oxygen gate not found; ring intake compatibility cannot be installed.");
        }
    }
}
