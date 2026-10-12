using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using HarmonyLib;
namespace NivenRingworld
{
    // AJE uses SolverEngines' own engine/inlet loops, outside stock CalculateThrust.
    // Keep its solver, fitting, throttle and resource calculations intact.
    [HarmonyPatch]
    internal static class RingSolverEngineCompatibility
    {
        private static bool Prepare(){return RingAdapterOptions.Enabled("solverEngines")&&AccessTools.TypeByName("SolverEngines.ModuleEnginesSolver")!=null;}
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(var target in new[]{
                new[]{"SolverEngines.ModuleEnginesSolver","FixedUpdate"},new[]{"SolverEngines.ModuleEnginesSolver","CheckTransformsUnderwater"},
                new[]{"SolverEngines.AJEInlet","FixedUpdate"},new[]{"SolverEngines.AJEInlet","InAtmosphere"},new[]{"SolverEngines.AJEInlet","CheckUnderwater"}})
            {
                var type=AccessTools.TypeByName(target[0]);var method=type==null?null:AccessTools.DeclaredMethod(type,target[1]);
                if(method==null||method.IsStatic||!typeof(PartModule).IsAssignableFrom(type))throw new NotSupportedException("SolverEngines environment method changed: "+string.Join(".",target));
                yield return method;
            }
        }
        internal static double Altitude(Vessel vessel)
        {RingworldSurfaceState state;return RingworldSurfaceApi.TryGetSurfaceState(vessel,out state)?state.Altitude:vessel.altitude;}
        internal static double AtmosphereDepth(CelestialBody body,PartModule module)
        {return VesselAtmosphereDepth(body,module.vessel);}
        internal static double VesselAtmosphereDepth(CelestialBody body,Vessel vessel)
        {var f=RingworldFlight.Instance;return f!=null&&StockIntegration.Applies(vessel)&&body==vessel.mainBody?(f.Settings.Atmosphere?f.Settings.Geometry.P.AtmosphereHeight:0):body.atmosphereDepth;}
        internal static float PositionAltitude(UnityEngine.Vector3 position,PartModule module)
        {return (float)RingAquaticCompatibility.Altitude(position,module.vessel.mainBody,module);}
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source,MethodBase __originalMethod)
        {
            var altitude=AccessTools.Field(typeof(Vessel),"altitude");var depth=AccessTools.Field(typeof(CelestialBody),"atmosphereDepth");
            var position=AccessTools.Method(typeof(FlightGlobals),"getAltitudeAtPos",new[]{typeof(UnityEngine.Vector3)});
            var codes=RingIntakeEnvironment.Transpiler(source,__originalMethod).ToList();
            if(__originalMethod.DeclaringType.Name=="ModuleEnginesSolver"&&__originalMethod.Name=="FixedUpdate"&&
                !codes.Any(c=>c.Calls(AccessTools.Method(typeof(RingIntakeEnvironment),"Oxygen"))))
                throw new NotSupportedException("SolverEngines oxygen query changed; its ring engine adapter cannot be installed.");
            foreach(var original in codes)
            {
                var code=new CodeInstruction(original);
                if(code.LoadsField(altitude)){code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingSolverEngineCompatibility),nameof(Altitude));}
                else if(code.LoadsField(depth)||code.Calls(position)){
                    string helper=code.LoadsField(depth)?nameof(AtmosphereDepth):nameof(PositionAltitude);
                    var arg=new CodeInstruction(OpCodes.Ldarg_0);arg.labels.AddRange(code.labels);code.labels.Clear();arg.blocks.AddRange(code.blocks);code.blocks.Clear();yield return arg;
                    code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingSolverEngineCompatibility),helper);
                }
                yield return code;
            }
        }
    }
    [HarmonyPatch]
    internal static class RingSolverFlightSystemCompatibility
    {
        private static bool Prepare(){return RingAdapterOptions.Enabled("solverEngines")&&AccessTools.TypeByName("SolverEngines.SolverFlightSys")!=null;}
        private static MethodBase TargetMethod(){return AccessTools.DeclaredMethod(AccessTools.TypeByName("SolverEngines.SolverFlightSys"),"FixedUpdate");}
        private static double AtmosphereDepth(CelestialBody body,VesselModule module)
        {return RingSolverEngineCompatibility.VesselAtmosphereDepth(body,module.GetComponent<Vessel>());}
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var altitude=AccessTools.Field(typeof(Vessel),"altitude");var depth=AccessTools.Field(typeof(CelestialBody),"atmosphereDepth");bool found=false;
            var speed=AccessTools.Field(typeof(Vessel),"srfSpeed");var velocity=AccessTools.Field(typeof(Vessel),"srf_velocity");
            foreach(var original in source){
                var code=new CodeInstruction(original);
                if(code.LoadsField(speed)||code.LoadsField(velocity)){
                    string helper=code.LoadsField(speed)?"Speed":"Velocity";code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingIntakeEnvironment),helper);
                }
                else if(code.LoadsField(altitude)){code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingSolverEngineCompatibility),"Altitude");}
                else if(code.LoadsField(depth)){
                    found=true;var arg=new CodeInstruction(OpCodes.Ldarg_0);arg.labels.AddRange(code.labels);code.labels.Clear();arg.blocks.AddRange(code.blocks);code.blocks.Clear();yield return arg;
                    code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingSolverFlightSystemCompatibility),"AtmosphereDepth");
                }
                else if(code.Calls(AccessTools.Method(typeof(RingSolverFlightSystemCompatibility),"AtmosphereDepth")))found=true;
                yield return code;
            }
            if(!found)throw new NotSupportedException("SolverEngines vessel atmosphere gate changed; ring inlet distribution cannot be adapted.");
        }
    }
}
