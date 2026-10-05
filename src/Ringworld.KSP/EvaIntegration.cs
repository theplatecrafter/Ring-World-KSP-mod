using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Ringworld.Core;
using UnityEngine;

namespace NivenRingworld
{
    internal static class RingEva
    {
        internal static bool Applies(KerbalEVA eva)
        {
            var f=RingworldFlight.Instance;
            return f!=null&&eva!=null&&f.AdoptParticipant(eva.vessel);
        }
        internal static bool Grounded(KerbalEVA eva)
        {
            if(eva.vessel.packed)return false;
            float height=eva.vessel.GetHeightFromSurface();
            return height>=0&&height<=eva.halfHeight+.15f&&Vector3.Dot(eva.vessel.HeightFromSurfaceHit.normal,eva.fUp)>.4f;
        }
    }
    // State changes seed walking interpolation from horizontalSrfSpeed. At the
    // ring this stock cache describes the Sun, not the local physics frame.
    // Replace the reads, including landing/jump decisions, without speed caps.
    [HarmonyPatch]
    internal static class EvaTransitionSpeedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(var name in new[]{"heading_acquire_OnLeave","bound_fl_OnLeave","land_OnEnter","jump_OnEnter"})
                yield return AccessTools.Method(typeof(KerbalEVA),name);
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var field=AccessTools.Field(typeof(Vessel),"horizontalSrfSpeed");
            var replacement=AccessTools.Method(typeof(EvaTransitionSpeedPatch),nameof(HorizontalSpeed));
            int replaced=0;
            foreach(var instruction in instructions)
            {
                if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,field))
                {instruction.opcode=OpCodes.Call;instruction.operand=replacement;replaced++;}
                yield return instruction;
            }
            if(replaced==0)throw new System.InvalidOperationException("KSP EVA speed read was not found; incompatible game assembly.");
        }
        internal static double HorizontalSpeed(Vessel v)
        {
            if(!StockIntegration.Applies(v))return v.horizontalSrfSpeed;
            var f=RingworldFlight.Instance;
            var velocity=f.Velocity(v);var up=f.Settings.Geometry.Up(f.Position(v));
            return (velocity-up*DVec.Dot(velocity,up)).Length;
        }
    }
    [HarmonyPatch(typeof(KerbalEVA),"getCoordinateFrame")]
    internal static class EvaFramePatch
    {
        private static bool Prefix(KerbalEVA __instance)
        {
            if(!RingEva.Applies(__instance))return true;
            var f=RingworldFlight.Instance;
            __instance.fUp=ConvertVector.Unity(f.Settings.Geometry.Up(f.Position(__instance.vessel)));
            var camera=FlightCamera.fetch;
            var forward=camera!=null?camera.mainCamera.transform.forward:__instance.transform.forward;
            __instance.fFwd=Vector3.ProjectOnPlane(forward,__instance.fUp).normalized;
            if(__instance.fFwd.sqrMagnitude<.01f)__instance.fFwd=ConvertVector.Unity(f.Settings.Geometry.Axis);
            __instance.fRgt=Vector3.Cross(__instance.fUp,__instance.fFwd);
            return false;
        }
    }
    [HarmonyPatch(typeof(Vessel),nameof(Vessel.GetHeightFromSurface))]
    internal static class RingSurfaceHeightPatch
    {
        private static bool Prefix(Vessel __instance,ref float __result,ref RaycastHit ___heightFromSurfaceHit,ref float ___heightFromSurface)
        {
            if(!StockIntegration.Applies(__instance)||__instance.packed)return true;
            var f=RingworldFlight.Instance;var up=ConvertVector.Unity(f.Settings.Geometry.Up(f.Position(__instance)));
            bool hit=Physics.Raycast(__instance.transform.position,-up,out ___heightFromSurfaceHit,100000,1<<15,QueryTriggerInteraction.Ignore);
            __result=___heightFromSurface=hit?___heightFromSurfaceHit.distance:-1;
            return false;
        }
    }
    [HarmonyPatch(typeof(KerbalEVA),"SurfaceOrSplashed")]
    internal static class EvaGroundPatch
    {
        private static bool Prefix(KerbalEVA __instance,ref bool __result)
        {
            if(!RingEva.Applies(__instance))return true;
            __result=RingEva.Grounded(__instance);return false;
        }
    }
    [HarmonyPatch(typeof(KerbalEVA),"SurfaceContact")]
    internal static class EvaSurfaceContactPatch
    {
        private static bool Prefix(KerbalEVA __instance,ref bool __result)
        {
            if(!RingEva.Applies(__instance))return true;
            __result=RingEva.Grounded(__instance);return false;
        }
    }
    [HarmonyPatch(typeof(Vessel),nameof(Vessel.GetGroundLevelAngle))]
    internal static class RingSlopePatch
    {
        private static bool Prefix(Vessel __instance,ref float ___groundLevelAngle)
        {
            if(!StockIntegration.Applies(__instance))return true;
            var f=RingworldFlight.Instance;
            var hit=__instance.GetHitFromSurface();
            ___groundLevelAngle=hit.collider==null?0:Vector3.Angle(ConvertVector.Unity(f.Settings.Geometry.Up(f.Position(__instance))),hit.normal);
            return false;
        }
    }
    [HarmonyPatch(typeof(KerbalEVA),"CalculateGroundLevelAngle")]
    internal static class EvaSlopePatch
    {
        private static bool Prefix(KerbalEVA __instance,ref int ___slopeMovementDirection)
        {
            if(!RingEva.Applies(__instance))return true;
            __instance.vessel.GetGroundLevelAngle();
            var origin=__instance.transform.position-__instance.fUp*__instance.halfHeight;
            bool front=Physics.Raycast(origin,__instance.transform.forward,.25f,1<<15,QueryTriggerInteraction.Ignore);
            bool back=Physics.Raycast(origin,-__instance.transform.forward,.25f,1<<15,QueryTriggerInteraction.Ignore);
            ___slopeMovementDirection=back?-1:front?1:0;return false;
        }
    }
    [HarmonyPatch(typeof(KerbalEVA),"UpdateMovement")]
    internal static class EvaMovementPatch
    {
        private static bool Prefix(KerbalEVA __instance,ref float ___currentSpd,float ___lastTgtSpeed,float ___tgtSpeed,Vector3 ___tgtRpos,ref Vector3 ___cmdDir)
        {
            if(!RingEva.Applies(__instance))return true;
            if(!RingEva.Grounded(__instance))return false;
            float blend=Mathf.Clamp01((float)__instance.fsm.TimeAtCurrentState*3.3333333f);
            ___currentSpd=Mathf.Lerp(___lastTgtSpeed,___tgtSpeed,blend);
            if(___tgtRpos!=Vector3.zero)
                ___cmdDir=Vector3.Lerp(___cmdDir,__instance.CharacterFrameMode?___tgtRpos:__instance.transform.forward,blend);
            var normal=__instance.vessel.HeightFromSurfaceHit.normal;
            __instance.part.rb.velocity=Vector3.ProjectOnPlane(___cmdDir,normal)*___currentSpd;
            return false;
        }
    }
    [HarmonyPatch(typeof(KerbalEVA),"onRotatingFrameChanged")]
    internal static class EvaStockRotationPatch
    {
        private static bool Prefix(KerbalEVA __instance){return !RingEva.Applies(__instance);}
    }
    [HarmonyPatch(typeof(KerbalEVA),"IntegrateRagdollRigidbodyForces")]
    internal static class EvaRagdollPatch
    {
        private static bool Prefix(KerbalEVA __instance)
        {
            if(!RingEva.Applies(__instance))return true;
            if(__instance.vessel.packed||__instance.ragdollNodes==null)return false;
            var f=RingworldFlight.Instance;
            foreach(var node in __instance.ragdollNodes)
            {
                var rb=node.rb;if(rb==null||rb.isKinematic||rb==__instance.part.rb)continue;
                var p=ConvertVector.Core((Vector3d)rb.worldCenterOfMass-f.Center);
                var v=ConvertVector.Core((Vector3d)rb.velocity+Krakensbane.GetFrameVelocity());
                rb.AddForce(ConvertVector.Unity(f.Acceleration(p,v)),ForceMode.Acceleration);
            }
            return false;
        }
    }
}
