#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Linq;
using System.Diagnostics;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class Issue7PerformanceSmoke
    {
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        internal static IEnumerator Run(RingworldFlight f)
        {
            var sun=FlightGlobals.GetBodyByName("Sun");var kerbin=FlightGlobals.GetBodyByName("Kerbin");
            double now=Planetarium.GetUniversalTime();
            var orbit=new Orbit(12,.45,10500000,23,17,.3,now,kerbin);
            orbit.StartUT=now;orbit.EndUT=now+10;orbit.patchEndTransition=Orbit.PatchTransitionType.FINAL;orbit.nextPatch=new Orbit();
            // Reproduce the exact old chain: it walks the final conic's unused
            // slot, then dereferences its null parent while constructing the ID.
            bool oldFailed=false;try{var old=orbit;while(old.nextPatch!=null&&old.EndUT>old.StartUT&&now+20>=old.EndUT)old=old.nextPatch;var oldId="body:"+old.referenceBody.name;}
            catch(NullReferenceException){oldFailed=true;}
            Check(oldFailed,"Old final-patch null reference was not reproduced");
            string error;RingAnchorState expectedParent,state;
            Check(RingAnchorEphemeris.TryBodyRelative(kerbin,sun,now+20,out expectedParent,out error),"Kerbin parent sample failed");
            var expected=expectedParent.Position+ConvertVector.Core(ConvertVector.Orbit(orbit.getRelativePositionAtUT(now+20)));
            var clock=Stopwatch.StartNew();
            for(int i=0;i<1000;i++)
            {
                Check(RingAnchorEphemeris.TryOrbitRelative(orbit,sun,now+20,out state),"Final conic became unavailable");
                Check((state.Position-expected).Length<.001,"Final conic lost parent-frame position");
            }
            double correctedMs=clock.Elapsed.TotalMilliseconds;
            var next=new Orbit(0,.1,1.8e10,0,0,.4,now,sun){StartUT=now+10,patchEndTransition=Orbit.PatchTransitionType.FINAL};
            orbit.patchEndTransition=Orbit.PatchTransitionType.ESCAPE;
            Check(!RingAnchorEphemeris.TryOrbitRelative(orbit,sun,now+20,out state),"Pending next patch was sampled");
            double eta;Check(!TrackingRing.TryEncounter(orbit,f.Settings.Geometry,now,60,out eta,reference:sun)&&double.IsInfinity(eta),"Pending encounter returned an invented hit");
            orbit.nextPatch=next;
            Check(RingAnchorEphemeris.TryOrbitRelative(orbit,sun,now+20,out state)&&
                (state.Position-ConvertVector.Core(ConvertVector.Orbit(next.getRelativePositionAtUT(now+20)))).Length<.001,"Real SOI escape sampled wrong frame");
            next.patchEndTransition=Orbit.PatchTransitionType.ESCAPE;next.EndUT=now+15;next.nextPatch=orbit;
            Check(!RingAnchorEphemeris.TryOrbitRelative(orbit,sun,now+20,out state),"Cyclic conics failed to terminate");
            Check(!RingAnchorEphemeris.TryOrbitRelative(null,sun,now,out state)&&!RingAnchorEphemeris.TryOrbitRelative(orbit,null,now,out state)&&!RingAnchorEphemeris.TryOrbitRelative(orbit,sun,double.NaN,out state),"Invalid orbit arguments reached KSP solver");
            next.nextPatch=null;next.patchEndTransition=Orbit.PatchTransitionType.FINAL;orbit.nextPatch=new Orbit();orbit.patchEndTransition=Orbit.PatchTransitionType.FINAL;
            Check(TrackingRing.TryEncounter(orbit,f.Settings.Geometry,now,3600,out eta,reference:sun),"Eccentric Kerbin-SOI encounter sampling failed");
            // Cross-check allocation-light celestial sampling against the retained
            // generic double-precision implementation for every installed body.
            foreach(var body in FlightGlobals.Bodies)foreach(double dt in new[]{0d,60d,600d})
            {
                Check(RingAnchorEphemeris.TryBodyRelative(body,sun,now+dt,out state,out error),"Body path failed: "+body.name);
                var baseline=AnchorEphemeris.Relative("body:"+body.name,"body:Sun",now+dt,(id,time)=>{
                    var b=FlightGlobals.Bodies.Find(x=>"body:"+x.name==id);var o=b.orbit;
                    if(o==null||o.referenceBody==null||o.referenceBody==b)return new AnchorOrbitSample(null,new RingAnchorState());
                    var p=ConvertVector.Core(ConvertVector.Orbit(o.getRelativePositionAtUT(time)));var vel=ConvertVector.Core(ConvertVector.Orbit(o.getOrbitalVelocityAtUT(time)));
                    return new AnchorOrbitSample("body:"+o.referenceBody.name,new RingAnchorState(p,vel,p*(-o.referenceBody.gravParameter/Math.Pow(p.Length,3))));});
                Check((state.Position-baseline.Position).Length<.001&&(state.Velocity-baseline.Velocity).Length<1e-7,"Optimized ephemeris differs: "+body.name);
            }
            var stats=Harmony.GetPatchInfo(AccessTools.Method(typeof(VesselPrecalculate),"CalculatePhysicsStats"));
            Check(stats.Postfixes.Any(p=>p.owner==StockIntegration.OwnerFor(typeof(RingOrientationPatch)))&&
                !stats.Prefixes.Any(p=>p.owner==StockIntegration.OwnerFor(typeof(RingOrientationPatch))),"Ring orientation replaced upstream physics calculation");
            Check(stats.Owners.Contains("KSPCommunityFixes"),"Real Community Fixes physics owner missing from fixture");
            UnityEngine.Debug.Log("[RingworldSmoke] PASS issue7 old null-patch exception reproduced; 1000 corrected samples="+correctedMs.ToString("F3")+" ms; pending/cyclic/null patches, real Kerbin-to-Sun SOI and all-body ephemeris equivalence; KSPCF override retained");

            var options=f.Settings.Save();options.SetValue("lodRange",200000,true);options.SetValue("forestQuality",0,true);options.SetValue("tileRadius",2,true);
            var settings=Settings.Load();settings.Apply(options);var streamer=new SurfaceStreamer(settings);
            int maximum=0,commits=0;var originalSettings=settings.Save().ToString();
            try
            {
                int footprint=(settings.TileRadius*2+1)*(settings.TileRadius*2+1);
                // Eight km/frame is 400 km/s at a 20 ms physics step. Always
                // move before the current near footprint can finish generating.
                int frames=0;
                for(int i=0;i<3000;i++)
                {
                    double along=20000000+i*8000;
                    settings.Geometry.OrientationRadians=i*.0001;
                    streamer.Update(settings.Geometry.Position(along,0,20000),f.Center);
                    maximum=Math.Max(maximum,streamer.TileCount);
                    Check(streamer.TileCount<=3*footprint,"Near collider trail grew beyond visible/pending/current footprints");
                    var lod=(TerrainLod)AccessTools.Field(typeof(SurfaceStreamer),"lod").GetValue(streamer);
                    commits=lod.GenerationCommits;
                    // Every published near hole must still have its actual tiles.
                    Check(lod.VisibleFootprintReady((x,y)=>streamer.TestNearReady(x,y)),"LOD published or retained a hole with missing tiles");
                    if(i%100==0)streamer.TestPublishedData();
                    frames=i+1;
                    if(commits>=2&&i>=240)break;
                    yield return null;
                }
                Check(commits>=2,"Fast motion permanently stalled LOD publication");
                streamer.Update(settings.Geometry.Position(25000000,0,20000),f.Center,true);
                streamer.TestPublishedData();
                Check(streamer.TestNearReady((long)Math.Floor(25000000/settings.TileSize),0),"Immediate relocation lost collision tiles");
                Check(settings.Save().ToString()==originalSettings,"Streaming optimization changed saved settings");
                UnityEngine.Debug.Log("[RingworldSmoke] PASS issue7 fast streaming frames="+frames+"; peak near="+maximum+" bounded="+(3*footprint)+" LOD commits="+commits+"; all published holes covered and immediate tiles ready; no settings mutation");
            }
            finally{streamer.Dispose();}
            // Reuse the existing native Map/Tracking test to verify that camera
            // projection remains immediate after predictor scheduling changes.
            var map=MapRenderingSmoke.Run(f,message=>{throw new Exception(message);});
            while(map.MoveNext())yield return map.Current;
            UnityEngine.Debug.Log("[RingworldSmoke] PASS issue7 performance regression complete");
        }
    }
}
#endif
