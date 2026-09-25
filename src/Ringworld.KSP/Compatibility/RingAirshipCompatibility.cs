using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
namespace NivenRingworld
{
    // Optional Hooligan Labs adapter. Change only this module's query call sites;
    // never change the host star or the gravity used by the stock integrator.
    [HarmonyPatch]
    internal static class RingAirshipCompatibility
    {
        private static IEnumerable<System.Type> ModuleTypes()
        {
            foreach(var pair in new[]{new[]{"hooliganLabsAirships","HLAirships.HLEnvelopePartModule"},new[]{"heisenbergLift","WildBlueIndustries.WBIModuleStaticLift"}})
            {
                var type=AccessTools.TypeByName(pair[1]);
                if(type!=null&&RingAdapterOptions.Enabled(pair[0]))yield return type;
            }
        }
        private static bool Prepare(){foreach(var type in ModuleTypes())return true;return false;}
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(var type in ModuleTypes())
            foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                if(method.GetMethodBody()!=null&&!method.ContainsGenericParameters)yield return method;
        }
        internal static Vector3d LiftReference(CelestialBody body,PartModule module,bool perPart)
        {
            RingworldSurfaceState state;
            return RingworldSurfaceApi.TryGetSurfaceState(module.vessel,out state)?
                (perPart?module.part.WCoM:module.vessel.CoM)-state.SurfaceUp*1000:body.position;
        }
        internal static Vector3d Gravity(Vector3d position,PartModule module)
        {RingworldPointEnvironment s;return RingworldSurfaceApi.TryGetEnvironmentAtPosition(module.vessel,position,out s)?s.EffectiveGravity:FlightGlobals.getGeeForceAtPosition(position);}
        internal static double Pressure(PartModule module)
        {RingworldPointEnvironment s;return State(module,out s)?s.PressureKPa:FlightGlobals.getStaticPressure();}
        internal static double Temperature(PartModule module)
        {RingworldPointEnvironment s;return State(module,out s)?s.TemperatureKelvin:FlightGlobals.getExternalTemperature();}
        internal static double Density(double pressure,double temperature,CelestialBody body,PartModule module)
        {RingworldPointEnvironment s;return State(module,out s)?s.AirDensity:FlightGlobals.getAtmDensity(pressure,temperature,body);}
        private static bool State(PartModule m,out RingworldPointEnvironment s)
        {s=default(RingworldPointEnvironment);return m.vessel!=null&&RingworldSurfaceApi.TryGetEnvironmentAtPosition(m.vessel,m.vessel.GetWorldPos3D(),out s);}
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source,ILGenerator generator,MethodBase __originalMethod)
        {
            var methods=new Dictionary<MethodInfo,string>{
                {AccessTools.Method(typeof(FlightGlobals),"getGeeForceAtPosition",new[]{typeof(Vector3d)}),"Gravity"},
                {AccessTools.Method(typeof(FlightGlobals),"getStaticPressure",System.Type.EmptyTypes),"Pressure"},
                {AccessTools.Method(typeof(FlightGlobals),"getExternalTemperature",System.Type.EmptyTypes),"Temperature"},
                {AccessTools.Method(typeof(FlightGlobals),"getAtmDensity",new[]{typeof(double),typeof(double),typeof(CelestialBody)}),"Density"}};
            var gravity=AccessTools.Field(typeof(Vessel),"gravityForPos");
            var position=AccessTools.PropertyGetter(typeof(CelestialBody),"position");
            var local=generator.DeclareLocal(typeof(Vector3d));
            foreach(var code in source)
            {
                if(code.operand as FieldInfo==gravity&&(code.opcode==OpCodes.Ldfld||code.opcode==OpCodes.Ldflda))
                {
                    bool address=code.opcode==OpCodes.Ldflda;code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingWheelGravity),"Gravity");yield return code;
                    if(address){yield return new CodeInstruction(OpCodes.Stloc,local);yield return new CodeInstruction(OpCodes.Ldloca,local);}
                    continue;
                }
                if(code.Calls(position)&&__originalMethod.DeclaringType.FullName=="WildBlueIndustries.WBIModuleStaticLift")
                {
                    var arg=new CodeInstruction(OpCodes.Ldarg_0);arg.labels.AddRange(code.labels);code.labels.Clear();yield return arg;
                    yield return new CodeInstruction(__originalMethod.Name=="applyPerPartLift"?OpCodes.Ldc_I4_1:OpCodes.Ldc_I4_0);
                    code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingAirshipCompatibility),"LiftReference");yield return code;continue;
                }
                string target;var method=code.operand as MethodInfo;
                if(method!=null&&methods.TryGetValue(method,out target)&&(code.opcode==OpCodes.Call||code.opcode==OpCodes.Callvirt))
                {
                    var arg=new CodeInstruction(OpCodes.Ldarg_0);arg.labels.AddRange(code.labels);code.labels.Clear();yield return arg;
                    code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(RingAirshipCompatibility),target);
                }
                yield return code;
            }
        }
    }
}
