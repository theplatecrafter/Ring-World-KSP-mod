#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class TrackingSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            var v=FlightGlobals.ActiveVessel;
            f.arrivalHeight=3000;f.Visit();while(!f.Ready)yield return null;
            yield return new WaitForFixedUpdate();
            if(FlightGlobals.ClearToSave(false)==ClearToSaveStatus.CLEAR){fail("Airborne ring resident can save/exit");yield break;}
            Debug.Log("[RingworldSmoke] TRACKING unsupported ring save blocked");
            var options=f.Settings.Save();RingQualityPresets.Apply(options,10);f.ApplyOptions(options,true);
            f.arrivalHeight=400000;f.Visit();while(!f.Ready)yield return null;
            v.SetWorldVelocity(ConvertVector.Ksp(f.Settings.Geometry.Up(f.Position(v))*-1000));
            int cameraCompletions=RingCameraBlend.Completed;f.Leave();
            yield return new WaitForSecondsRealtime(3);
            if(RingCameraBlend.Completed<=cameraCompletions||RingCameraBlend.FirstFrameAngle>.1f)
            {fail("Camera departure blend did not preserve pose and complete");yield break;}
            Debug.Log("[RingworldSmoke] CAMERA departure blend completed; first-frame angle="+RingCameraBlend.FirstFrameAngle);
            var id=v.id;double start=Planetarium.GetUniversalTime();
            GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(),"persistent",HighLogic.SaveFolder,SaveMode.OVERWRITE);
            HighLogic.LoadScene(GameScenes.TRACKSTATION);
            while(HighLogic.LoadedScene!=GameScenes.TRACKSTATION||TrackingRing.Trajectory==null||PlanetariumCamera.fetch==null)yield return null;
            yield return new WaitForSecondsRealtime(4);
            v=FlightGlobals.Vessels.Find(x=>x.id==id);
            if(v==null){fail("Tracking vessel missing");yield break;}
            PlanetariumCamera.fetch.SetTarget(v.mapObject);
            yield return new WaitForSecondsRealtime(5);
            var trajectory=TrackingRing.Trajectory;
            Debug.Log("[RingworldSmoke] TRACKING points="+trajectory.PointCount+" encounters="+trajectory.EncounterCount+" status="+trajectory.Status);
            if(trajectory.PointCount<2||trajectory.EncounterCount==0||!RingOrbitSplinePatch.ShouldHide(v.orbit)){fail("Tracking encounter trajectory not replacing solar spline");yield break;}
            TimeWarp.SetRate(TimeWarp.fetch.warpRates.Length-1,true);
            if(TimeWarp.CurrentRateIndex!=0){fail("Tracking unsafe warp accepted");yield break;}
            int low=0;for(int i=0;i<TimeWarp.fetch.warpRates.Length;i++)if(TimeWarp.fetch.warpRates[i]<=10)low=i;
            TimeWarp.SetRate(low,true);
            yield return new WaitForSecondsRealtime(30);
            if(HighLogic.LoadedScene!=GameScenes.TRACKSTATION){fail("Tracking encounter forced an unsolicited Flight scene");yield break;}
            var savedType=v.vesselType;v.vesselType=VesselType.Debris;
            if(TrackingRing.GuardsEncounter(v)){fail("Crash debris participates in automatic encounter guard");yield break;}
            v.vesselType=savedType;
            TimeWarp.SetRate(0,true);
            var game=HighLogic.CurrentGame.Updated();
            int index=game.flightState.protoVessels.FindIndex(p=>p.vesselID==id);
            if(index<0){fail("Tracking vessel missing before explicit Fly");yield break;}
            FlightDriver.StartAndFocusVessel(game,index);
            float timeout=Time.realtimeSinceStartup+90;
            while(HighLogic.LoadedScene!=GameScenes.FLIGHT&&Time.realtimeSinceStartup<timeout)yield return null;
            timeout=Time.realtimeSinceStartup+90;
            while((RingworldFlight.Instance==null||!RingworldFlight.Instance.Ready)&&Time.realtimeSinceStartup<timeout)yield return null;
            f=RingworldFlight.Instance;v=FlightGlobals.ActiveVessel;
            if(f==null||!f.Ready||v.id!=id){fail("Tracking handoff lost vessel or rotating frame");yield break;}
            double altitude=f.Settings.Geometry.Coordinates(f.Position(v)).Altitude;
            Debug.Log("[RingworldSmoke] TRACKING handoff elapsed="+(Planetarium.GetUniversalTime()-start)+" altitude="+altitude+" speed="+f.Velocity(v).Length);
            if(altitude>410000||altitude<60000){fail("Tracking flight restore rolled back or crossed atmosphere unloaded");yield break;}
            if(FlightGlobals.ClearToSave(false)==ClearToSaveStatus.CLEAR){fail("Returning airborne resident can save");yield break;}
            Debug.Log("[RingworldSmoke] PASS tracking encounter, warp guard, station persistence and explicit Fly");
        }
    }
}
#endif
