using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // Map coordinates are inertial even when local flight physics uses a frozen,
    // co-rotating chart. Do not change physical transforms to position map icons.
    internal static class RingMapFrame
    {
        internal static bool Active {get{return MapView.MapIsEnabled||HighLogic.LoadedScene==GameScenes.TRACKSTATION;}}
        internal static Vector3d Center(Settings s){return Active?s.InertialCenter:s.Center;}
        internal static bool TryPosition(Vessel v,out Vector3d position)
        {
            position=Vector3d.zero;VesselRecord record;
            if(v==null||v.state==Vessel.State.DEAD||!RingResidence.Saved(v,out record))return false;
            double now=Planetarium.GetUniversalTime();var f=RingworldFlight.Instance;
            if(f!=null&&f.Owns(v)&&!v.packed)
            {
                position=f.Settings.InertialCenter+ConvertVector.Ksp(f.Settings.Geometry.ToInertialPosition(f.Position(v),now-f.FrameEpoch));
                return true;
            }
            if(!record.Landed)return false;
            var s=RingworldScenario.Instance.RingSettings(record.RingId);
            if(s==null||s.Body==null)return false;
            position=s.InertialCenter+ConvertVector.Ksp(s.Geometry.ToInertialPosition(record.Position,now-record.Epoch));
            return true;
        }
    }
    [HarmonyPatch(typeof(ScaledMovement),"OnLateUpdate")]
    internal static class RingMapMarker
    {
        private static void Postfix(ScaledMovement __instance)
        {
            Vector3d position;
            if(RingMapFrame.Active&&RingMapFrame.TryPosition(__instance.vessel,out position))
                __instance.transform.position=(Vector3)ScaledSpace.LocalToScaledSpace(position);
        }
    }
}
