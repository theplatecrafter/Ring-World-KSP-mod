#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    internal static class VisualOptionsSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            for(int i=0;i<RingQualityPresets.Names.Length;i++)
            {
                var n=f.Settings.Save();RingQualityPresets.Apply(n,i);var check=Settings.Load();check.Apply(n);
                if(RingQualityPresets.Match(check)!=RingQualityPresets.Names[i]){fail("Expanded preset roundtrip "+i);yield break;}
                if(i==0&&(check.WaterQuality!=4||check.Cyla["ViewSteps"]!=500)){fail("Cow maximum optics/water");yield break;}
                if(i==10&&(check.WaterQuality!=0||check.Cyla["ViewSteps"]!=1)){fail("Rotten minimum optics/water");yield break;}
            }
            var toggle=f.Settings.Save();toggle.SetValue("waterExtension",false,true);var disabled=Settings.Load();disabled.Apply(toggle);var restored=Settings.Load();restored.Apply(disabled.Save());if(restored.WaterExtension){fail("Water extension disable did not persist");yield break;}
            var custom=f.Settings.Save();foreach(var d in CylaOptions.Definitions)custom.SetValue(d.Key,((d.Min+d.Max)/2).ToString("R",System.Globalization.CultureInfo.InvariantCulture),true);
            custom.SetValue("cylaLightingMode",2,true);var first=Settings.Load();first.Apply(custom);var second=Settings.Load();second.Apply(first.Save());
            foreach(var d in CylaOptions.Definitions)if(first.Save().GetValue(d.Key)!=second.Save().GetValue(d.Key)){fail("Cyla option roundtrip "+d.Key);yield break;}
            Debug.Log("[RingworldSmoke] VISUAL OPTIONS all 11 presets and 21 Cyla optical values roundtrip; highest tiers checked without rendering");
            f.arrivalHeight=80;f.Visit();while(!f.Ready)yield return null;for(int i=0;i<30;i++)yield return null;
            var vessel=FlightGlobals.ActiveVessel;var part=vessel.rootPart;
            RingworldSurfaceState surfaceState;
            if(!RingworldSurfaceApi.TryGetSurfaceState(vessel,out surfaceState)||Vector3d.Dot(surfaceState.StationaryFrameAcceleration,surfaceState.SurfaceUp)>=-1||double.IsNaN(surfaceState.FrameAcceleration.magnitude))
            {fail("Ring acceleration API is invalid");yield break;}
            var biomeScanner=(ModuleBiomeScanner)part.AddModule("ModuleBiomeScanner");
            var resourceScanner=(ModuleResourceScanner)part.AddModule("ModuleResourceScanner");
            bool landed=vessel.Landed;
            try
            {
                bool unlocked=ResourceMap.Instance.IsBiomeUnlocked(vessel.mainBody.flightGlobalsIndex,"LANDED");
                vessel.Landed=true;biomeScanner.FixedUpdate();biomeScanner.RunAnalysis();resourceScanner.Update();
                if(!biomeScanner.Events["RunAnalysis"].active||resourceScanner.abundanceDisplay!="Ring resource definition unavailable"||resourceScanner.abundance!=0||ResourceMap.Instance.IsBiomeUnlocked(vessel.mainBody.flightGlobalsIndex,"LANDED")!=unlocked)
                {fail("Ring scanner scope or host resource isolation failed");yield break;}
                Debug.Log("[RingworldSmoke] SCANNERS ring biome action, unavailable resource display and host-unlock isolation passed");
            }
            finally {vessel.Landed=landed;part.RemoveModule(biomeScanner);part.RemoveModule(resourceScanner);}
            RingCompatibilitySmoke.Run(vessel);
              // Check the replacement's live routing without rendering a heavy preset.
            int oldMode=f.Settings.CloudMode,oldSteps=f.Settings.CloudSteps;bool oldCloudExtension=f.Settings.CloudExtension;
            f.Settings.CloudMode=1;f.Settings.CloudSteps=32;f.Settings.CloudExtension=true;
            f.visuals.Prepare(true,f.Settings.Center);
            if(!f.visuals.CloudRendering){fail("Cloud extension did not activate");yield break;}
            f.Settings.CloudExtension=false;f.visuals.Prepare(true,f.Settings.Center);
            if(f.visuals.CloudRendering){fail("Cloud extension disable left volumes active");yield break;}
            f.Settings.CloudMode=oldMode;f.Settings.CloudSteps=oldSteps;f.Settings.CloudExtension=oldCloudExtension;f.visuals.Prepare(true,f.Settings.Center);
            Debug.Log("[RingworldSmoke] CLOUD EXTENSION flight routing enable/disable/restore passed without changing terrain preset");
            var surface=(SurfaceStreamer)AccessTools.Field(typeof(RingworldFlight),"surface").GetValue(f);
            var coord=f.Settings.Geometry.Coordinates(f.Position(FlightGlobals.ActiveVessel));bool wet=false;
            for(int i=0;i<20000&&!wet;i++)
            {
                double a=coord.Along+(i%200-100)*200,b=coord.Across+(i/200-50)*200;var t=f.Settings.Terrain.Sample(a,b);
                if(t.Wet&&t.WaterHeight-surface.CameraFloor(a,b)>2){wet=true;if(surface.CameraFloor(a,b)!=surface.CollisionHeight(a,b)){fail("Camera still blocked by water");yield break;}}
            }
            if(!wet){fail("Wet camera-floor fixture missing");yield break;}
            if(f.visuals.WaterShader()==null||!f.visuals.WaterShader().isSupported){fail("Water shader unsupported");yield break;}
            try{CheckWater(f.visuals.WaterShader(),new[]{0,1,2});CheckWater(f.visuals.WaterShader(true),new[]{3,4});}catch(Exception e){fail("Water transparency: "+e.Message);yield break;}
            Debug.Log("[RingworldSmoke] WATER compiled shader supported; camera floor follows submerged ground");
            f.Settings.CloudAmount=1;f.Settings.DynamicWeather=false;f.Settings.RainEnabled=true;
            double ut=Planetarium.GetUniversalTime();
            foreach(int edge in new[]{512,1536})
            {
                if(!f.visuals.BeginPhoto(1,6,edge)){fail("Photo entry "+f.visuals.Status);yield break;}
                var clock=System.Diagnostics.Stopwatch.StartNew();while(f.visuals.PhotoActive&&!f.visuals.PhotoFinished&&clock.Elapsed.TotalSeconds<180)yield return null;
                if(!f.visuals.PhotoFinished){fail("Photo did not finish: "+f.visuals.Status);yield break;}
                if(f.weatherEffects==null||f.weatherEffects.Drops==0){fail("Photo storm precipitation missing");yield break;}
                Debug.Log("[RingworldSmoke] PHOTO precipitation drops="+f.weatherEffects.Drops+" kind="+f.weatherEffects.Description);
                var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(f.visuals.LastPhoto));
                int width=image.width,height=image.height;var pixels=image.GetPixels32();int different=0;var baseColor=pixels[0];foreach(var c in pixels)if(c.r!=baseColor.r||c.g!=baseColor.g||c.b!=baseColor.b)different++;
                UnityEngine.Object.Destroy(image);
                if(Math.Max(width,height)!=edge||Math.Abs((double)width/height-(double)Screen.width/Screen.height)>.01||different<pixels.Length/10){fail("Photo size/aspect/content invalid "+width+"x"+height);yield break;}
                File.Copy(f.visuals.LastPhoto,Path.Combine(KSPUtil.ApplicationRootPath,"Ringworld-output-"+edge+".png"),true);
                Debug.Log("[RingworldSmoke] PHOTO output render "+width+"x"+height+" with Slow preset; file="+f.visuals.LastPhoto);
                f.visuals.EndPhoto();yield return null;
                if(Time.timeScale!=1||RingQualityPresets.Match(f.Settings)!="Slow"||InputLockManager.GetControlLock("NivenRingworld.Photo")!=ControlTypes.None){fail("Photo restoration failed");yield break;}
            }
            if(!f.visuals.BeginPhoto(1,6,512)){fail("Photo cancellation entry");yield break;}f.visuals.EndPhoto();yield return null;
            if(f.visuals.PhotoActive||Time.timeScale!=1){fail("Photo cancellation failed");yield break;}
            Debug.Log("[RingworldSmoke] PASS visual-options-only");
        }
        internal static void CheckWater(Shader shader,int[] qualities)
        {
            if(shader==null||!shader.isSupported)throw new Exception("Water variant unsupported");
            var cameraObject=new GameObject("Water transparency probe");var camera=cameraObject.AddComponent<Camera>();
            var water=GameObject.CreatePrimitive(PrimitiveType.Quad);var backing=GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material=new Material(shader);var backMaterial=new Material(Shader.Find("Unlit/Color"));
            var target=new RenderTexture(64,64,24);var pixels=new Texture2D(64,64,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                cameraObject.transform.position=new Vector3(0,0,-5);camera.orthographic=true;camera.orthographicSize=.4f;camera.targetTexture=target;
                if(shader.name=="NivenRingworld/WaterRefraction")Extensions.WaterScreenCopy.Attach(camera);
                water.layer=30;backing.layer=30;backing.transform.position=Vector3.forward;water.GetComponent<Renderer>().sharedMaterial=material;backing.GetComponent<Renderer>().sharedMaterial=backMaterial;
                material.SetVector("_WaveUp",Vector3.back);material.SetVector("_WaveAlong",Vector3.right);material.SetVector("_WaveAcross",Vector3.up);material.SetFloat("_WaterLight",1);
                var waves=new Vector4[12];for(int i=0;i<12;i++)waves[i]=new Vector4((float)Math.Cos(i*2.3)*.1f*(i+1),(float)Math.Sin(i*2.3)*.1f*(i+1),i*.7f,.12f/(i+1));
                material.SetVectorArray("_SeaWaves",waves);material.SetFloat("_SeaCount",12);material.SetVector("_WaveSun",new Vector3(.6f,0,-.8f));material.SetFloat("_SeaWind",.6f);
                foreach(int quality in qualities)
                {
                    material.SetFloat("_WaterQuality",quality);backMaterial.color=Color.red;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();var red=pixels.GetPixel(32,32);
                    backMaterial.color=Color.blue;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();var blue=pixels.GetPixel(32,32);
                    if(red.r-blue.r<.1f||blue.b-red.b<.1f)throw new Exception("Underlying surface hidden at quality "+quality+": "+red+" / "+blue);
                    File.WriteAllBytes(Path.Combine(KSPUtil.ApplicationRootPath,"Ringworld-water-probe-"+quality+".png"),pixels.EncodeToPNG());
                    Debug.Log("[RingworldSmoke] WATER transparency GPU probe quality="+quality+" red="+red+" blue="+blue);
                }
            }
            finally{water.SetActive(false);backing.SetActive(false);Extensions.WaterScreenCopy.Detach(camera);RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(water);UnityEngine.Object.Destroy(backing);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(backMaterial);UnityEngine.Object.Destroy(cameraObject);}
        }
    }
}
#endif
