#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    // Replay the moving Aeris report, with the real optional renderers installed.
    internal static class MovingLandingSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            var options=f.Settings.Save();RingQualityPresets.Apply(options,6);
            bool liveHang=Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-live-hang-replay")>=0;
            bool lightingOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-surface-lighting-only")>=0;
            options.SetValue("seed",lightingOnly?408656951:liveHang?1403775798:745138026,true);options.SetValue("atmosphereBackend",0,true);
            f.ApplyOptions(options,true);
            var wrapClock=System.Diagnostics.Stopwatch.StartNew();
            double wrap=UtilMath.ClampRadians(-616194345473.6643);
            double invalidWrap=UtilMath.ClampRadians(double.PositiveInfinity);
            if(wrap<0||wrap>2*Math.PI||!double.IsNaN(invalidWrap)||wrapClock.Elapsed.TotalSeconds>1||RingAngleSafety.Reduced<2)
            {fail("Large orbital angle was not reduced safely through Harmony");yield break;}
            Debug.Log("[RingworldSmoke] PASS live-hang orbital angle fixture / infinity terminates through stock helper");
            var v=FlightGlobals.ActiveVessel;
            v.ActionGroups.SetGroup(KSPActionGroup.Gear,true);
            foreach(var d in v.FindPartModulesImplementing<ModuleWheels.ModuleWheelDeployment>())
                AccessTools.Method(typeof(ModuleWheels.ModuleWheelDeployment),"ToggleDeployment").Invoke(d,new object[]{true});
            yield return new WaitForSecondsRealtime(4);
            f.arrivalHeight=lightingOnly?450:150;
            AccessTools.Method(typeof(RingworldFlight),"VisitCoordinates").Invoke(f,lightingOnly?new object[]{53993880082.137,76762176.0651363}:liveHang?new object[]{80814534555.0796,71203081.1879123}:new object[]{64135096458.85,-53929581.1946292});
            float deadline=Time.realtimeSinceStartup+60;
            while(!f.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!f.Ready){fail("Moving-plane arrival failed");yield break;}
            if(lightingOnly){yield return SurfaceLightingSmoke.Run(f,fail);yield break;}
            // Capture the unchanged rendering with an airborne stationary fixture first.
            float scale=Time.timeScale;Time.timeScale=0;
            yield return new WaitForSecondsRealtime(5);
            var camera=FlightCamera.fetch.mainCamera;
            var surface=(SurfaceStreamer)AccessTools.Field(typeof(RingworldFlight),"surface").GetValue(f);
            int retainedPeak=0;var origin=f.Settings.Geometry.Coordinates(f.Position(v));
            for(int step=1;step<=20;step++)
            {
                var point=f.Settings.Geometry.Position(origin.Along+step*f.Settings.TileSize,origin.Across,origin.Altitude);
                for(int frame=0;frame<8;frame++){surface.Update(point,f.Center);retainedPeak=Math.Max(retainedPeak,surface.TileCount);}
            }
            surface.Update(f.Position(v),f.Center,true);
            int footprint=(2*f.Settings.TileRadius+1)*(2*f.Settings.TileRadius+1);
            Debug.Log("[RingworldSmoke] STREAMING 20 km traversal retainedPeak="+retainedPeak+" footprint="+footprint+" pending="+f.LodPending);
            if(retainedPeak>footprint*2+2*(2*f.Settings.TileRadius+1)){fail("Near tiles accumulated across flight path");yield break;}
            var viewPoint=f.Position(v);var viewUp=f.Settings.Geometry.Up(viewPoint);var viewHeading=f.Settings.Geometry.AlongDirection(viewPoint);
            camera.transform.position=v.transform.position+ConvertVector.Unity(viewUp*35-viewHeading*45);
            camera.transform.rotation=Quaternion.LookRotation(v.transform.position-camera.transform.position,ConvertVector.Unity(viewUp));
            foreach(var light in UnityEngine.Object.FindObjectsOfType<Light>())
                if(light.enabled&&light.type==LightType.Directional)
                    Debug.Log("[RingworldSmoke] LIGHT "+light.name+" intensity="+light.intensity+" colour="+light.color+" mask="+light.cullingMask);
            Debug.Log("[RingworldSmoke] LIGHT ambient="+RenderSettings.ambientLight+" intensity="+RenderSettings.ambientIntensity+" mode="+RenderSettings.ambientMode+" path="+camera.actualRenderingPath+" HDR="+camera.allowHDR+" sky="+(RenderSettings.skybox==null?"none":RenderSettings.skybox.name));
            Debug.Log("[RingworldSmoke] LIGHT reflection="+RenderSettings.reflectionIntensity+" deferredAmbient="+Shader.GetGlobalFloat("deferredAmbientBrightness")+" legacy="+Shader.GetGlobalColor("legacyAmbientColor"));
            bool observedLighting=false;string lightingFailure=null;Camera.CameraCallback inspect=rendering=>
            {
                if(rendering!=camera)return;observedLighting=true;
                int keys=0;foreach(var light in UnityEngine.Object.FindObjectsOfType<Light>())if(light.enabled&&light.intensity>1e-6&&light.type==LightType.Directional&&(light.cullingMask&(1<<15))!=0)keys++;
                if(keys!=1)lightingFailure="Expected one terrain key light; got "+keys;
                if(Shader.GetGlobalFloat("deferredAmbientBrightness")!=0)lightingFailure="Planetary diffuse probe contribution leaked into ring camera";
            };
            Camera.onPreRender+=inspect;
            try{Capture(camera,"normal");}finally{Camera.onPreRender-=inspect;}
            if(!observedLighting||lightingFailure!=null){fail(lightingFailure??"Lighting scope not observed");yield break;}
            float reflections=RenderSettings.reflectionIntensity;RenderSettings.reflectionIntensity=0;Capture(camera,"no-reflections");RenderSettings.reflectionIntensity=reflections;
            float ambient=Shader.GetGlobalFloat("deferredAmbientBrightness");Shader.SetGlobalFloat("deferredAmbientBrightness",0);Capture(camera,"no-deferred-ambient");Shader.SetGlobalFloat("deferredAmbientBrightness",ambient);
            var path=camera.renderingPath;camera.renderingPath=RenderingPath.Forward;Capture(camera,"forward");camera.renderingPath=path;
            var originalMaterials=new System.Collections.Generic.Dictionary<MeshRenderer,Material>();
            foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(renderer.name.StartsWith("Ringworld terrain "))
            {
                originalMaterials[renderer]=renderer.sharedMaterial;
                if(originalMaterials.Count==1){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);var tex=block.GetTexture("_MainTex");Debug.Log("[RingworldSmoke] TERRAIN shader="+originalMaterials[renderer].shader.name+" tint="+originalMaterials[renderer].color+" texture="+tex+" sample="+f.Settings.Terrain.Sample(64135096458.85,-53929581.1946292).GroundColor);}
                renderer.sharedMaterial.SetFloat("_AlbedoOnly",1);
            }
            Capture(camera,"albedo");foreach(var kv in originalMaterials)kv.Value.SetFloat("_AlbedoOnly",0);
            var post=OptionalVisualIntegrations.FindType("UnityEngine.Rendering.PostProcessing.PostProcessLayer");var layer=post==null?null:camera.GetComponent(post) as Behaviour;
            if(layer!=null){bool wasEnabled=layer.enabled;layer.enabled=false;Capture(camera,"no-post");layer.enabled=wasEnabled;}
            var atmosphere=(AtmosphereRenderer)AccessTools.Field(typeof(RingworldFlight),"atmosphere").GetValue(f);
            atmosphere.Update(false,f.Center);Capture(camera,"no-sky");atmosphere.Update(true,f.Center);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath,"MovingLanding-before.png"));
            yield return new WaitForSecondsRealtime(1);
            Time.timeScale=scale;
            CheatOptions.NoCrashDamage=false;CheatOptions.UnbreakableJoints=false;
            var p=f.Position(v);var up=f.Settings.Geometry.Up(p);var heading=f.Settings.Geometry.AlongDirection(p);
            // Keep the upright runway roll preserved by Transfer. Building a
            // fresh Z-up quaternion can invert an SPH craft's control axes.
            v.SetRotation(Quaternion.FromToRotation(v.ReferenceTransform.up,ConvertVector.Unity(heading))*v.transform.rotation,false);
            bool gentle=Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-gentle-landing")>=0;
            v.SetWorldVelocity(ConvertVector.Ksp(heading*170-up*(gentle?4:8)));RingCollisionFrame.Reset(v);
            float pilotInput=0;FlightInputCallback pilot=controls=>controls.pitch=pilotInput;
            if(gentle)v.OnFlyByWire+=pilot;
            float end=Time.realtimeSinceStartup+(gentle?180:90),next=0;int frames=0;bool touched=false;double trim=0;
            while(Time.realtimeSinceStartup<end)
            {
                var active=FlightGlobals.ActiveVessel;
                if(active==null){fail("No cockpit/debris vessel after moving impact");yield break;}
                if(f.Owns(active))
                {
                    var c=f.Settings.Geometry.Coordinates(f.Position(active));var ground=f.Settings.Terrain.Sample(c.Along,c.Across);
                    touched|=active.Landed||active.parts.Exists(part=>part.GroundContact||part.PermanentGroundContact)||
                        (c.Altitude-ground.Height<3&&active.parts.Count<40);
                    if(gentle&&active.parts.Count==40)
                    {
                        // Normal control-surface input: no artificial gravity or
                        // velocity/rotation changes during the approach itself.
                        var vertical=DVec.Dot(f.Velocity(active),f.Settings.Geometry.Up(f.Position(active)));
                        double error=(c.Altitude-ground.Height>20?-5:-1)-vertical;
                        trim=Math.Max(-.15,Math.Min(.15,trim+error*Time.deltaTime*.002));
                        pilotInput=(float)Math.Max(-.4,Math.Min(.4,error*.025+trim));
                    }
                        if(Time.realtimeSinceStartup>=next){next=Time.realtimeSinceStartup+2;Debug.Log("[RingworldSmoke] MOVING frame="+frames+" AGL="+(c.Altitude-ground.Height)+" speed="+f.Velocity(active).Length+" pitch="+active.ctrlState.pitch+" parts="+active.parts.Count+" loaded="+FlightGlobals.VesselsLoaded.Count+" landed="+active.Landed+" tiles="+surface.TileCount+" frameTime="+Time.unscaledDeltaTime);}
                }
                frames++;yield return null;
            }
            if(gentle)v.OnFlyByWire-=pilot;FlightInputHandler.state.pitch=0;
            if(!touched||frames<30){fail("Moving plane did not contact terrain with live updates");yield break;}
            if(RingParticipantConics.Suppressed==0){fail("Recreated participant conic solver was not intercepted");yield break;}
            var invalid=new Orbit(0,0,100000000,0,0,0,Planetarium.GetUniversalTime(),f.Star);invalid.meanAnomalyAtEpoch=double.NaN;
            if(RingAnchorEphemeris.OrbitReady(invalid)){fail("NaN orbit anomaly accepted");yield break;}
            var corpse=FlightGlobals.ActiveVessel;
            if(!RingworldFlight.LiveCraft(corpse))
            {
                double epoch=f.FrameEpoch;var position=corpse.transform.position;int records=RingworldScenario.Instance.Vessels.Count;
                f.VisitRandomTerrain(1871930414);
                if(f.FrameEpoch!=epoch||corpse.packed||corpse.transform.position!=position||RingworldScenario.Instance.Vessels.Count!=records)
                {fail("Dead-craft random relocation changed physics/frame state");yield break;}
                for(int i=0;i<60;i++)yield return null;
                Debug.Log("[RingworldSmoke] PASS post-crash Random terrain refused without packing or moving the corpse/debris; 60 further frames");
            }
            else {fail("Dead-craft relocation fixture was not destroyed");yield break;}
            var bridge=AccessTools.TypeByName("Ringworld.Parallax.BridgeSmoke");
            if(bridge!=null)AccessTools.Method(bridge,"ValidateBiomes").Invoke(null,new object[]{f});
            Debug.Log("[RingworldSmoke] PASS moving Aeris impact responsiveness frames="+frames);
        }
        private static void Capture(Camera camera,string label)
        {
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            var previous=camera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(KSPUtil.ApplicationRootPath,"MovingLanding-"+label+".png"),pixels.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.Destroy(pixels);target.Release();UnityEngine.Object.Destroy(target);}
        }
    }
}
#endif
