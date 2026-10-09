#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class SurfaceLightingSmoke
    {
        internal static IEnumerator Run(RingworldFlight f,Action<string> fail)
        {
            float scale=Time.timeScale;Time.timeScale=0;
            try{
                var v=FlightGlobals.ActiveVessel;var camera=FlightCamera.fetch.mainCamera;
                var p=f.Position(v);var up=f.Settings.Geometry.Up(p);var heading=f.Settings.Geometry.AlongDirection(p);
                bool textureTest=Array.IndexOf(Environment.GetCommandLineArgs(),"-ringworld-terrain-textures")>=0;
                string folder=Path.Combine(KSPUtil.ApplicationRootPath,textureTest?"../Ring World KSP/artifacts/diagnostics/terrain-textures-20261008":"../Ring World KSP/artifacts/diagnostics/surface-lighting-20261008");Directory.CreateDirectory(folder);
                if(textureTest)TerrainTextureSmoke.CheckChunks();
                camera.transform.position=v.transform.position+ConvertVector.Unity(up*18-heading*30);
                camera.transform.rotation=Quaternion.LookRotation(v.transform.position-camera.transform.position,ConvertVector.Unity(up));
                float originalDistance=QualitySettings.shadowDistance;float originalSplit=QualitySettings.shadowCascade2Split;
                var resolution=QualitySettings.shadowResolution;int cascades=QualitySettings.shadowCascades;
                Debug.Log("[RingworldSmoke] SHADOW baseline distance="+originalDistance+" resolution="+resolution+" cascades="+cascades+" split="+originalSplit);
                RingStellarLighting.NativeShadowComparison=true;Capture(camera,Path.Combine(folder,"native-range.png"));RingStellarLighting.NativeShadowComparison=false;
                if(textureTest){
                    var restored=new System.Collections.Generic.Dictionary<Material,float>();
                    foreach(var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>()){
                        var m=renderer.sharedMaterial;if(m==null||!m.HasProperty("_SurfaceDetailEnabled")||restored.ContainsKey(m))continue;
                        restored[m]=m.GetFloat("_SurfaceDetailEnabled");m.SetFloat("_SurfaceDetailEnabled",0);
                    }
                    try{Capture(camera,Path.Combine(folder,"base-colour-only.png"));}finally{foreach(var pair in restored)pair.Key.SetFloat("_SurfaceDetailEnabled",pair.Value);}
                }
                Vector3 direction=Vector3.zero;float intensity=-1;bool observed=false;string error=null;
                Camera.CameraCallback inspect=c=>{
                    if(c!=camera)return;observed=true;
                    Light key=null;int count=0;foreach(var l in UnityEngine.Object.FindObjectsOfType<Light>())if(l.enabled&&l.type==LightType.Directional&&(l.cullingMask&(1<<15))!=0&&l.intensity>1e-6){key=l;count++;}
                    if(count!=1){error="Expected one shared key light; got "+count;return;}
                    if(intensity<0){intensity=key.intensity;direction=key.transform.forward;Debug.Log("[RingworldSmoke] SHADOW scoped distance="+QualitySettings.shadowDistance+" bias="+key.shadowBias+" normalBias="+key.shadowNormalBias+" intensity="+intensity);}
                    if(Math.Abs(key.intensity-intensity)>1e-5||(key.transform.forward-direction).sqrMagnitude>1e-10)error="Paused frame lighting changed between camera renders";
                    if(QualitySettings.shadowDistance>6000||QualitySettings.shadowResolution!=resolution||QualitySettings.shadowCascades!=cascades)error="Local shadow precision scope violated quality limits";
                };
                Camera.onPreRender+=inspect;
                try{
                    for(int i=0;i<30;i++){Capture(camera,i==0?Path.Combine(folder,"local-range.png"):null);yield return null;}
                }finally{Camera.onPreRender-=inspect;}
                if(!observed||error!=null){fail(error??"No ring lighting scope observed");yield break;}
                if(QualitySettings.shadowDistance!=originalDistance||QualitySettings.shadowCascade2Split!=originalSplit){fail("Shadow settings leaked outside camera render");yield break;}
                if(textureTest)TerrainTextureSmoke.CaptureExterior(f);
                var bridge=AccessTools.TypeByName("Ringworld.Parallax.BridgeSmoke");
                if(bridge!=null){
                    float deadline=Time.realtimeSinceStartup+100;bool tinted=false;
                    while(Time.realtimeSinceStartup<deadline){tinted=(bool)AccessTools.Method(bridge,"GrassTintReady").Invoke(null,null);if(tinted)break;yield return null;}
                    if(!tinted){fail("Terrain-coloured grass candidates never appeared");yield break;}
                    var chart=f.Settings.Geometry.Coordinates(p);double floor=f.Settings.Terrain.Sample(chart.Along,chart.Across).Height;
                    var ground=f.Settings.Geometry.Position(chart.Along,chart.Across,floor);
                    for(int i=0;i<60;i++){
                        camera.transform.position=(Vector3)(f.Center+ConvertVector.Ksp(ground+up*1.5-heading*20));
                        camera.transform.rotation=Quaternion.LookRotation(ConvertVector.Unity(heading-up*.1),ConvertVector.Unity(up));
                        camera.Render();yield return null;
                    }
                    camera.transform.position=(Vector3)(f.Center+ConvertVector.Ksp(ground+up*1.5-heading*20));
                    camera.transform.rotation=Quaternion.LookRotation(ConvertVector.Unity(heading-up*.1),ConvertVector.Unity(up));
                    Capture(camera,Path.Combine(folder,"terrain-tinted-grass.png"));
                    AccessTools.Method(bridge,"ValidateTint").Invoke(null,null);
                }
                // Exercise normal camera/light updates while crossing streamed
                // terrain, rather than only invoking Render on a paused frame.
                int movingViews=0;Vector3 previousDirection=direction;float previousIntensity=intensity;
                Camera.CameraCallback movingInspect=c=>{
                    if(c!=camera)return;Light key=null;int count=0;
                    foreach(var l in UnityEngine.Object.FindObjectsOfType<Light>())if(l.enabled&&l.type==LightType.Directional&&(l.cullingMask&(1<<15))!=0&&l.intensity>1e-6){key=l;count++;}
                    if(count!=1){error="Moving view changed shared key-light count";return;}
                    if(Math.Abs(key.intensity-previousIntensity)>.001f||(key.transform.forward-previousDirection).sqrMagnitude>.0001f)error="Moving view had a discontinuous stellar light change";
                    previousIntensity=key.intensity;previousDirection=key.transform.forward;movingViews++;
                };
                Time.timeScale=scale;v.SetWorldVelocity(ConvertVector.Ksp(heading*170));RingCollisionFrame.Reset(v);
                Camera.onPreRender+=movingInspect;
                try{for(int i=0;i<120;i++)yield return null;}finally{Camera.onPreRender-=movingInspect;}
                if(movingViews==0||error!=null){fail(error??"Moving lighting not observed");yield break;}
                Debug.Log("[RingworldSmoke] PASS moving shared lighting views="+movingViews+" parts="+v.parts.Count);
                Debug.Log("[RingworldSmoke] PASS stable shared lighting / restored shadow settings / terrain-tinted grass");
            }finally{RingStellarLighting.NativeShadowComparison=false;Time.timeScale=scale;}
        }
        private static void Capture(Camera camera,string path){
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);var old=camera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();if(path!=null)File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.Destroy(pixels);rt.Release();UnityEngine.Object.Destroy(rt);}
        }
    }
}
#endif
