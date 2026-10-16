#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // Derived workload, not the reporter's complete modded career save. Retain
    // its exact world options/time/approach orbit, but use a supported stock pod.
    internal static class ReporterLagSmoke
    {
        private static ConfigNode source,options;
        internal static bool Enabled {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-reporter-lag-only")>=0;}}
        internal static void Prepare(ConfigNode game)
        {
            source=ConfigNode.Load(Path.Combine(KSPUtil.ApplicationRootPath,"ReporterLagSource.sfs")).GetNode("GAME");
            var original=source.GetNode("FLIGHTSTATE");var vessels=original.GetNodes("VESSEL");
            var active=vessels[int.Parse(original.GetValue("activeVessel"))];
            foreach(var s in source.GetNodes("SCENARIO"))if(s.GetValue("name")==nameof(RingworldScenario))
            {options=(s.GetNode("RING")??s.GetNode("OPTIONS")).CreateCopy();break;}
            if(options==null)throw new Exception("Reporter ring layout missing");
            var state=game.GetNode("FLIGHTSTATE");state.SetValue("UT",original.GetValue("UT"),true);
            var fixture=state.GetNodes("VESSEL")[0];
            foreach(var v in state.GetNodes("VESSEL"))if(v!=fixture)state.RemoveNode(v);
            fixture.RemoveNodes("ORBIT");fixture.AddNode(active.GetNode("ORBIT").CreateCopy());
            fixture.SetValue("sit","ORBITING",true);fixture.SetValue("landed",false,true);fixture.SetValue("splashed",false,true);
            fixture.SetValue("name","Reporter approach - stock pod workload",true);
            var parts=fixture.GetNodes("PART");for(int i=1;i<parts.Length;i++)fixture.RemoveNode(parts[i]);parts[0].RemoveValues("attN");
            parts[0].SetValue("name","mk1-3pod",true);parts[0].SetValue("rTrf","mk1-3pod",true);
            state.SetValue("activeVessel",0,true);
        }
        internal static IEnumerator Run(RingworldFlight f)
        {
            CheatOptions.IgnoreMaxTemperature=true;CheatOptions.NoCrashDamage=true;CheatOptions.UnbreakableJoints=true;
            var originalOptions=options.ToString();f.ApplyOptions(options.CreateCopy(),true);
            var v=FlightGlobals.ActiveVessel;double now=Planetarium.GetUniversalTime();
            RingAnchorState orbital;
            if(!RingAnchorEphemeris.TryOrbitRelative(v.orbit,f.Star,now,out orbital))throw new Exception("Reporter approach orbit not ready");
            var c=f.Settings.Geometry.Coordinates(orbital.Position);
            UnityEngine.Debug.Log("[RingworldSmoke] REPORTER retained seed="+f.Settings.Geometry.P.Seed+" density="+f.Settings.Geometry.P.SurfaceDensity+" lodRange="+f.Settings.LodRange+" UT="+now+" radialAltitude="+c.Altitude+" across="+c.Across+" parts="+v.parts.Count);
            int eclipsed=0;
            for(int i=0;i<160;i++)
            {
                var p=f.Settings.Geometry.Position(i*f.Settings.Geometry.P.Circumference/160,(i%3-1)*f.Settings.Geometry.P.Width*.49,i%2==0?100:220000);
                double previous=RingLighting.VisibilityReference(f.Settings,p,now),filtered=RingLighting.Visibility(f.Settings,p,now);
                if(previous!=filtered)throw new Exception("Eclipse broad phase changed finite-star visibility");if(previous<1)eclipsed++;
            }
            UnityEngine.Debug.Log("[RingworldSmoke] PASS reporter eclipse filtering equals unfiltered 160 positions; shadowed="+eclipsed);
            var sky=new RingAtmosphere(f.Settings.Geometry);var origin=f.Settings.Geometry.Position(c.Along,c.Across,1700);var sunPosition=RingLighting.LightPosition(f.Settings,now);
            for(int i=0;i<80;i++)
            {
                var ray=new DVec(Math.Cos(i*.41),Math.Sin(i*.29),Math.Cos(i*.17)).Unit;
                var a=sky.Sky(origin,ray,now,true);var b=sky.SkyFromLight(origin,ray,now,sunPosition);
                if((a.Radiance-b.Radiance).Length>1e-9||Math.Abs(a.Opacity-b.Opacity)>1e-12)throw new Exception("Frozen illumination changed scattering integration");
            }
            UnityEngine.Debug.Log("[RingworldSmoke] PASS sky illumination snapshot retains original radiance/opacity across 80 directions");
            // Same view/flight instance: map approach with numerical gravity,
            // then resting/fast arrivals with full saved terrain range intact.
            MapView.EnterMapView();yield return new WaitForSecondsRealtime(3);
            yield return Measure("saved-orbit-map",f,20);
            MapView.ExitMapView();yield return new WaitForSecondsRealtime(1);
            f.arrivalHeight=220000;f.Visit();float end=Time.realtimeSinceStartup+90;
            while(!f.Ready&&Time.realtimeSinceStartup<end)yield return null;
            if(!f.Ready)throw new Exception("Derived reporter approach placement timed out");
            yield return null;
            ((AtmosphereRenderer)AccessTools.Field(typeof(RingworldFlight),"atmosphere").GetValue(f)).TestCloudData();
            UnityEngine.Debug.Log("[RingworldSmoke] PASS fallback cloud deck retains original vertices/colours/UV/coverage across all three layers");
            yield return Measure("resting-high-ring",f,15);
            var position=f.Position(v);v.SetWorldVelocity(ConvertVector.Ksp(f.Settings.Geometry.SpinVelocity(position).Unit*-394124.410+f.Settings.Geometry.Up(position)*-266));
            yield return Measure("fast-ring-394kmps",f,20);
            MapView.EnterMapView();yield return Measure("fast-ring-map",f,15);MapView.ExitMapView();
            if(options.ToString()!=originalOptions)throw new Exception("Source ring options mutated");
            UnityEngine.Debug.Log("[RingworldSmoke] PASS reporter-derived lag workload complete; original world options/source preserved; upstream craft/module stack not reproduced");
        }
        private static IEnumerator Measure(string label,RingworldFlight f,float seconds)
        {
            int frames=0,fixedTicks=0;float end=Time.realtimeSinceStartup+seconds,start=Time.realtimeSinceStartup;
            int previous=(int)(Time.fixedTime/Time.fixedDeltaTime);
            while(Time.realtimeSinceStartup<end)
            {frames++;int current=(int)(Time.fixedTime/Time.fixedDeltaTime);fixedTicks+=Math.Max(0,current-previous);previous=current;yield return null;}
            double elapsed=Time.realtimeSinceStartup-start;
            UnityEngine.Debug.Log("[RingworldSmoke] REPORTER_TIMING "+label+" frames="+frames+" wallSeconds="+elapsed+" fps="+(frames/elapsed)+" fixedTicks="+fixedTicks+" near="+f.NearTileCount+" lod="+f.LodCount+" queued="+f.LodPending+" speed="+f.Velocity(FlightGlobals.ActiveVessel).Length);
        }
    }
}
#endif
