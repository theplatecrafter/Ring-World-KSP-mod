using HarmonyLib;
using Ringworld.Core;
using UnityEngine;

namespace NivenRingworld
{
    internal static class StockIntegration
    {
        private static bool installed;
        internal static void Install()
        {
            if(installed)return;
            new Harmony("NivenRingworld.surface").PatchAll(typeof(StockIntegration).Assembly);installed=true;
            Debug.Log("[NivenRingworld] Scoped stock surface compatibility patches installed.");
        }
        internal static bool Applies(Vessel v)
        {
            var f=RingworldFlight.Instance;
            return f!=null&&f.Owns(v);
        }
    }
    // The stock method dereferences the body's PQS on contact; the Sun has no PQS.
    // Report real contact without invoking spherical PQS anchoring.
    [HarmonyPatch(typeof(Vessel),nameof(Vessel.checkLanded))]
    internal static class RingLandingPatch
    {
        private static bool Prefix(Vessel __instance,ref bool __result)
        {
            var f=RingworldFlight.Instance;
            if(f==null)return true;
            // Decoupled debris can check contact during Initialize, before the
            // normal frame-adoption update. Avoid entering the Sun's null-PQS path.
            if(!f.IsParticipant(__instance)&&__instance.parts!=null&&__instance.parts.Count>0&&f.FrameInUse)f.AdoptParticipant(__instance);
            if(!f.IsParticipant(__instance))return true;
            __result=RingResidence.UpdateContact(f,__instance);return false;
        }
    }
    [HarmonyPatch(typeof(VesselPrecalculate),"CalculatePhysicsStats")]
    internal static class RingOrientationPatch
    {
        private static void Postfix(VesselPrecalculate __instance)
        {
            RingSurfaceCaches.Publish(__instance.Vessel);
        }
    }
    // Vessel.UpdateCaches runs after precalculation and overwrites the surface
    // fields from the host orbit. Publish at that boundary as well, preserving
    // the actual orbital caches used by map/rails flight.
    [HarmonyPatch(typeof(Vessel),"UpdateCaches")]
    internal static class RingSurfaceCachePatch
    {
        private static void Postfix(Vessel __instance){RingSurfaceCaches.Publish(__instance);}
    }
    internal static class RingSurfaceCaches
    {
        internal static void Publish(Vessel v)
        {
            if(!StockIntegration.Applies(v)||!v.loaded||v.packed||v.parts==null||v.parts.Count==0)return;
            var f=RingworldFlight.Instance;var up=f.Settings.Geometry.Up(f.Position(v));
            v.upAxis=ConvertVector.Ksp(up);v.north=ConvertVector.Ksp(f.Settings.Geometry.Axis);
            v.east=ConvertVector.Ksp(DVec.Cross(f.Settings.Geometry.Axis,up));
            // These caches describe motion relative to the current surface, not
            // orbital motion. Publish them for all stock/third-party consumers;
            // keep obt_velocity, orbit, host body and orbital coordinates intact.
            var surfaceVelocity=f.Velocity(v);double vertical=DVec.Dot(surfaceVelocity,up);
            v.srf_velocity=ConvertVector.Ksp(surfaceVelocity);v.srfSpeed=surfaceVelocity.Length;
            v.srf_vel_direction=v.srfSpeed>1e-9?v.srf_velocity/v.srfSpeed:Vector3d.zero;
            v.verticalSpeed=vertical;v.horizontalSrfSpeed=(surfaceVelocity-up*vertical).Length;
        }
    }
    [HarmonyPatch(typeof(FlightCamera),nameof(FlightCamera.GetCameraFoR))]
    internal static class RingCameraPatch
    {
        private static bool Prefix(FlightCamera __instance,FoRModes mode,ref Quaternion __result)
        {
            var v=FlightGlobals.ActiveVessel;if(!StockIntegration.Applies(v))return true;
            if(mode!=FoRModes.SRF_NORTH&&mode!=FoRModes.SRF_HDG&&mode!=FoRModes.SRF_VEL)return true;
            __instance.FoRMode=mode;
            var f=RingworldFlight.Instance;
            __result=Quaternion.LookRotation(ConvertVector.Unity(f.Settings.Geometry.Axis),ConvertVector.Unity(f.Settings.Geometry.Up(f.Position(v))));return false;
        }
    }
    [HarmonyPatch(typeof(FlightCamera),"GetAutoModeForVessel")]
    internal static class RingAutoCameraPatch
    {
        private static bool Prefix(Vessel v,ref FlightCamera.Modes __result)
        {
            if(!StockIntegration.Applies(v))return true;
            __result=FlightCamera.Modes.FREE;return false;
        }
    }
    [HarmonyPatch(typeof(FlightCamera),"GetChaseFoR")]
    internal static class RingEvaChasePatch
    {
        private static bool Prefix(FlightCamera __instance,Vessel v,ref Quaternion __result)
        {
            if(v==null||!v.isEVA||!StockIntegration.Applies(v))return true;
            __result=__instance.GetCameraFoR(FoRModes.SRF_NORTH);return false;
        }
    }
    // Stock altitude is solar altitude, so Krakensbane would remain active at the ring floor.
    // Its velocity frame moves static colliders every tick and can inject contact energy.
    // Retain floating-origin shifts, but keep ordinary local Rigidbody velocities at the floor.
    [HarmonyPatch(typeof(Krakensbane),nameof(Krakensbane.SafeToEngage))]
    internal static class RingVelocityFramePatch
    {
        private static bool Prefix(ref bool __0,ref bool __result)
        {
            var f=RingworldFlight.Instance;
            if(f==null||(!f.FrameInUse&&!f.IsParticipant(FlightGlobals.ActiveVessel)))return true;
            __0=true;__result=false;return false;
        }
    }
    [HarmonyPatch(typeof(KSP.UI.Screens.Flight.AltitudeTumbler),"LateUpdate")]
    internal static class RingAltimeterPatch
    {
        private static bool Prefix(KSP.UI.Screens.Flight.AltitudeTumbler __instance,ref float ___lastUpdateTime)
        {
            var v=FlightGlobals.ActiveVessel;if(!StockIntegration.Applies(v))return true;
            if(__instance.modeTumbler!=null)__instance.modeTumbler.UpdateDelta(Time.realtimeSinceStartup-___lastUpdateTime,__instance.modeTumblerSharpness);
            ___lastUpdateTime=Time.realtimeSinceStartup;
            if(__instance.tumbler!=null)__instance.tumbler.SetValue(RingworldFlight.Instance.SurfaceClearance(v));
            return false;
        }
    }

}
