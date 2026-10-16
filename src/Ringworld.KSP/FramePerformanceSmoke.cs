#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class FramePerformanceSmoke
    {
        internal static bool Enabled {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-frame-performance")>=0;}}
        internal static IEnumerator Run(RingworldFlight f)
        {
            CheatOptions.NoCrashDamage=true;CheatOptions.UnbreakableJoints=true;CheatOptions.IgnoreMaxTemperature=true;
            var o=f.Settings.Save();RingQualityPresets.Apply(o,6);
            o.SetValue("referenceBody","Sun",true);o.SetValue("anchorId","body:Sun",true);o.SetValue("designatedStar",false,true);
            o.SetValue("radius",15300000000.0,true);o.SetValue("width",160500000.0,true);o.SetValue("surfaceDensity",0,true);
            o.SetValue("lodRange",200000,true);o.SetValue("tileRadius",2,true);o.SetValue("parallaxExtension",true,true);o.SetValue("parallaxNativeFallback",false,true);
            o.SetValue("parallaxRange",450,true);o.SetValue("parallaxDensity",.35,true);o.SetValue("parallaxMaxObjects",12000,true);
            o.SetValue("atmosphereBackend",0,true);o.SetValue("cloudExtension",false,true);o.SetValue("waterExtension",false,true);o.SetValue("fullRingAtmosphere",true,true);
            f.ApplyOptions(o,true);
            var precision=Precision(f);while(precision.MoveNext())yield return precision.Current;
            if(Extensions.ExtensionProviders.Clouds==null||Extensions.ExtensionProviders.Scattering==null||Extensions.ExtensionProviders.Parallax==null)throw new Exception("Frame benchmark requires all three installed extensions.");
            var start=f.Settings.Terrain.Landmarks[0];double along=start.Along+20000,across=start.Across+12000;
            for(int i=0;i<128;i++){double a=start.Along+20000+i*2048,b=start.Across+12000+i*337;var t=f.Settings.Terrain.Sample(a,b);if(t.Biome==Biome.Grassland||t.Biome==Biome.Forest){along=a;across=b;break;}}
            f.arrivalHeight=12;AccessTools.Method(typeof(RingworldFlight),"VisitCoordinates").Invoke(f,new object[]{along,across});
            float end=Time.realtimeSinceStartup+100;while(!f.Ready&&Time.realtimeSinceStartup<end)yield return null;
            if(!f.Ready)throw new Exception("Frame benchmark arrival failed");
            precision=Precision(f);while(precision.MoveNext())yield return precision.Current;
            yield return new WaitForSecondsRealtime(35);
            var provider=Extensions.ExtensionProviders.Parallax;var layer=AccessTools.Field(provider.GetType(),"Current").GetValue(provider);
            var verify=layer==null?null:AccessTools.Method(layer.GetType(),"VerifyCulling");
            if(verify!=null)verify.Invoke(layer,new object[]{FlightCamera.fetch.mainCamera});
            yield return Measure("ground-basic-parallax",f,20);
            f.Settings.CloudExtension=true;f.Settings.CloudMode=1;
            ProbeClouds(f.Settings);
            ProbeCloudCost(f.Settings);
            yield return new WaitForSecondsRealtime(5);ProbeLiveCloudCost(f);yield return Measure("ground-volume-parallax",f,20);
            MapView.EnterMapView();yield return new WaitForSecondsRealtime(4);yield return Measure("map-all-extensions",f,15);MapView.ExitMapView();
            var ocean=f.Settings.Terrain.Landmarks.First(l=>l.Kind=="ocean");
            f.Settings.WaterExtension=true;f.Settings.WaterQuality=3;f.Settings.WaterScattering=true;
            AccessTools.Method(typeof(RingworldFlight),"VisitCoordinates").Invoke(f,new object[]{ocean.Along,ocean.Across});
            end=Time.realtimeSinceStartup+90;while(!f.Ready&&Time.realtimeSinceStartup<end)yield return null;
            yield return new WaitForSecondsRealtime(10);yield return Measure("water-volume-parallax",f,20);
            var camera=FlightCamera.fetch.mainCamera;var pose=camera.gameObject.AddComponent<FramePerformanceCamera>();
            pose.Position=(Vector3)(f.Center+ConvertVector.Ksp(f.Settings.Geometry.Position(ocean.Along,ocean.Across,-8)));
            pose.Rotation=Quaternion.LookRotation(ConvertVector.Unity(f.Settings.Geometry.AlongDirection(f.Settings.Geometry.Position(ocean.Along,ocean.Across,-8)))+ConvertVector.Unity(f.Settings.Geometry.Up(f.Settings.Geometry.Position(ocean.Along,ocean.Across,-8)))*.15f,ConvertVector.Unity(f.Settings.Geometry.Up(f.Settings.Geometry.Position(ocean.Along,ocean.Across,-8))));
            yield return new WaitForSecondsRealtime(3);yield return Measure("underwater-volume",f,10);UnityEngine.Object.Destroy(pose);
            VisualOptionsSmoke.CheckWater(Extensions.ExtensionProviders.Scattering.WaterShader(true),new[]{1,3});
            CheckWaterVisibility();
            UnityEngine.Debug.Log("[RingworldSmoke] PASS frame performance workload complete; all extension providers exercised");
        }
        private static IEnumerator Precision(RingworldFlight flight)
        {
            var n=flight.Settings.Save();n.SetValue("ringId","performance-precision-probe",true);n.SetValue("radius",150000000000.0,true);
            var settings=Settings.Load();settings.Apply(n);settings.Geometry.Basis=new RingBasis(23,42,17);
            var root=new GameObject("Performance precision probe");var mesh=new Mesh();var points=new DVec[128];
            for(int i=0;i<points.Length;i++)points[i]=new RingGeometry(settings.Geometry.P).Position(i*settings.Geometry.P.Circumference/points.Length,i%2==0?-settings.Geometry.P.Width/2:settings.Geometry.P.Width/2,30000)*ScaledSpace.InverseScaleFactor;
            mesh.vertices=new Vector3[points.Length];var assets=RingVisualAssets.Acquire();var material=new Material(assets.LoadAsset<Shader>("Assets/Shaders/GlobalClouds.shader"));
            var placement=new CameraRelativeRingMesh(root,mesh,material,points,settings);
            try{yield return null;yield return null;if(placement.PreparedFrames==0)throw new Exception("Inclined full-size precision mesh was not prepared");}
            finally{placement.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(material);RingVisualAssets.Release();}
        }
        private static void CheckWaterVisibility()
        {
            var obj=new GameObject("Water visibility regression camera");var camera=obj.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<15;camera.farClipPlane=100;camera.orthographic=true;camera.orthographicSize=4;
            obj.transform.position=new Vector3(1000000,2000000,3000000);
            var water=GameObject.CreatePrimitive(PrimitiveType.Quad);water.name="Water visibility regression mesh";water.layer=15;var renderer=water.GetComponent<Renderer>();
            var type=Extensions.ExtensionProviders.Scattering.GetType().Assembly.GetType("NivenRingworld.Extensions.ScatteringScreenCopy");
            var prepare=AccessTools.Method(type,"Prepare");var detach=AccessTools.Method(type,"Detach");RingWaterVisibility.Add(renderer);
            try{
                water.transform.position=obj.transform.position+Vector3.forward*10;
                if(!RingWaterVisibility.Visible(camera))throw new Exception("Visible water was culled");prepare.Invoke(null,new object[]{camera});
                if(camera.GetCommandBuffers(UnityEngine.Rendering.CameraEvent.BeforeForwardAlpha).Length!=1)throw new Exception("Visible water did not attach a background copy");
                water.transform.position=obj.transform.position-Vector3.forward*10;
                if(RingWaterVisibility.Visible(camera))throw new Exception("Off-frustum water retained");prepare.Invoke(null,new object[]{camera});
                if(camera.GetCommandBuffers(UnityEngine.Rendering.CameraEvent.BeforeForwardAlpha).Length!=0)throw new Exception("Off-frustum water retained its copy");
                water.transform.position=obj.transform.position+Vector3.forward*10;renderer.enabled=false;
                if(RingWaterVisibility.Visible(camera))throw new Exception("Disabled water retained");renderer.enabled=true;water.SetActive(false);
                if(RingWaterVisibility.Visible(camera))throw new Exception("Staged water retained");
                Debug.Log("[RingworldSmoke] PASS water visibility/copy attachment visible/off-frustum/disabled/staged");
            }finally{detach.Invoke(null,new object[]{camera});RingWaterVisibility.Remove(renderer);UnityEngine.Object.Destroy(water);UnityEngine.Object.Destroy(obj);}
        }
        private static void ProbeCloudCost(Settings settings)
        {
            var assets=Extensions.ExtensionProviders.Clouds.Assets;var referenceShader=assets.LoadAsset<Shader>("Assets/Shaders/CloudPerformanceReference.shader");if(referenceShader==null)return;
            var current=new Material(Extensions.ExtensionProviders.Clouds.AtmosphereShader);var reference=new Material(referenceShader);
            var target=new RenderTexture(320,180,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();var pixel=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;var far=FarDepth();
            try{
                foreach(var material in new[]{current,reference})
                {
                    var controller=Extensions.ExtensionProviders.Clouds.Create(material);RingCloudField.Apply(material,settings,0,0,0,12345);controller.Update(settings,new WeatherSample{Cloud=.8,Rain=0,Storm=0},12345);
                    material.SetVector("_Habitat",new Vector4(1000,(float)settings.Geometry.P.Radius,0,(float)(settings.Geometry.P.Width*.5)));material.SetVector("_WeatherMap",new Vector4(0,0,180000,1));material.SetFloat("_CloudAmount",.8f);
                    material.SetVector("_Quality",new Vector4(32,16,4,500000));material.SetVector("_Look",Vector4.one);material.SetVector("_Photo",new Vector4(1,-1,0,0));material.SetVector("_FrameSize",new Vector2(320,180));
                    material.SetVector("_RayForward",new Vector3(0,1,.2f));material.SetVector("_RayRight",new Vector3(.8f,0,0));material.SetVector("_RayUp",new Vector3(0,0,.5f));material.SetVector("_Sun",Vector3.forward);
                    material.SetVector("_CloudHandoff",new Vector4(400000,500000,0,0));material.SetTexture("_SavedDepth",far);
                    Graphics.Blit(Texture2D.blackTexture,target,material,0);RenderTexture.active=target;pixel.ReadPixels(new Rect(0,0,1,1),0,0);
                }
                foreach(float amount in new[]{.8f,1f})
                foreach(var sun in new[]{Vector3.forward,new Vector3(.25f,.1f,.963f).normalized})
                {
                    current.SetFloat("_CloudAmount",amount);reference.SetFloat("_CloudAmount",amount);
                    current.SetVector("_Sun",sun);reference.SetVector("_Sun",sun);
                    double before=CloudCost(reference,target,pixel),after=CloudCost(current,target,pixel);
                    UnityEngine.Debug.Log("[RingworldSmoke] CLOUD_GPU_COST referenceMs="+before+" optimizedMs="+after+" amount="+amount+" sun="+sun+" size=320x180 steps=32/16/4 repeats=6 far-depth photo path; every draw synchronized by readback");
                }
            }finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixel);UnityEngine.Object.Destroy(current);UnityEngine.Object.Destroy(reference);UnityEngine.Object.Destroy(far);}
        }
        private static double CloudCost(Material material,RenderTexture target,Texture2D pixel)
        {
            // Synchronize every draw so a tile GPU cannot discard five full
            // overwrites of the same target before the final readback.
            var clock=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<6;i++){
                Graphics.Blit(Texture2D.blackTexture,target,material,0);RenderTexture.active=target;pixel.ReadPixels(new Rect(0,0,1,1),0,0);
            }return clock.Elapsed.TotalMilliseconds/6;
        }
        private static Texture2D FarDepth(){var t=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);t.SetPixel(0,0,new Color(1000000,0,0,0));t.Apply(false);return t;}
        private static void ProbeLiveCloudCost(RingworldFlight flight)
        {
            var live=(Material)AccessTools.Field(typeof(RingVisualRenderer),"material").GetValue(flight.visuals);
            var current=new Material(live);var reference=new Material(live){shader=Extensions.ExtensionProviders.Clouds.Assets.LoadAsset<Shader>("Assets/Shaders/CloudPerformanceReference.shader")};
            var target=new RenderTexture(160,90,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();var previous=RenderTexture.active;
            var pixels=new Texture2D(160,90,TextureFormat.RGBAFloat,false,true);var far=FarDepth();
            try{
                foreach(var m in new[]{current,reference}){
                    // The shader exposes most uniforms outside ShaderLab's
                    // Properties block. Material cloning does not reliably copy
                    // those uniforms or arrays; copy the live inputs explicitly.
                    foreach(var name in new[]{"_CoverageOrigin","_CoverageEvolution","_CoverageSeed","_CloudOrigin","_CloudControls","_CloudLimits","_CloudMarchLimits","_Habitat","_WeatherMap","_Look","_Quality","_RayRight","_RayUp","_RayForward","_CloudHandoff","_SurfaceWeather"})m.SetVector(name,live.GetVector(name));
                    foreach(var name in new[]{"_CoveragePeriod","_CoverageScaleX","_CloudAmount","_Lightning","_CloudRise","_CloudProbe","_PrecipitationBase","_Jitter"})m.SetFloat(name,live.GetFloat(name));
                    foreach(var name in new[]{"_CloudBands","_CloudShapes","_CloudOptics"})m.SetVectorArray(name,live.GetVectorArray(name));
                    m.SetTexture("_CloudShape",live.GetTexture("_CloudShape"));
                    m.SetVector("_FrameSize",new Vector2(160,90));m.SetVector("_Photo",new Vector4(1,-1,0,0));m.SetTexture("_SavedDepth",far);
                }
                if(current.GetVectorArray("_CloudBands").Length!=10||current.GetVector("_CloudControls").x<.5f)throw new Exception("Live cloud probe lost its configuration: source="+live.GetVector("_CloudControls")+" clone="+current.GetVector("_CloudControls"));
                Graphics.Blit(Texture2D.blackTexture,target,reference,0);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,160,90),0,0);var expected=pixels.GetPixels();
                Graphics.Blit(Texture2D.blackTexture,target,current,0);pixels.ReadPixels(new Rect(0,0,160,90),0,0);var actual=pixels.GetPixels();float maximum=0,maxAlpha=0;
                foreach(var color in actual)maxAlpha=Math.Max(maxAlpha,color.a);
                for(int i=0;i<actual.Length;i++)for(int c=0;c<4;c++){float error=Math.Abs(actual[i][c]-expected[i][c]);if(float.IsNaN(error)||float.IsInfinity(error))throw new Exception("Nonfinite cloud integration probe");maximum=Math.Max(maximum,error);}
                Debug.Log("[RingworldSmoke] CLOUD_LIVE_REFERENCE pixels="+actual.Length+" maxDifference="+maximum+" maxAlpha="+maxAlpha+" sun="+live.GetVector("_Sun")+" quality="+live.GetVector("_Quality")+" controls="+live.GetVector("_CloudControls"));
                if(maxAlpha<.001f)throw new Exception("Cloud integration reference was effectively empty");
                if(maximum>1f/1024)throw new Exception("Cloud integration changed versus reference: "+maximum);
                double before=CloudCost(reference,target,pixels),after=CloudCost(current,target,pixels);
                Debug.Log("[RingworldSmoke] CLOUD_LIVE_COST referenceMs="+before+" optimizedMs="+after+" size=160x90 repeats=6 camera rays/far-depth photo path; every draw synchronized by readback");
                Debug.Log("[RingworldSmoke] PASS live cloud integration reference");
            }finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(current);UnityEngine.Object.Destroy(reference);UnityEngine.Object.Destroy(far);}
        }
        private static void ProbeClouds(Settings settings)
        {
            ProbeCloudArithmetic(settings);
            var shader=Extensions.ExtensionProviders.Clouds.Assets.LoadAsset<Shader>("Assets/Shaders/CloudDensityProbe.shader");
            if(shader==null)return; // The pre-change baseline bundle has no density probe.
            if(!shader.isSupported)throw new Exception("Cloud density regression shader unsupported");
            var material=new Material(shader);var controller=Extensions.ExtensionProviders.Clouds.Create(material);
            var target=new RenderTexture(32,32,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();
            var pixels=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
            int mode=settings.CloudMode,checkedPixels=0,nonzero=0;float maximum=0,maximumLight=0;
            // Compiler reassociation can propagate small interpolation/optical
            // rounding differences into linear RGB. Keep a
            // sub-8-bit tolerance, and report the actual maximum over every case.
            const float lightTolerance=1f/4096;
            try{
                RingCloudField.Apply(material,settings,0,0,0,12345);material.SetVector("_Habitat",new Vector4(0,(float)settings.Geometry.P.Radius,0,(float)(settings.Geometry.P.Width*.5)));
                material.SetVector("_Quality",new Vector4(32,16,4,500000));
                material.SetVector("_WeatherMap",new Vector4(0,0,180000,1));material.SetFloat("_CloudAmount",.8f);material.SetVector("_Look",Vector4.one);material.SetVector("_CloudHandoff",new Vector4(200000,300000,0,0));
                foreach(var sun in new[]{Vector3.forward,new Vector3(.25f,.1f,.963f).normalized})foreach(int quality in new[]{1,2,3})foreach(float rain in new[]{0f,.25f,1f})foreach(float storm in new[]{0f,.4f,1f})foreach(float altitude in new[]{800f,1700f,3500f,5300f,8500f,11500f})
                {
                    material.SetVector("_Sun",sun);
                    settings.CloudMode=quality;controller.Update(settings,new WeatherSample{Cloud=.8,Rain=rain,Storm=storm},12345);
                    material.SetFloat("_ProbeAltitude",altitude);Graphics.Blit(Texture2D.blackTexture,target,material);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply(false);
                    var march=material.GetVector("_CloudMarchLimits");var values=pixels.GetPixels();
                    for(int i=0;i<values.Length;i++)
                    {
                        var value=values[i];maximum=Math.Max(maximum,Math.Abs(value.r-value.g));maximumLight=Math.Max(maximumLight,value.b);checkedPixels++;if(value.r>1e-5)nonzero++;
                        if(float.IsNaN(value.r)||float.IsInfinity(value.r)||float.IsNaN(value.g)||float.IsInfinity(value.g)||Math.Abs(value.r-value.g)>1e-5||float.IsNaN(value.b)||float.IsInfinity(value.b))throw new Exception("Cloud density/light changed versus reference: "+quality+"/"+rain+"/"+storm+" difference="+Math.Abs(value.r-value.g)+" light="+value.b);
                        double x=((i%32+.5)/32-.5)*180000,h=altitude-x*x/(2*settings.Geometry.P.Radius);
                        if((h<march.x-.1||h>march.y+.1)&&value.r>1e-5)throw new Exception("Cloud march limits excluded a visible cloud type");
                    }
                }
                if(nonzero==0)throw new Exception("Cloud density probe did not sample any clouds");
                UnityEngine.Debug.Log("[RingworldSmoke] Cloud GPU reference/march bounds samples="+checkedPixels+" nonzero="+nonzero+" maxDifference="+maximum+" maxLightDifference="+maximumLight+" lightTolerance="+lightTolerance);
                if(maximumLight>lightTolerance)throw new Exception("Cloud illumination exceeded reference tolerance: "+maximumLight);
                UnityEngine.Debug.Log("[RingworldSmoke] PASS cloud GPU density/light reference and march bounds");
            }finally{settings.CloudMode=mode;RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(material);}
        }
        private static void ProbeCloudArithmetic(Settings settings)
        {
            var shader=Extensions.ExtensionProviders.Clouds.Assets.LoadAsset<Shader>("Assets/Shaders/CloudArithmeticProbe.shader");
            if(shader==null||!shader.isSupported)throw new Exception("Cloud arithmetic regression shader missing/unsupported");
            var material=new Material(shader);RingCloudField.Apply(material,settings,0,0,0,12345);
            var target=new RenderTexture(64,32,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);target.Create();
            var pixels=new Texture2D(64,32,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;int samples=0;
            try{
                foreach(float amount in new[]{0f,.5f,.65f,.8f,.81f,1f})
                foreach(int period in new[]{1,17,24033,235619})foreach(int frequency in new[]{1,2,4,8,16,32,64,128})
                {
                    material.SetFloat("_CloudAmount",amount);material.SetFloat("_CoveragePeriod",period);material.SetFloat("_ProbeFrequency",frequency);Graphics.Blit(Texture2D.blackTexture,target,material);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,32),0,0);
                    foreach(var p in pixels.GetPixels()){if(p.r!=p.g||float.IsNaN(p.r)||float.IsNaN(p.b)||Math.Abs(p.b-p.a)>0.000001f)throw new Exception("Cloud noise/coverage changed at amount="+amount+" period="+period+" frequency="+frequency);samples++;}
                }
                int checks=0;unchecked{uint state=1970;foreach(int period in new[]{1,2,17,24033,235619,30159232})for(int i=0;i<131072;i++){
                    state=state*1664525+1013904223;int c=i==0?int.MinValue:i==1?int.MaxValue:(int)state;uint magnitude=c<0?0u-(uint)c:(uint)c,remainder=magnitude%(uint)period;
                    uint wrapped=c<0&&remainder!=0?(uint)period-remainder:remainder;long reference=((long)c%period+period)%period;
                    int mask=1048576*(1<<(i%8))-1;long across=((long)c%(mask+1)+(mask+1))%(mask+1);
                    if(wrapped!=reference||(c&mask)!=across)throw new Exception("Cloud wrapping integer boundary regression");checks++;
                }}
                Debug.Log("[RingworldSmoke] PASS cloud noise arithmetic GPU samples="+samples+" integer boundary pairs="+checks);
            }finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(material);}
        }
        private static IEnumerator Measure(string label,RingworldFlight f,float seconds)
        {
            var frames=new List<float>();int collections=GC.CollectionCount(0),visuals=f.visuals.RenderedFrames;long memory=GC.GetTotalMemory(false);
            float start=Time.realtimeSinceStartup,end=start+seconds;
            while(Time.realtimeSinceStartup<end){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
            double elapsed=Time.realtimeSinceStartup-start;frames.Sort();
            var volume=(RenderTexture)AccessTools.Field(typeof(RingVisualRenderer),"volume").GetValue(f.visuals);
            UnityEngine.Debug.Log("[RingworldSmoke] FRAME_TIMING "+label+" frames="+frames.Count+" seconds="+elapsed+" fps="+(frames.Count/elapsed)+" medianMs="+frames[frames.Count/2]+" p95Ms="+frames[(int)(frames.Count*.95)]+" gc0="+(GC.CollectionCount(0)-collections)+" heapDelta="+(GC.GetTotalMemory(false)-memory)+" near="+f.NearTileCount+" lod="+f.LodCount+" resolution="+Screen.width+"x"+Screen.height+" volumeTarget="+(volume==null?"none":volume.width+"x"+volume.height)+" atmosphereSubmissions="+(f.visuals.RenderedFrames-visuals)+" parallax="+Extensions.ExtensionProviders.Parallax.Status);
        }
    }
    [DefaultExecutionOrder(31000)]
    public sealed class FramePerformanceCamera : MonoBehaviour
    {
        internal Vector3 Position;internal Quaternion Rotation;
        public void LateUpdate(){transform.SetPositionAndRotation(Position,Rotation);var f=RingworldFlight.Instance;f.visuals?.Prepare(true,f.Center);Extensions.ExtensionProviders.Scattering.Underwater(true);}
    }
}
#endif
