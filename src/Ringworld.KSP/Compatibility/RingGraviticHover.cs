using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    [HarmonyPatch]
    internal static class RingGraviticHover
    {
        private static Type Engine {get{return AccessTools.TypeByName("WildBlueIndustries.WBIGraviticEngine");}}
        private static bool Prepare(){return RingAdapterOptions.Enabled("kfsHover")&&Engine!=null&&AccessTools.Method(Engine,"UpdateHoverState")!=null;}
        private static MethodBase TargetMethod(){return AccessTools.Method(Engine,"UpdateHoverState");}
        internal static Vector3d Gravity(Vessel vessel)
        {RingworldSurfaceState s;return RingworldSurfaceApi.TryGetSurfaceState(vessel,out s)?s.StationaryFrameAcceleration:vessel.graviticAcceleration;}
        internal static Vector3d LiftReference(CelestialBody body,PartModule module)
        {
            RingworldSurfaceState s;
            // This synthetic reference is used only by the hover direction expression;
            // it never creates a body or changes an orbit/reference-body property.
            return RingworldSurfaceApi.TryGetSurfaceState(module.vessel,out s)?module.vessel.GetWorldPos3D()-s.SurfaceUp*1000:body.position;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source,ILGenerator generator)
        {
            var codes=new List<CodeInstruction>(source);var field=AccessTools.Field(typeof(Vessel),"graviticAcceleration");
            var position=AccessTools.PropertyGetter(typeof(CelestialBody),"position");
            bool hasGravity=false,hasPosition=false;
            foreach(var c in codes){if(c.operand as FieldInfo==field)hasGravity=true;if(c.Calls(position))hasPosition=true;}
            if(!hasGravity||!hasPosition){Debug.LogWarning("[NivenRingworld] KFS hover signature changed; adapter skipped.");return codes;}
            var result=new List<CodeInstruction>();var local=generator.DeclareLocal(typeof(Vector3d));
            foreach(var c in codes)
            {
                if(c.operand as FieldInfo==field&&(c.opcode==OpCodes.Ldfld||c.opcode==OpCodes.Ldflda))
                {
                    bool address=c.opcode==OpCodes.Ldflda;c.opcode=OpCodes.Call;c.operand=AccessTools.Method(typeof(RingGraviticHover),"Gravity");result.Add(c);
                    if(address){result.Add(new CodeInstruction(OpCodes.Stloc,local));result.Add(new CodeInstruction(OpCodes.Ldloca,local));}
                }
                else if(c.Calls(position))
                {
                    var arg=new CodeInstruction(OpCodes.Ldarg_0);arg.labels.AddRange(c.labels);c.labels.Clear();result.Add(arg);
                    c.opcode=OpCodes.Call;c.operand=AccessTools.Method(typeof(RingGraviticHover),"LiftReference");result.Add(c);
                }
                else result.Add(c);
            }
            return result;
        }
    }
}
