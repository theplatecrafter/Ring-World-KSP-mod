using System;
using HarmonyLib;
namespace NivenRingworld
{
    [HarmonyPatch(typeof(SunFlare),"LateUpdate")]
    internal static class RingPanelSunOcclusion
    {
        // Stock flare occlusion only considers celestial-body spheres. The ring's
        // shadow squares are meshes, so apply their existing daylight mask too.
        private static void Postfix(SunFlare __instance)
        {
            float daylight;
            if(__instance.sunFlare!=null&&TryDaylight(__instance.sun,out daylight))
            {
                __instance.sunFlare.brightness*=daylight;__instance.sunFlare.enabled=daylight>0;
                var f=RingworldFlight.Instance;var camera=FlightCamera.fetch.mainCamera;
                var p=ConvertVector.Core((Vector3d)camera.transform.position-f.Center);
                __instance.sunDirection=-ConvertVector.Ksp(RingLighting.Direction(f.Settings,p,Planetarium.GetUniversalTime()));
                __instance.transform.forward=(UnityEngine.Vector3)__instance.sunDirection;
            }
        }
        internal static bool TryDaylight(CelestialBody star,out float daylight)
        {
            daylight=1;
            var f=RingworldFlight.Instance;
            if(f==null||!f.FrameInUse||MapView.MapIsEnabled||star!=RingLighting.Illuminator(f.Settings)||FlightCamera.fetch==null)return false;
            var camera=FlightCamera.fetch.mainCamera;if(camera==null)return false;
            var p=ConvertVector.Core((Vector3d)camera.transform.position-f.Center);var g=f.Settings.Geometry;
            var c=g.Coordinates(p);
            // LateUpdate recomputes stock brightness each frame, so no saved global
            // state is left behind on daybreak, a map switch or scene teardown.
            daylight=(float)g.Daylight(c.Along,Planetarium.GetUniversalTime(),c.Across,c.Altitude);return true;
        }
    }
}
