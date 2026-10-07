#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class MapRenderingSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            var v=FlightGlobals.ActiveVessel;
            // The isolated fixture uses an ordinary Kerbol ring regardless of the
            // user's installed new-save preset. No user save or config is edited.
            var options=Settings.Load().Save();RingQualityPresets.Apply(options,6);
            options.SetValue("referenceBody","Sun",true);options.SetValue("anchorId","body:Sun",true);
            options.SetValue("radius",15300000000.0,true);options.SetValue("width",160500000.0,true);
            options.SetValue("surfaceDensity",0,true);options.SetValue("predictionSeconds",1000,true);options.SetValue("fullRingAtmosphere",true,true);
            options.SetValue("cloudAmount",.55,true);options.SetValue("atmosphereBackend",0,true);
            f.ApplyOptions(options,true);
            MapView.EnterMapView();yield return new WaitForSecondsRealtime(3);
            if(RingOrbitSplinePatch.ShouldHide(v.orbit)){fail("Ordinary planetary orbit replaced by ring coast");yield break;}
            Debug.Log("[RingworldSmoke] MAPRENDER unrelated stock orbit preserved");
            MapView.ExitMapView();f.arrivalHeight=400000;f.Visit();while(!f.Ready)yield return null;
            v.SetWorldVelocity(ConvertVector.Ksp(f.Settings.Geometry.Up(f.Position(v))*-1000));f.Leave();
            yield return new WaitForSecondsRealtime(3);MapView.EnterMapView();
            PlanetariumCamera.fetch.SetTarget(v.mapObject);PlanetariumCamera.fetch.SetDistance(20000);
            float timeout=Time.realtimeSinceStartup+30;
            while((f.trajectory.PointCount<2||f.trajectory.RenderedPointCount<2)&&Time.realtimeSinceStartup<timeout)yield return null;
            foreach(var dialog in UnityEngine.Object.FindObjectsOfType<PopupDialog>())dialog.Dismiss();
            if(f.trajectory.RenderedPointCount<2){fail("Native ring coast line not drawn: "+f.trajectory.Status);yield break;}
            Debug.Log("[RingworldSmoke] MAPRENDER coast points="+f.trajectory.PointCount+" renderPoints="+f.trajectory.RenderedPointCount+" frames="+f.trajectory.RenderFrames);
            var camera=PlanetariumCamera.Camera;var original=camera.transform.rotation;int frames=f.trajectory.RenderFrames;
            // Render explicitly after changing the camera pose. This catches an
            // Update/LateUpdate cache without waiting for another player frame.
            for(int i=0;i<6;i++)
            {
                camera.transform.rotation=original*Quaternion.Euler(i*1.5f,i*2,0);camera.Render();
                foreach(var name in new[]{"Ringworld global cloud shell","Ringworld full-ring atmosphere"})
                {
                    var layer=GameObject.Find(name);
                    if(layer==null&&(name=="Ringworld global cloud shell"||Extensions.ExtensionProviders.Scattering!=null))
                    {fail("Expected scaled visual layer missing: "+name);yield break;}
                    if(layer!=null&&layer.activeInHierarchy&&Vector3.Distance(layer.transform.position,camera.transform.position)>.001f)
                    {fail("Scaled visual layer did not use current render camera: "+name);yield break;}
                }
                var at=f.Star.position+ConvertVector.Ksp(RingAnchorEphemeris.OrbitRelative(v.orbit,f.Star,Planetarium.GetUniversalTime()).Position);
                var expected=camera.WorldToScreenPoint((Vector3)ScaledSpace.LocalToScaledSpace(at));
                if(expected.z>0&&(f.trajectory.RenderedHead-new Vector2(expected.x,expected.y)).magnitude>1.5f)
                {fail("Coast head does not follow current vessel/camera projection");yield break;}
                if(float.IsNaN(f.trajectory.RenderedHead.x)||float.IsInfinity(f.trajectory.RenderedHead.x))
                {fail("Non-finite coast projection");yield break;}
                yield return new WaitForEndOfFrame();
            }
            camera.transform.rotation=original;
            if(f.trajectory.RenderFrames<frames+6){fail("Coast did not draw on every camera render");yield break;}
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldMapRendering.png"));
            yield return new WaitForSecondsRealtime(1);
            var id=v.id;
            GamePersistence.SaveGame(HighLogic.CurrentGame.Updated(),"persistent",HighLogic.SaveFolder,SaveMode.OVERWRITE);
            HighLogic.LoadScene(GameScenes.TRACKSTATION);
            timeout=Time.realtimeSinceStartup+90;
            while((HighLogic.LoadedScene!=GameScenes.TRACKSTATION||TrackingRing.Trajectory==null||PlanetariumCamera.fetch==null)&&Time.realtimeSinceStartup<timeout)yield return null;
            v=FlightGlobals.Vessels.Find(x=>x.id==id);if(v==null){fail("Tracking fixture lost");yield break;}
            PlanetariumCamera.fetch.SetTarget(v.mapObject);PlanetariumCamera.fetch.SetDistance(20000);
            timeout=Time.realtimeSinceStartup+30;
            while(TrackingRing.Trajectory.RenderedPointCount<2&&Time.realtimeSinceStartup<timeout)yield return null;
            var trajectory=TrackingRing.Trajectory;
            if(trajectory.RenderedPointCount<2||!RingOrbitSplinePatch.ShouldHide(v.orbit)){fail("Tracking native coast missing");yield break;}
            yield return new WaitForEndOfFrame();
            foreach(var renderer in UnityEngine.Object.FindObjectsOfType<OrbitRendererBase>())
                if(renderer.vessel==v&&renderer.OrbitLine!=null&&renderer.OrbitLine.active)
                {fail("Retained stock spline is still active after suppression");yield break;}
            if(!RingOrbitSplinePatch.ShouldHideVessel(v)||RingPatchedConicLine.Suppressed==0){fail("Cloned patched-conic lines were not suppressed");yield break;}
            camera=PlanetariumCamera.Camera;frames=trajectory.RenderFrames;
            camera.transform.rotation*=Quaternion.Euler(3,6,0);camera.Render();
            if(trajectory.RenderFrames<=frames){fail("Tracking camera movement delayed coast drawing");yield break;}
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"RingworldTrackingRendering.png"));
            yield return new WaitForSecondsRealtime(1);
            // Surface record recognition is the same live path used by the stock
            // orbit Harmony hooks; it must clear both retained line and markers.
            var record=new VesselRecord{RingId=TrackingRing.Settings.RingId,Landed=true,Position=TrackingRing.Settings.Geometry.Position(1000000,0,5),Epoch=Planetarium.GetUniversalTime()};
            RingworldScenario.Instance.Vessels[v.id.ToString()]=record;
            if(!RingOrbitSplinePatch.ShouldHide(v.orbit)){fail("Landed resident stock orbit not hidden");yield break;}
            trajectory.Update();camera.Render();
            if(trajectory.PointCount!=0||trajectory.RenderedPointCount!=0){fail("Landed resident retained coast line");yield break;}
            Debug.Log("[RingworldSmoke] PASS native map and tracking camera-time drawing, scaled cloud/atmosphere placement, landed suppression");
        }
    }
}
#endif

