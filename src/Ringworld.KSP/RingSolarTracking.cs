using System;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    [HarmonyPatch(typeof(FlightIntegrator),"CalculateSunBodyFlux")]
    internal static class RingSolarFlux
    {
        private static void Postfix(FlightIntegrator __instance)
        {
            var v=__instance.GetComponent<Vessel>();if(StockIntegration.Applies(v))Apply(__instance,v,1);
        }
        internal static void Apply(FlightIntegrator fi,Vessel v,double transmission)
        {
            var f=RingworldFlight.Instance;var p=f.Position(v);double time=Planetarium.GetUniversalTime();
            double distance=RingLighting.LightDistance(f.Settings,p,time);
            fi.sunVector=ConvertVector.Unity(RingLighting.Direction(f.Settings,p,time));
            fi.sunDot=Vector3d.Dot(fi.sunVector,v.upAxis);
            fi.solarFluxMultiplier=RingLighting.Visibility(f.Settings,p,time)*transmission;
            v.solarFlux=fi.solarFlux=PhysicsGlobals.SolarLuminosity/(4*Math.PI*Math.Max(1,distance*distance))*fi.solarFluxMultiplier;
        }
    }
    // A private target changes only this module's tracking calculation. Never move
    // the real celestial body's transform to satisfy a stock part query.
    [HarmonyPatch(typeof(ModuleDeployablePart),"CalculateTracking")]
    internal static class RingSolarTracking
    {
        private static GameObject target;
        internal static void Clear(){if(target!=null)UnityEngine.Object.Destroy(target);target=null;}
        private static void Prefix(ModuleDeployablePart __instance,out Transform __state)
        {
            __state=null;var panel=__instance as ModuleDeployableSolarPanel;
            if(panel==null||!StockIntegration.Applies(panel.vessel)||panel.trackingTransformLocal==null)return;
            var f=RingworldFlight.Instance;if(panel.trackingBody!=RingLighting.Illuminator(f.Settings))return;
            if(target==null)target=new GameObject("Ringworld solar tracking target"){hideFlags=HideFlags.HideAndDontSave};
            var local=ConvertVector.Core((Vector3d)panel.part.transform.position-f.Center);
            target.transform.position=panel.part.transform.position+ConvertVector.Unity(RingLighting.Direction(f.Settings,local,Planetarium.GetUniversalTime()))*1000000;
            __state=panel.trackingTransformLocal;panel.trackingTransformLocal=target.transform;
        }
        private static Exception Finalizer(ModuleDeployablePart __instance,Transform __state,Exception __exception)
        {if(__state!=null)__instance.trackingTransformLocal=__state;return __exception;}
    }
    [HarmonyPatch(typeof(ModuleDeployableSolarPanel),"CalculateTrackingLOS")]
    internal static class RingSolarLineOfSight
    {
        private static void Prefix(ModuleDeployableSolarPanel __instance,ref int ___planetLayerMask,out int? __state)
        {
            __state=null;if(!StockIntegration.Applies(__instance.vessel))return;
            // Scaled body colliders live in the inertial chart outside render calls.
            // Keep stock vessel/terrain raycasts; evaluate celestial shadows below.
            __state=___planetLayerMask;___planetLayerMask=0;
        }
        private static void Postfix(ModuleDeployableSolarPanel __instance,int? __state,ref bool __result,ref string blocker)
        {
            if(!__state.HasValue||!__result)return;var f=RingworldFlight.Instance;
            var p=ConvertVector.Core((Vector3d)__instance.part.transform.position-f.Center);
            if(RingLighting.Visibility(f.Settings,p,Planetarium.GetUniversalTime())<=0){__result=false;blocker="Ringworld eclipse";}
        }
        private static Exception Finalizer(ref int ___planetLayerMask,int? __state,Exception __exception)
        {if(__state.HasValue)___planetLayerMask=__state.Value;return __exception;}
    }
}
