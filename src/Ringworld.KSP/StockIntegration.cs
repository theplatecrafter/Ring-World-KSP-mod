using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;

namespace NivenRingworld
{
    internal static class StockIntegration
    {
        private static bool installed;
        private static bool rollbackFailed;
        private static readonly Dictionary<Type,string> failures=new Dictionary<Type,string>();
        internal static string OwnerFor(Type type){return "NivenRingworld.surface."+type.FullName;}
        private static bool Optional(Type type)
        {return type==typeof(RingResourceHarvester)||type==typeof(RingHarvesterResourceQuery)||type==typeof(RingCommunityFixesResourceCache)||type==typeof(RingIntakeEnvironment)||
            type==typeof(RingAirshipCompatibility)||type==typeof(RingAquaticCompatibility)||type==typeof(RingScanSatCompatibility)||type==typeof(RingGoldStrikeCompatibility)||type==typeof(RingGraviticHover)||type==typeof(RingSolverEngineCompatibility)||type==typeof(RingSolverFlightSystemCompatibility);}
        internal static bool ReadyForFlight {get{return installed&&!rollbackFailed&&(failures.Count==0||!failures.Keys.Any(t=>!Optional(t)));}}
        internal static string Warning
        {
            get{
                var messages=new List<string>();
                if(failures.Count>0)messages.Add(string.Join(", ",failures.Keys.Select(t=>t.Name).ToArray())+" could not be installed. See KSP.log.");
                if(!RingAdapterOptions.Enabled("stockHarvesters"))messages.Add("Ring harvesting disabled by compatibility configuration.");
                if(!RingAdapterOptions.Enabled("stockModuleEnvironment"))messages.Add("Ring intake/engine environment reads disabled by compatibility configuration.");
                if(messages.Count==0)return "";
                return "Ringworld compatibility: "+string.Join(" ",messages.ToArray())+" "+(ReadyForFlight?"Affected features are unavailable.":"Ring flight is blocked to protect saved residents.");
            }
        }
        internal static void InstallClass(Type type,Action<Harmony> apply)
        {
            var harmony=new Harmony(OwnerFor(type));
            try{apply(harmony);}
            catch(Exception e)
            {
                // A multi-target class may have installed some patches before
                // failing. Roll back only this class, never another mod's owner.
                failures[type]=e.ToString();
                try{harmony.UnpatchAll(harmony.Id);}catch(Exception rollback){rollbackFailed=true;Debug.LogError("[NivenRingworld] Patch rollback failed; ring flight disabled.\n"+rollback);}
                Debug.LogError("[NivenRingworld] Compatibility patch failed: "+type.FullName+"; owner="+harmony.Id+". Other features continue initializing.\n"+e);
            }
        }
        internal static void Install()
        {
            if(installed)return;
            // Install once per session, including failed classes: a scene change
            // must not append duplicates to already installed transpilers.
            installed=true;
            foreach(var type in typeof(StockIntegration).Assembly.GetTypes().Where(t=>t.IsDefined(typeof(HarmonyPatch),false)).OrderBy(t=>t.FullName))
                InstallClass(type,h=>h.CreateClassProcessor(type).Patch());
            Debug.Log("[NivenRingworld] Scoped stock surface compatibility patches installed; failures="+failures.Count+"; flightReady="+ReadyForFlight);
            // Capture the actual competing owners instead of guessing from a
            // mod list. No save contents or filesystem paths are recorded here.
            foreach(var method in new[]{AccessTools.Method(typeof(ModuleResourceHarvester),"PrepareRecipe"),AccessTools.Method(typeof(ModuleResourceIntake),"FixedUpdate"),AccessTools.Method(typeof(PartBuoyancy),"FixedUpdate"),AccessTools.Method(typeof(VesselPrecalculate),"CalculatePhysicsStats")})
            {
                var info=Harmony.GetPatchInfo(method);if(info!=null)Debug.Log("[NivenRingworld] Patch owners "+method.DeclaringType.Name+"."+method.Name+": "+string.Join(", ",info.Owners.OrderBy(o=>o).ToArray()));
            }
            if(!string.IsNullOrEmpty(Warning))CompatibilityNotice.Show();
        }
        internal static bool Applies(Vessel v)
        {
            var f=RingworldFlight.Instance;
            return ReadyForFlight&&f!=null&&f.Owns(v);
        }
#if RINGWORLD_SMOKE_TEST
        internal static void ClearTestFailure(Type type){failures.Remove(type);}
#endif
    }
    internal sealed class CompatibilityNotice : MonoBehaviour
    {
        private static CompatibilityNotice instance;
        private bool dismissed,uiVisible=true;
        internal static void Show()
        {if(instance!=null)return;var go=new GameObject("Ringworld compatibility notice");UnityEngine.Object.DontDestroyOnLoad(go);instance=go.AddComponent<CompatibilityNotice>();}
        private void Awake(){GameEvents.onHideUI.Add(Hide);GameEvents.onShowUI.Add(ShowUi);}
        private void Hide(){uiVisible=false;}
        private void ShowUi(){uiVisible=true;}
        private void OnDestroy(){GameEvents.onHideUI.Remove(Hide);GameEvents.onShowUI.Remove(ShowUi);if(instance==this)instance=null;}
        private void OnGUI()
        {
            if(dismissed||!uiVisible||string.IsNullOrEmpty(StockIntegration.Warning)||!HighLogic.LoadedSceneIsGame)return;
            GUILayout.BeginArea(new Rect(30,30,520,130),GUI.skin.box);
            GUILayout.Label(StockIntegration.Warning);
            if(GUILayout.Button("Dismiss notice (details remain in the Ringworld panel and KSP.log)"))dismissed=true;
            GUILayout.EndArea();
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
    // UpdatePosVel writes these fields again after UpdateCaches in stock
    // FixedUpdate. Publish at the final writer so all ordinary consumers see
    // local airspeed, not just modules with a dedicated read adapter.
    [HarmonyPatch(typeof(Vessel),"UpdatePosVel")]
    internal static class RingSurfaceVelocityPatch
    {
        private static void Postfix(Vessel __instance){RingSurfaceCaches.Publish(__instance);}
    }
    internal static class RingSurfaceCaches
    {
        internal static void Publish(Vessel v)
        {
            long profile=RingPerformance.Begin();
            try{PublishState(v);}finally{RingPerformance.End(RingPerformance.Section.SurfaceCaches,profile);}
        }
        private static void PublishState(Vessel v)
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
