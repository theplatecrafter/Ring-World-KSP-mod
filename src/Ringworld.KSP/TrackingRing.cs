using System;
using System.Collections.Generic;
using Ringworld.Core;
using HarmonyLib;
using UnityEngine;

namespace NivenRingworld
{
    [KSPAddon(KSPAddon.Startup.TrackingStation,false)]
    public sealed class TrackingRing : MonoBehaviour
    {
        internal static Settings Settings;
        internal static CelestialBody Star;
        internal static RingTrajectory Trajectory;
        private bool initialized;
        private float nextCheck;
        public void Start()
        {
            StockIntegration.Install();
            gameObject.AddComponent<RingRenderRegistry>();
            Settings=Settings.Load();Star=FlightGlobals.Bodies.Find(b=>b.name=="Sun");
            Trajectory=gameObject.AddComponent<RingTrajectory>();
        }
        public void Update()
        {
            var scenario=RingworldScenario.Instance;
            if(scenario==null||Star==null)return;
            if(!initialized)
            {
                Settings.Apply(scenario.GetOptions());
                // Old saves could leave an airborne rotating snapshot behind while
                // the stock orbit kept advancing. Keep the current orbit, not that
                // stale snapshot. New airborne residents are blocked by ClearToSave.
                var remove=new List<string>();
                foreach(var pair in scenario.Vessels)if(!pair.Value.Landed)remove.Add(pair.Key);
                foreach(var id in remove)scenario.Vessels.Remove(id);
                initialized=true;
            }
            // Tracking rails may advance a stock osculating orbit independently of
            // OrbitDriver.UpdateOrbit. Keep surface residents at their saved material point.
            foreach(var resident in FlightGlobals.Vessels)
            {VesselRecord record;if(resident.packed&&RingResidence.Saved(resident,out record)&&record.Landed)RingResidence.HoldSaved(resident,record);}
            var selected=PlanetariumCamera.fetch!=null&&PlanetariumCamera.fetch.target!=null?PlanetariumCamera.fetch.target.vessel:null;
            string nearest=RingSelection.Nearest(selected);if(nearest!=null){Settings=scenario.RingSettings(nearest);Star=Settings.Body;}
            if(Time.realtimeSinceStartup<nextCheck)return;
            nextCheck=Time.realtimeSinceStartup+.1f;
            double now=Planetarium.GetUniversalTime();
            double horizon=Math.Max(10,Math.Min(72000,TimeWarp.CurrentRate*2));
            foreach(var v in FlightGlobals.Vessels)
            {
                if(!GuardsEncounter(v))continue;
                double eta=VesselEncounter(v,now,horizon);
                if(double.IsInfinity(eta))continue;
                if(TimeWarp.CurrentRateIndex==0)continue;
                TimeWarp.SetRate(0,true);
                ScreenMessages.PostScreenMessage("Ringworld encounter: time warp stopped for "+v.vesselName+". Select Fly to simulate atmospheric entry.",3,ScreenMessageStyle.UPPER_CENTER);
                // Scene changes belong to the player's Fly action, never an encounter.
                break;
            }
        }
        internal static bool GuardsEncounter(Vessel v)
        {
            // Uncontrolled crash fragments must not lock the entire tracking station.
            return v!=null&&v.vesselType!=VesselType.Debris&&!v.Landed&&!v.Splashed&&v.orbit!=null;
        }
        private static double VesselEncounter(Vessel vessel,double now,double horizon)
        {
            double soonest=double.PositiveInfinity;var scenario=RingworldScenario.Instance;if(scenario==null)return soonest;
            foreach(var node in scenario.Rings)
            {
                var s=scenario.RingSettings(node.GetValue("ringId")??"primary");if(s.Body==null)continue;
                var orbit=RingTrajectory.SolarPatch(vessel,s.Body);if(orbit==null)continue;
                double offset=Math.Max(0,orbit.StartUT-now);if(offset>horizon)continue;
                soonest=Math.Min(soonest,offset+Encounter(orbit,s.Geometry,now+offset,horizon-offset,default(DVec),t=>s.AnchorAt(t).Position,s.Body));
            }
            return soonest;
        }
        internal static double Encounter(Orbit orbit,RingGeometry geometry,double now,double horizon,DVec centerOffset=default(DVec),Func<double,DVec> centerAt=null,CelestialBody reference=null)
        {
            var previous=RingAnchorEphemeris.OrbitRelative(orbit,reference??orbit.referenceBody,now).Position-(centerAt==null?centerOffset:centerAt(now));
            if(geometry.InArrivalRegion(previous,false))return 0;
            for(double elapsed=0;elapsed<horizon;)
            {
                double dt=Math.Min(120,horizon-elapsed);
                var next=RingAnchorEphemeris.OrbitRelative(orbit,reference??orbit.referenceBody,now+elapsed+dt).Position-(centerAt==null?centerOffset:centerAt(now+elapsed+dt));
                // Chord intersection catches a thin ribbon crossed between samples.
                double entry=geometry.TimeToArrival(previous,(next-previous)/dt,dt);
                if(!double.IsInfinity(entry))return elapsed+entry;
                previous=next;elapsed+=dt;
            }
            return double.PositiveInfinity;
        }
        internal static bool WarpSafe(float rate)
        {
            if(Settings==null||Star==null)return false;
            double now=Planetarium.GetUniversalTime(),horizon=Math.Max(10,Math.Min(72000,rate*2));
            foreach(var v in FlightGlobals.Vessels)
                if(GuardsEncounter(v)&&
                   !double.IsInfinity(VesselEncounter(v,now,horizon)))return false;
            return true;
        }
        public void OnDestroy(){Settings=null;Star=null;Trajectory=null;}
    }
    [HarmonyPatch(typeof(TimeWarp),"setRate")]
    internal static class RingTrackingWarpGuard
    {
        private static bool Prefix(TimeWarp __instance,int rateIdx,ref bool __result)
        {
            if(HighLogic.LoadedScene!=GameScenes.TRACKSTATION||rateIdx<=0)return true;
            int index=Math.Min(rateIdx,__instance.warpRates.Length-1);
            if(TrackingRing.WarpSafe(__instance.warpRates[index]))return true;
            ScreenMessages.PostScreenMessage("Ringworld encounter ahead: use a lower warp rate or fly the approaching vessel.",3,ScreenMessageStyle.UPPER_CENTER);
            __result=false;return false;
        }
    }
}
