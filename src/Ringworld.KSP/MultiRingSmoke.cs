#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class MultiRingSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            var state=RingworldScenario.Instance;var v=FlightGlobals.ActiveVessel;
            bool missingCyla=Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-no-cyla")>=0;
            if(missingCyla)foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetType("Cyla.ShaderLoader",false)!=null){fail("Cyla still loaded in dependency-free fixture");yield break;}
            // An isolated save and a plain pod keep the test within the laptop budget.
            var primary=state.GetOptions().CreateCopy();primary.SetValue("ringId","primary",true);f.ApplyOptions(primary,true);
            var second=primary.CreateCopy();second.SetValue("ringId","test-ring",true);second.SetValue("ringName","Remote habitat",true);
            second.SetValue("spinDirection",-1,true);second.SetValue("panelsEnabled",false,true);second.SetValue("gravity",.75,true);
            second.SetValue("tiltX",31,true);second.SetValue("tiltY",67,true);second.SetValue("tiltZ",-112,true);
            second.SetValue("anchorId","body:Kerbin",true);second.SetValue("centerY",4e10,true);second.SetValue("designatedStar",false,true);second.SetValue("seed",90210,true);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-asteroid-anchor")>=0)
            {
                var orbit=new Orbit(7,.05,1.8e10,0,0,0,Planetarium.GetUniversalTime(),FlightGlobals.GetBodyByName("Sun"));
                var asteroid=DiscoverableObjectsUtil.SpawnAsteroid("Ringworld regression anchor",orbit,90210,(UntrackedObjectClass)0,1e8,1e8);
                second.SetValue("anchorId","vessel:"+asteroid.vesselID.ToString("D"),true);
            }
            else
            {
                second.SetValue("centerY",0,true);second.SetValue("radius",1e7,true);second.SetValue("width",1e6,true);
            }
            var candidate=Settings.Load();candidate.Apply(second);
            if(!PlacementLightingSmoke.Run(candidate,fail))yield break;
            var roundtrip=Settings.Load();roundtrip.Apply(candidate.Save());
            if(roundtrip.Geometry.P.SpinDirection!=-1||roundtrip.Geometry.P.PanelsEnabled||roundtrip.Geometry.P.Gravity!=.75||(roundtrip.Geometry.Axis-new RingBasis(31,67,-112).Y).Length>1e-12){fail("Placement controls did not roundtrip through saved settings");yield break;}
            var legacy=Settings.Load();legacy.Apply(primary);
            if(legacy.Geometry.P.SpinDirection!=1||!legacy.Geometry.P.PanelsEnabled){fail("Legacy ring placement defaults changed");yield break;}
            var kerbin=FlightGlobals.GetBodyByName("Kerbin");RingAnchorState anchor;string anchorError;
            if(!RingAnchorEphemeris.TryRelative(RingAnchorEphemeris.Id(kerbin),kerbin.referenceBody,Planetarium.GetUniversalTime()+3600,out anchor,out anchorError)||anchor.Velocity.Length<=0){fail("Body ephemeris sampling failed: "+anchorError);yield break;}
            Debug.Log("[RingworldSmoke] Placement settings persistence and future body ephemeris passed");
            if(RingSelection.Validate(candidate,null)!=null){fail("Separated habitat rejected");yield break;}
            var overlap=Settings.Load();overlap.Apply(primary);if(RingSelection.Validate(overlap,null)==null){fail("Overlapping habitat accepted");yield break;}
            state.Rings.Add(second);
            yield return new WaitForSecondsRealtime(2);
            if(UnityEngine.Object.FindObjectsOfType<ScaledRing>().Length!=2){fail("Both ring renderers were not created");yield break;}
            f.arrivalHeight=8;string reason;
            if(!f.VisitRing("test-ring",out reason)){fail(reason);yield break;}
            float end=Time.realtimeSinceStartup+70;
            while(!f.Ready&&Time.realtimeSinceStartup<end)yield return null;
            if(!f.Ready){fail("Remote ring transfer did not finish");yield break;}
            if(f.Settings.Geometry.P.Omega>=0||f.Settings.Geometry.P.PanelsEnabled){fail("Retrograde or disabled panel state was not activated");yield break;}
            yield return new WaitForSecondsRealtime(2);
            int panelsOn=0,panelsOff=0;
            foreach(var obj in Resources.FindObjectsOfTypeAll<GameObject>())if(obj.name=="Twenty shadow squares"){if(obj.activeSelf)panelsOn++;else panelsOff++;}
            if(panelsOn!=1||panelsOff!=1){fail("Panel toggle did not independently control both ring meshes");yield break;}
            if(Shader.GetGlobalFloat("_RingPanelsDisabled")!=1){fail("Disabled panel lighting flag was not uploaded");yield break;}
            while((!v.Landed||!f.surfaceWarp.CanAdvance(f))&&Time.realtimeSinceStartup<end)yield return new WaitForSecondsRealtime(.25f);
            if(!v.Landed||FlightGlobals.ClearToSave(false)!=ClearToSaveStatus.CLEAR){fail("Remote landing not saveable "+f.surfaceWarp.Status);yield break;}
            if(missingCyla){var options=f.Settings.Save();options.SetValue("atmosphereBackend",1,true);f.ApplyOptions(options,false);yield return new WaitForSecondsRealtime(2);if(f.visuals!=null&&f.visuals.CylaActive){fail("Missing Cyla backend reported active");yield break;}Debug.Log("[RingworldSmoke] Optional Cyla absent: requested backend safely falls back to Original");}
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            var fi=v.GetComponent<FlightIntegrator>();double lightTime=Planetarium.GetUniversalTime();
            double lightDistance=RingLighting.LightDistance(f.Settings,f.Position(v),lightTime);
            double expectedFlux=PhysicsGlobals.SolarLuminosity/(4*Math.PI*lightDistance*lightDistance)*RingLighting.Visibility(f.Settings,f.Position(v),lightTime)*Math.Exp(-v.atmDensity*.04);
            if(fi==null||Math.Abs(v.solarFlux-expectedFlux)>Math.Max(1,expectedFlux*.02)){fail("Moving-ring solar flux disagrees with physical eclipse");yield break;}
            f.Capture();var record=state.Vessels[v.id.ToString()];
            if(record.RingId!="test-ring"||!state.Occupied("test-ring")||(ConvertVector.Core(f.Center-f.Star.position)-f.FrameAnchorPosition).Length>1){fail("Remote residence identity/center incorrect");yield break;}
            Settings scienceSettings;ResearchLocation location;
            if(!RingScience.Resolve(v,out scienceSettings,out location)||!location.Id.StartsWith("test-ring-")){fail("Science not isolated by ring");yield break;}
            var expected=f.Position(v);double ut=Planetarium.GetUniversalTime();
            TimeWarp.SetRate(3,true);yield return new WaitForSecondsRealtime(2);TimeWarp.SetRate(0,true);yield return new WaitForSecondsRealtime(3);
            if((f.Position(v)-expected).Length>5||!v.Landed||Planetarium.GetUniversalTime()<=ut+2){fail("Remote landed warp failed");yield break;}
            Debug.Log("[RingworldSmoke] MULTIRING remote landing, science identity and native warp passed");
            MapView.EnterMapView();yield return new WaitForSecondsRealtime(2);PlanetariumCamera.fetch.SetDistance(f.Settings.Geometry.P.Radius<1e8?5000:5000000);yield return new WaitForSecondsRealtime(3);ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(KSPUtil.ApplicationRootPath,"RingworldMultiRingMap.png"));yield return new WaitForSecondsRealtime(2);MapView.ExitMapView();
            f.Capture();var id=v.id;var folder=HighLogic.SaveFolder;expected=f.Position(v);
            GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(),"persistent",folder,SaveMode.OVERWRITE);
            HighLogic.LoadScene(GameScenes.SPACECENTER);while(HighLogic.LoadedScene!=GameScenes.SPACECENTER)yield return null;yield return new WaitForSecondsRealtime(4);
            var reload=GamePersistence.LoadGame("persistent",folder,true,false);int index=reload.flightState.protoVessels.FindIndex(p=>p.vesselID==id);
            if(index<0){fail("Remote resident lost");yield break;}
            FlightDriver.StartAndFocusVessel(reload,index);
            end=Time.realtimeSinceStartup+70;
            while((!HighLogic.LoadedSceneIsFlight||RingworldFlight.Instance==null||!RingworldFlight.Instance.Ready)&&Time.realtimeSinceStartup<end)yield return null;
            f=RingworldFlight.Instance;if(f==null||!f.Ready){fail("Remote resume timeout");yield break;}
            yield return new WaitForSecondsRealtime(4);v=FlightGlobals.ActiveVessel;state=RingworldScenario.Instance;
            if(state.Rings.Count!=2||f.Settings.RingId!="test-ring"||!v.Landed||(f.Position(v)-expected).Length>5){fail("Remote save reload incorrect");yield break;}
            if(f.Settings.Geometry.P.SpinDirection!=-1||f.Settings.Geometry.P.PanelsEnabled||f.Settings.Geometry.P.Gravity!=.75||(f.Settings.Geometry.Axis-new RingBasis(31,67,-112).Y).Length>1e-12){fail("Placement controls lost after KSC save/reload");yield break;}
            var shifted=state.RingOptions("primary").CreateCopy();shifted.SetValue("centerY",-4e8,true);state.Rings[state.Rings.IndexOf(state.RingOptions("primary"))]=shifted;
            yield return new WaitForSecondsRealtime(2);
            if((f.Position(v)-expected).Length>5){fail("Moving another ring moved resident");yield break;}
            state.Rings.Remove(shifted);yield return new WaitForSecondsRealtime(2);
            if(UnityEngine.Object.FindObjectsOfType<ScaledRing>().Length!=1){fail("Deleted ring renderer retained");yield break;}
            yield return TrackingSmoke.Run(f,fail);
            f=RingworldFlight.Instance;v=FlightGlobals.ActiveVessel;
            if(f.Settings.AnchorId=="body:Kerbin")
            {
                f.Leave();
                FlightGlobals.fetch.SetShipOrbit(kerbin.flightGlobalsIndex,0,30000000,0,0,0,0,Planetarium.GetUniversalTime());
                yield return new WaitForSecondsRealtime(3);end=Time.realtimeSinceStartup+60;
                while((v.packed||v.mainBody!=kerbin)&&Time.realtimeSinceStartup<end)yield return null;
                if(v.packed||v.mainBody!=kerbin){fail("Planet-reference arrival fixture could not unpack around Kerbin");yield break;}
                var g=f.Settings.Geometry;var point=g.Position(0,0,150000);var spin=g.SpinVelocity(point);
                FloatingOrigin.SetOffset(f.Settings.InertialCenter+ConvertVector.Ksp(point));Krakensbane.ResetVelocityFrame(true);
                v.SetPosition(f.Settings.InertialCenter+ConvertVector.Ksp(point),true);v.SetWorldVelocity(ConvertVector.Ksp(spin));
                v.orbit.UpdateFromStateVectors(ConvertVector.Orbit(ConvertVector.Ksp(point)),ConvertVector.Orbit(ConvertVector.Ksp(spin)),kerbin,Planetarium.GetUniversalTime());
                v.Landed=false;v.Splashed=false;Physics.SyncTransforms();f.TryArrival();
                end=Time.realtimeSinceStartup+15;while(!f.Ready&&Time.realtimeSinceStartup<end)yield return null;
                if(!f.Ready||f.Velocity(v).Length>50){fail("Planet-reference arrival did not preserve spin-matched velocity: "+f.Velocity(v).Length);yield break;}
                Debug.Log("[RingworldSmoke] PASS automatic arrival from Kerbin reference; relative speed="+f.Velocity(v).Length);
            }
            Debug.Log("[RingworldSmoke] PASS multiple rings: independent rendering, overlap guard, offset flight/warp/science, KSC save roundtrip, move/delete unoccupied ring");
        }
    }
}
#endif
