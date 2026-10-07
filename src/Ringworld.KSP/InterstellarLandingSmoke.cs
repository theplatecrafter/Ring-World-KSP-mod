#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class InterstellarLandingSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            var v=FlightGlobals.ActiveVessel;
            var options=f.Settings.Save();RingQualityPresets.Apply(options,6);
            options.SetValue("seed",1542493278,true);options.SetValue("radius",150000000000.0,true);
            options.SetValue("width",1605000000.0,true);options.SetValue("wallHeight",1600000.0,true);
            options.SetValue("fullRingAtmosphere",true,true);options.SetValue("atmosphereBackend",0,true);
            f.ApplyOptions(options,true);
            if(f.Star==null||f.Star.name!="NivenRingworldHost"||Math.Abs(f.Star.orbit.semiMajorAxis-9460730472580800.0)>100)
            {fail("Full-size one-light-year star fixture not loaded");yield break;}
            if(RingAnchorEphemeris.OrbitReady(new Orbit())){fail("Uninitialized orbit accepted");yield break;}
            Debug.Log("[RingworldSmoke] FULLSIZE plane parts="+v.parts.Count+" star="+f.Star.name+" radius="+f.Settings.Geometry.P.Radius+" starDistance="+f.Star.orbit.semiMajorAxis);
            v.ActionGroups.SetGroup(KSPActionGroup.Gear,true);
            foreach(var d in v.FindPartModulesImplementing<ModuleWheels.ModuleWheelDeployment>())AccessTools.Method(typeof(ModuleWheels.ModuleWheelDeployment),"ToggleDeployment").Invoke(d,new object[]{true});
            yield return new WaitForSecondsRealtime(4);
            CheatOptions.NoCrashDamage=false;CheatOptions.UnbreakableJoints=false;
            for(int attempt=0;attempt<2;attempt++)
            {
                f.arrivalHeight=attempt==0?12:60;f.Visit();
                float deadline=Time.realtimeSinceStartup+90;
                while(!f.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
                if(!f.Ready){fail("Full-size plane transfer did not finish");yield break;}
                var arrival=f.Settings.Geometry.Coordinates(f.Position(v));
                Debug.Log("[RingworldSmoke] FULLSIZE arrival attempt="+attempt+" altitude="+arrival.Altitude+" terrain="+f.Settings.Terrain.Sample(arrival.Along,arrival.Across).Height+" axis="+v.transform.up+" up="+v.upAxis);
                float end=Time.realtimeSinceStartup+35,next=0;bool touched=false;int frames=0;
                double peakSpeed=0;
                while(Time.realtimeSinceStartup<end)
                {
                    var active=FlightGlobals.ActiveVessel;
                    if(active==null){fail("No active vessel after plane impact");yield break;}
                    if(f.Owns(active))
                    {
                        var c=f.Settings.Geometry.Coordinates(f.Position(active));double speed=f.Velocity(active).Length;
                        peakSpeed=Math.Max(peakSpeed,speed);touched|=active.Landed||active.parts.Exists(p=>p.GroundContact||p.PermanentGroundContact);
                        if(Time.realtimeSinceStartup>=next)
                        {next=Time.realtimeSinceStartup+3;Debug.Log("[RingworldSmoke] FULLSIZE tick attempt="+attempt+" frame="+frames+" altitude="+c.Altitude+" speed="+speed+" landed="+active.Landed+" parts="+active.parts.Count+" loaded="+FlightGlobals.VesselsLoaded.Count);}
                        if(!RingParameters.Finite(c.Altitude)||!RingParameters.Finite(speed)){fail("Non-finite full-size plane state");yield break;}
                    }
                    frames++;yield return null;
                }
                Debug.Log("[RingworldSmoke] FULLSIZE landing attempt="+attempt+" touched="+touched+" frames="+frames+" peakSpeed="+peakSpeed);
                if(!touched||frames<15){fail("Full-size plane did not reach the ground with live frame updates");yield break;}
                v=FlightGlobals.ActiveVessel;
            }
            Debug.Log("[RingworldSmoke] PASS full-size interstellar stock-plane natural-gravity landing and impact responsiveness");
        }
    }
}
#endif
