using System;
using HarmonyLib;
using KSP.UI.Screens.Flight;
using UnityEngine;

namespace NivenRingworld
{
    internal static class RingNavigation
    {
        internal static bool TryVelocity(Vessel vessel,out Vector3 velocity)
        {
            var f=RingworldFlight.Instance;
            velocity=ConvertVector.Unity(f.Velocity(vessel));
            if(FlightGlobals.speedDisplayMode!=FlightGlobals.SpeedDisplayModes.Target)return true;
            var target=vessel.targetObject;
            var other=target==null?null:target.GetVessel();
            if(!StockIntegration.Applies(other)){velocity=Vector3.zero;return false;}
            velocity-=ConvertVector.Unity(f.Velocity(other));return true;
        }
    }
    [HarmonyPatch(typeof(NavBall),"Update")]
    internal static class RingNavballPatch
    {
        private static readonly Action<NavBall,Quaternion> SetRelative=(Action<NavBall,Quaternion>)Delegate.CreateDelegate(typeof(Action<NavBall,Quaternion>),AccessTools.PropertySetter(typeof(NavBall),"relativeGymbal"));
        private static readonly Action<NavBall,Transform> Tint=(Action<NavBall,Transform>)Delegate.CreateDelegate(typeof(Action<NavBall,Transform>),AccessTools.Method(typeof(NavBall),"SetVectorAlphaTint"));
        private static void Postfix(NavBall __instance)
        {
            var v=FlightGlobals.ActiveVessel;if(!FlightGlobals.ready||!StockIntegration.Applies(v))return;
            var f=RingworldFlight.Instance;
            var frame=Quaternion.LookRotation(ConvertVector.Unity(f.Settings.Geometry.Axis),ConvertVector.Unity(f.Settings.Geometry.Up(f.Position(v))));
            var relative=__instance.attitudeGymbal*frame;
            SetRelative(__instance,relative);__instance.navBall.rotation=relative;
            __instance.headingText.text=KSPUtil.LocalizeNumber(Quaternion.Inverse(relative).eulerAngles.y,"000")+"°";
            Vector3 velocity;bool valid=RingNavigation.TryVelocity(v,out velocity);
            bool moving=valid&&velocity.magnitude>__instance.VectorVelocityThreshold;
            Marker(__instance,__instance.progradeVector,velocity.normalized,moving);
            Marker(__instance,__instance.retrogradeVector,-velocity.normalized,moving);
            // Osculating solar-orbit normal/radial cues do not describe ring travel.
            __instance.normalVector.gameObject.SetActive(false);
            __instance.antiNormalVector.gameObject.SetActive(false);
            __instance.radialInVector.gameObject.SetActive(false);
            __instance.radialOutVector.gameObject.SetActive(false);
        }
        private static void Marker(NavBall ball,Transform marker,Vector3 direction,bool moving)
        {
            marker.localPosition=ball.attitudeGymbal*(direction*ball.VectorUnitScale);
            marker.gameObject.SetActive(moving&&marker.localPosition.z>=ball.VectorUnitCutoff);
            Tint(ball,marker);
        }
    }
    [HarmonyPatch(typeof(SpeedDisplay),"LateUpdate")]
    internal static class RingSpeedDisplayPatch
    {
        private static bool wasRing;
        private static void Postfix(SpeedDisplay __instance)
        {
            var v=FlightGlobals.ActiveVessel;
            if(!FlightGlobals.ready||!StockIntegration.Applies(v))
            {
                if(wasRing){__instance.textTitle.text=FlightGlobals.speedDisplayMode.displayDescription();wasRing=false;}
                return;
            }
            wasRing=true;Vector3 velocity;bool valid=RingNavigation.TryVelocity(v,out velocity);
            __instance.textTitle.text=FlightGlobals.speedDisplayMode==FlightGlobals.SpeedDisplayModes.Target?"Ring target":"Ring surface";
            __instance.textSpeed.text=valid?KSPUtil.LocalizeNumber(Math.Round(velocity.magnitude*SpeedDisplay.speedMultiplier,1),SpeedDisplay.format)+SpeedDisplay.units:"—";
        }
    }
}
