using System;
using System.Collections.Generic;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal sealed class RingSurfaceWarp
    {
        private sealed class Anchor {internal DVec Position;internal Quaternion Rotation;}
        private readonly Dictionary<Vessel,RingRestPose> restPoses=new Dictionary<Vessel,RingRestPose>();
        private readonly Dictionary<Vessel,Anchor> anchors=new Dictionary<Vessel,Anchor>();
        internal double Rate
        {
            get{return TimeWarp.CurrentRate;}
            set{int index=0;for(int i=0;i<TimeWarp.fetch.warpRates.Length;i++)if(TimeWarp.fetch.warpRates[i]<=value)index=i;TimeWarp.SetRate(index,true);}
        }
        internal string Status="Use stock time warp when resting on the ring.";
        // Read the live table: BetterTimeWarp can replace it without changing
        // the number of stock buttons. Limits are rates, never fixed indices.
        internal static int AllowedIndex(float[] rates,int requested,double limit)
        {
            if(rates==null||rates.Length==0)return 0;
            int index=Math.Max(0,Math.Min(rates.Length-1,requested));
            while(index>0&&(!RingParameters.Finite(rates[index])||rates[index]<1||rates[index]>limit))index--;
            return index;
        }
        internal bool Anchored(Vessel v){return v!=null&&anchors.ContainsKey(v);}
        internal bool CanAdvance(RingworldFlight f,bool allowPaused=false)
        {
            if((Time.timeScale==0&&!allowPaused)||(!f.Ready&&anchors.Count==0)){Status="Warp waiting for an unpacked, ready expedition.";return false;}
            bool found=false;
            foreach(var v in FlightGlobals.VesselsLoaded)
            {
                if(v==null)continue;
                if(Anchored(v)){found=true;continue;}
                if(v.packed)continue;
                found=true;if(!CanRest(f,v))return false;
            }
            if(found)Status="Ready for stock ring-surface warp.";return found;
        }
        // Saving one resident does not advance physics for its airborne neighbours.
        internal bool CanSave(RingworldFlight f,Vessel v)
        {
            return v!=null&&f.Owns(v)&&(Anchored(v)||(f.Ready&&!v.packed&&CanRest(f,v)));
        }
        private bool CanRest(RingworldFlight f,Vessel v)
        {
            if(!f.Owns(v)){Status="Warp blocked by a nearby craft outside the ring frame.";return false;}
            if(v.parts==null||v.parts.Count==0){Status="Warp waiting for vessel initialization.";return false;}
            var c=f.Settings.Geometry.Coordinates(f.Position(v));var t=f.Settings.Terrain.Sample(c.Along,c.Across);
            bool contact=false;foreach(var part in v.parts)if(part!=null&&(part.GroundContact||part.PermanentGroundContact))contact=true;
            double speed=f.Velocity(v).Length;
            if(!RingParameters.Finite(speed)){Status="Warp blocked: invalid vessel velocity.";return false;}
            if(!contact){Status="Warp blocked: no solid ground contact ("+v.vesselName+").";return false;}
            if(t.Wet){Status="Warp blocked: vessel is on water.";return false;}
            if(speed>.25){Status="Warp blocked: still moving at "+speed.ToString("F2")+" m/s (limit 0.25).";return false;}
            if(v.ctrlState.mainThrottle>.001f){Status="Warp blocked: throttle is not zero.";return false;}
            bool chatter=false;
            foreach(var part in v.parts)if(part!=null&&part.rb!=null)
            {
                double angular=part.rb.angularVelocity.magnitude;
                if(!RingParameters.Finite(angular)||angular>.12||(part==v.rootPart&&angular>.05))
                {Status="Warp blocked: "+(part.partInfo==null?part.name:part.partInfo.title)+" is still rotating.";return false;}
                if(angular>.05)chatter=true;
            }
            if(chatter&&!ObserveRest(f,v))
            {Status="Warp waiting for one second of bounded joint motion (3 cm / 0.5 degrees).";return false;}
            return true;
        }
        private bool ObserveRest(RingworldFlight f,Vessel v)
        {
            RingRestPose pose;if(!restPoses.TryGetValue(v,out pose)){pose=new RingRestPose();restPoses[v]=pose;}
            return pose.Observe(f,v);
        }
        internal bool Prepare(RingworldFlight f)
        {
            if(!CanAdvance(f))return false;
            f.Capture();
            foreach(var v in FlightGlobals.VesselsLoaded)
                if(f.Owns(v)&&!Anchored(v))anchors[v]=new Anchor{Position=ConvertVector.Core((Vector3d)v.transform.position-f.Center),Rotation=v.transform.rotation};
            return true;
        }
        internal void Hold(Vessel v)
        {
            Anchor a;if(!anchors.TryGetValue(v,out a))return;
            var f=RingworldFlight.Instance;if(f==null)return;
            var pos=ConvertVector.Ksp(a.Position);
            RingResidence.UpdateBookkeeping(v,f.Settings,f.Star,a.Position,new DVec(),f.FrameEpoch);
            v.orbitDriver.pos=pos;v.orbitDriver.vel=Vector3d.zero;
            v.SetPosition(f.Center+pos,true);v.SetRotation(a.Rotation,false);
        }
        internal void Release(Vessel v)
        {
            if(!Anchored(v)||v.packed)return;
            Hold(v);v.SetWorldVelocity(Vector3d.zero);v.IgnoreGForces(2);v.IgnoreSpeed(2);anchors.Remove(v);
        }
        internal void Update(RingworldFlight f)
        {
            if(f.Ready)
                foreach(var v in FlightGlobals.VesselsLoaded)
                    if(v!=null&&!v.packed&&f.Owns(v)&&v.rootPart!=null)ObserveRest(f,v);
            var stale=new List<Vessel>();foreach(var v in restPoses.Keys)if(v==null||!f.Owns(v))stale.Add(v);
            foreach(var v in stale)restPoses.Remove(v);
            if(TimeWarp.CurrentRateIndex>0||TimeWarp.CurrentRate>1.0001f)
            {
                int allowed=AllowedIndex(TimeWarp.fetch.warpRates,TimeWarp.CurrentRateIndex,f.Settings.SurfaceWarpLimit);
                if(TimeWarp.WarpMode!=TimeWarp.Modes.HIGH){TimeWarp.SetRate(0,true);Status="Physics warp is unavailable in the ring frame.";return;}
                if(allowed!=TimeWarp.CurrentRateIndex){TimeWarp.SetRate(allowed,true);return;}
                if(anchors.Count==0&&!Prepare(f)){TimeWarp.SetRate(0,true);return;}
                foreach(var v in new List<Vessel>(anchors.Keys)){if(v==null){anchors.Remove(v);continue;}if(!v.packed)v.GoOnRails();Hold(v);}
                Status="Stock rails warp: ring contact anchored; universal time advances normally.";
            }
            else if(anchors.Count>0)
            {
                foreach(var v in new List<Vessel>(anchors.Keys)){if(v==null){anchors.Remove(v);continue;}Hold(v);v.GoOffRails();Release(v);}
            }
        }
        internal void Draw(RingworldFlight f){if(TimeWarp.CurrentRateIndex==0)CanAdvance(f);GUILayout.Label(Status);GUILayout.Label("Use the stock top-left warp controls or comma/period. Ground contact is required for ring-surface rails warp.");}
    }
    [HarmonyPatch(typeof(TimeWarp),"setRate")]
    internal static class RingStockWarpRate
    {
        private static bool Prefix(TimeWarp __instance,ref int rateIdx,ref bool __result)
        {
            var f=RingworldFlight.Instance;if(f==null||!f.Active)return true;
            rateIdx=Math.Max(0,Math.Min(__instance.warpRates.Length-1,rateIdx));if(rateIdx==0)return true;
            if(TimeWarp.WarpMode!=TimeWarp.Modes.HIGH){f.surfaceWarp.Status="Use standard rails warp; physics warp is unavailable in the ring frame.";__result=false;return false;}
            if(!f.surfaceWarp.Prepare(f)){__result=false;return false;}
            rateIdx=RingSurfaceWarp.AllowedIndex(__instance.warpRates,rateIdx,f.Settings.SurfaceWarpLimit);
            return true;
        }
    }
    [HarmonyPatch(typeof(TimeWarp),"getMaxOnRailsRateIdx")]
    internal static class RingStockWarpLimit
    {
        private static bool Prefix(int tgtRateIdx,ref ClearToSaveStatus reason,ref int __result)
        {
            var f=RingworldFlight.Instance;if(f==null||!f.Active||!f.surfaceWarp.CanAdvance(f))return true;
            reason=ClearToSaveStatus.CLEAR;__result=RingSurfaceWarp.AllowedIndex(TimeWarp.fetch.warpRates,tgtRateIdx,f.Settings.SurfaceWarpLimit);return false;
        }
    }
    [HarmonyPatch(typeof(OrbitDriver),"UpdateOrbit")]
    internal static class RingAnchoredOrbit
    {
        private static bool Prefix(OrbitDriver __instance)
        {var f=RingworldFlight.Instance;if(f==null||!f.surfaceWarp.Anchored(__instance.vessel))return true;f.surfaceWarp.Hold(__instance.vessel);return false;}
    }
    [HarmonyPatch(typeof(VesselPrecalculate),"MainPhysics")]
    internal static class RingAnchoredPhysics
    {
        private static bool Prefix(VesselPrecalculate __instance)
        {var f=RingworldFlight.Instance;var v=__instance.Vessel;if(f==null||!v.packed||!f.surfaceWarp.Anchored(v))return true;f.surfaceWarp.Hold(v);return false;}
    }
    [HarmonyPatch(typeof(VesselPrecalculate),"Update")]
    internal static class RingAnchoredPresentation
    {
        private static bool Prefix(VesselPrecalculate __instance)
        {var f=RingworldFlight.Instance;var v=__instance.Vessel;if(f==null||!v.packed||!f.surfaceWarp.Anchored(v))return true;f.surfaceWarp.Hold(v);return false;}
    }
    [HarmonyPatch(typeof(Vessel),"GoOffRails")]
    internal static class RingUnpackAnchor
    {
        private static bool Prefix(Vessel __instance)
        {var f=RingworldFlight.Instance;return f==null||!f.surfaceWarp.Anchored(__instance)||(TimeWarp.CurrentRateIndex==0&&TimeWarp.CurrentRate<=1.0001f);}
        private static void Postfix(Vessel __instance){var f=RingworldFlight.Instance;if(f!=null)f.surfaceWarp.Release(__instance);}
    }
}
