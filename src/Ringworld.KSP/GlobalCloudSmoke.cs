#if RINGWORLD_SMOKE_TEST
using System;
using System.IO;
using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld
{
    internal static class GlobalCloudSmoke
    {
        internal static void Run()
        {
            var s=Settings.Load();s.CloudAmount=.8;s.DynamicWeather=false;s.FullRingDetail=false;
            var parent=new GameObject("Global cloud validation");var bundle=RingVisualAssets.Acquire();
            WeatherParticleSmoke.Run(bundle);
            VisualOptionsSmoke.CheckWater(bundle.LoadAsset<Shader>("Assets/Shaders/RingWater.shader"),new[]{0,1,2});
            VisualOptionsSmoke.CheckWater(bundle.LoadAsset<Shader>("Assets/Shaders/RingWaterRefraction.shader"),new[]{3,4});
            CloudVolumeSmoke.Run(Settings.Load(),bundle);
            CheckCoverage(s,bundle);
            CheckDistantSurface(s,bundle);
            var clouds=new GlobalClouds(parent.transform,s,bundle);clouds.Update(s,null,null,0);
            var obj=GameObject.Find("Ringworld global cloud shell");if(obj==null)throw new Exception("Global cloud renderer missing");
            var mesh=obj.GetComponent<MeshFilter>().sharedMesh;var material=obj.GetComponent<MeshRenderer>().sharedMaterial;
            if(mesh.vertexCount!=65536||!material.shader.isSupported)throw new Exception("Invalid cloud shell mesh/shader");
            var field=mesh.uv2;var macro=mesh.uv4;
            if(macro.Length!=mesh.vertexCount)throw new Exception("Macro cloud chart missing");
            for(int i=0;i<16384;i++)
            {
                double cells=Math.Round(s.Geometry.P.Circumference/RingCloudField.CoverageScale);
                double difference=field[i*4+2].x+macro[i*4+2].x-field[((i+1)%16384)*4].x-macro[((i+1)%16384)*4].x;
                float error=(float)(difference-Math.Round(difference/cells)*cells);
                float macroError=Mathf.Repeat(macro[i*4+2].x-macro[((i+1)%16384)*4].x+.5f,1)-.5f;
                if(Mathf.Abs(macroError)>.00002f)throw new Exception("Macro cloud seam at "+i);
                if(Mathf.Abs(error)>.00002f)throw new Exception("Cloud field seam at segment "+i+": "+error);
            }
            var cameraObj=new GameObject("Cloud validation camera");var camera=cameraObj.AddComponent<Camera>();camera.enabled=false;
            double along=s.Geometry.P.Circumference*.0123;
            cameraObj.transform.position=ConvertVector.Unity(s.Geometry.Position(along,0,1000000)*ScaledSpace.InverseScaleFactor);
            cameraObj.transform.LookAt(ConvertVector.Unity(s.Geometry.Position(along,0,5500)*ScaledSpace.InverseScaleFactor),Vector3.up);
            camera.orthographic=true;camera.orthographicSize=(float)(180000*ScaledSpace.InverseScaleFactor);camera.nearClipPlane=.01f;camera.farClipPlane=1000;camera.cullingMask=1<<10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(900,600,24);camera.targetTexture=target;
            string output=Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath,"../artifacts/validation/global-clouds"));Directory.CreateDirectory(output);
            float dayPhase=(float)RingGeometry.Wrap(20*.0123-.5,1);material.SetFloat("_DayPhase",dayPhase);
            double day=Capture(camera,target,Path.Combine(output,"day.png"));
            material.SetFloat("_DayPhase",(float)(20*.0123));double night=Capture(camera,target,Path.Combine(output,"night.png"));
            material.SetFloat("_DayPhase",dayPhase);material.SetVector("_Local",new Vector4(.0123f,.5f,5500,1));material.SetFloat("_LocalAmount",(float)(.5+(s.CloudAmount-.5)*.1));material.SetVector("_CloudHandoff",RingCloudField.Handoff(s,false));
            double span=s.Geometry.P.Circumference/16384,sector=Math.Floor(along/span);material.SetFloat("_SegmentLength",(float)span);material.SetVector("_LocalChart",new Vector4((float)sector,(float)(along-sector*span),0,5500));
            double transition=Capture(camera,target,Path.Combine(output,"handoff.png"));
            if(day<.01||night>day*.4||transition>=day*.98)throw new Exception("Cloud shading/handoff failed: "+day+" / "+night+" / "+transition);
            material.SetVector("_Local",Vector4.zero);
            cameraObj.transform.position=ConvertVector.Unity(s.Geometry.Position(along,0,1000)*ScaledSpace.InverseScaleFactor);
            cameraObj.transform.LookAt(ConvertVector.Unity(s.Geometry.Position(along,0,5500)*ScaledSpace.InverseScaleFactor),Vector3.up);
            double underside=Capture(camera,target,Path.Combine(output,"underside.png"));
            if(underside<.01)throw new Exception("Cloud sheet invisible from below: "+underside);
            Debug.Log("[RingworldSmoke] CLOUD UNDERSIDE luminance="+underside);
            cameraObj.transform.position=ConvertVector.Unity(s.Geometry.Position(along,0,-1000000)*ScaledSpace.InverseScaleFactor);
            cameraObj.transform.LookAt(ConvertVector.Unity(s.Geometry.Position(along,0,5500)*ScaledSpace.InverseScaleFactor),Vector3.up);
            camera.farClipPlane=(float)(s.Geometry.P.Radius*3*ScaledSpace.InverseScaleFactor);
            double exterior=Capture(camera,target,Path.Combine(output,"exterior.png"));
            if(exterior>.0001)throw new Exception("Clouds visible through outer hull: "+exterior);
            Debug.Log("[RingworldSmoke] CLOUD EXTERIOR luminance="+exterior);
            s.CloudAmount=0;clouds.Update(s,null,null,0);if(obj.activeSelf)throw new Exception("Cloud off setting ignored");
            Debug.Log("[RingworldSmoke] PASS global-clouds-only: 16384 continuous UV segments; day="+day+" night="+night+" handoff="+transition+"; low-detail and off settings OK");
            var atmosphere=new Extensions.RingworldScattering(parent.transform,s,bundle);
            s.FullRingAtmosphere=true;s.Atmosphere=true;s.Haze=1;atmosphere.Update(s,0);
            var airObject=GameObject.Find("Ringworld full-ring atmosphere");
            if(airObject==null)throw new Exception("Full-ring atmosphere missing");
            var air=airObject.GetComponent<MeshRenderer>().sharedMaterial;
            air.SetFloat("_DayPhase",dayPhase);air.SetFloat("_LocalBlend",0);
            cameraObj.transform.position=ConvertVector.Unity(s.Geometry.Position(along,0,1000000)*ScaledSpace.InverseScaleFactor);
            cameraObj.transform.LookAt(ConvertVector.Unity(s.Geometry.Position(along,0,30000)*ScaledSpace.InverseScaleFactor),Vector3.up);
            double airDay=Capture(camera,target,Path.Combine(output,"atmosphere-day.png"));
            air.SetFloat("_DayPhase",(float)(20*.0123));
            double airNight=Capture(camera,target,Path.Combine(output,"atmosphere-night.png"));
            if(airDay<.005||airNight>airDay*.1)throw new Exception("Full-ring atmospheric day/night failed: "+airDay+" / "+airNight);
            air.SetFloat("_DayPhase",dayPhase);air.SetFloat("_LocalBlend",1);
            cameraObj.transform.position=ConvertVector.Unity(s.Geometry.Position(along,0,1000)*ScaledSpace.InverseScaleFactor);
            cameraObj.transform.LookAt(ConvertVector.Unity(s.Geometry.Position(along,0,30000)*ScaledSpace.InverseScaleFactor),Vector3.up);
            camera.orthographicSize=1;camera.farClipPlane=100; // Isolate the nearby handoff; the opposite ring remains legitimately visible.
            double airLocal=Capture(camera,target,Path.Combine(output,"atmosphere-local-handoff.png"));
            if(airLocal>.001)throw new Exception("Full-ring atmosphere overlaps nearby sky: "+airLocal);
            camera.farClipPlane=(float)(s.Geometry.P.Radius*3*ScaledSpace.InverseScaleFactor);
            cameraObj.transform.LookAt(ConvertVector.Unity(s.Geometry.Position(along+10000000,0,30000)*ScaledSpace.InverseScaleFactor),Vector3.up);
            double airDistant=Capture(camera,target,Path.Combine(output,"atmosphere-landed-distant.png"));
            if(airDistant<.005)throw new Exception("Distant atmosphere invisible while landed");
            cameraObj.transform.position=ConvertVector.Unity(s.Geometry.Position(along,0,-1000000)*ScaledSpace.InverseScaleFactor);
            cameraObj.transform.LookAt(ConvertVector.Unity(s.Geometry.Position(along,0,30000)*ScaledSpace.InverseScaleFactor),Vector3.up);
            double airExterior=Capture(camera,target,Path.Combine(output,"atmosphere-exterior.png"));
            if(airExterior>.001)throw new Exception("Atmosphere leaked through hull");
            air.SetFloat("_LocalBlend",0);
            float scaledRadius=(float)(s.Geometry.P.Radius*ScaledSpace.InverseScaleFactor);
            cameraObj.transform.position=new Vector3(scaledRadius*2,scaledRadius,scaledRadius);
            cameraObj.transform.LookAt(Vector3.zero,Vector3.up);camera.orthographicSize=scaledRadius*1.1f;
            Capture(camera,target,Path.Combine(output,"atmosphere-full-ring.png"));
            s.FullRingAtmosphere=false;atmosphere.Update(s,0);
            if(airObject.activeSelf)throw new Exception("Atmosphere switch ignored");
            var saved=Settings.Load();saved.Apply(s.Save());
            if(saved.FullRingAtmosphere)throw new Exception("Atmosphere switch not saved");
            Debug.Log("[RingworldSmoke] FULL RING ATMOSPHERE day="+airDay+" night="+airNight+" local="+airLocal+" landedDistant="+airDistant+" exterior="+airExterior+"; off/roundtrip passed");
            atmosphere.Dispose();
            camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(cameraObj);clouds.Dispose();UnityEngine.Object.Destroy(parent);RingVisualAssets.Release();
        }
        private static void CheckCoverage(Settings s,AssetBundle bundle)
        {
            var shader=bundle.LoadAsset<Shader>("Assets/Shaders/CloudCoverageProbe.shader");
            if(shader==null||!shader.isSupported)throw new Exception("Cloud fBm probe unavailable");
            var mat=new Material(shader);RingCloudField.Apply(mat,s,0,0,0,0);mat.SetFloat("_CloudAmount",.5f);mat.SetVector("_CoverageOrigin",new Vector4(8000,12,.37f,.23f));
            var target=new RenderTexture(1024,1024,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Func<float,Color32[]> sample=offset=>
            {
                mat.SetFloat("_ProbeOffset",offset);Graphics.Blit(null,target,mat);
                var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1024,1024,TextureFormat.RGBA32,false,true);
                image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();RenderTexture.active=old;var colors=image.GetPixels32();
                if(offset==0){string folder=Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath,"../artifacts/validation/global-clouds"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"fbm-coverage.png"),image.EncodeToPNG());}
                UnityEngine.Object.Destroy(image);return colors;
            };
            var original=sample(0);
            var origin=mat.GetVector("_CoverageOrigin");mat.SetVector("_CoverageOrigin",origin+new Vector4(-.002f,-.002f,0,0));
            var rounded=sample(0);mat.SetVector("_CoverageOrigin",origin);
            for(int i=0;i<original.Length;i++)if(original[i].r!=rounded[i].r)throw new Exception("Interpolated integer cloud cell changed the pattern");
            int covered=0;foreach(var color in original)if(color.r>127)covered++;
            double coverage=(double)covered/original.Length;
            if(coverage<.45||coverage>.55)throw new Exception("Distant cloud coverage not about 50%: "+coverage);
            foreach(float offset in new[]{512000f,32768000f})
            {
                var shifted=sample(offset);double difference=0;for(int i=0;i<original.Length;i++)difference+=Math.Abs(original[i].r-shifted[i].r)/(255.0*original.Length);
                if(difference<.03)throw new Exception("Clouds repeat at old texture period "+offset);
                Debug.Log("[RingworldSmoke] CLOUD FBM offset="+offset+" meanDifference="+difference);
            }
            Debug.Log("[RingworldSmoke] CLOUD FBM coverage="+coverage);
            mat.SetVector("_CoverageEvolution",new Vector4(.2f,-.15f,0,0));
            var evolved=sample(0);double evolutionDifference=0;
            for(int i=0;i<original.Length;i++)evolutionDifference+=Math.Abs(original[i].r-evolved[i].r)/(255.0*original.Length);
            if(evolutionDifference<.005)throw new Exception("Cloud evolution has no visible effect");
            Debug.Log("[RingworldSmoke] CLOUD EVOLUTION meanDifference="+evolutionDifference);
            RenderTexture.active=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(mat);
        }
        private static void CheckDistantSurface(Settings s,AssetBundle bundle)
        {
            var shader=bundle.LoadAsset<Shader>("Assets/Shaders/DistantSurface.shader");
            var transition=bundle.LoadAsset<Shader>("Assets/Shaders/TerrainTransition.shader");
            if(shader==null||!shader.isSupported||transition==null||!transition.isSupported)throw new Exception("Terrain shader unsupported");
            var mat=new Material(shader);var target=new RenderTexture(1024,256,0);
            var previous=RenderTexture.active;
            mat.SetFloat("_CircumferenceKm",(float)(s.Geometry.P.Circumference/1000));mat.SetFloat("_WidthKm",(float)(s.Geometry.P.Width/1000));
            uint seed=unchecked((uint)s.Geometry.P.Seed);mat.SetFloat("_SeedLow",seed&65535);mat.SetFloat("_SeedHigh",seed>>16);mat.SetFloat("_Generation",s.GenerationVersion);
            try
            {
                for(int detail=0;detail<=1;detail++)
                {
                    mat.SetFloat("_Detail",detail);Graphics.Blit(null,target,mat);
                    RenderTexture.active=target;var pixels=new Texture2D(1024,256,TextureFormat.RGB24,false);
                    try
                    {
                        pixels.ReadPixels(new Rect(0,0,1024,256),0,0);pixels.Apply();
                        int ocean=0,land=0;double minRatio=10,maxRatio=0;
                        foreach(var c in pixels.GetPixels32())
                        {
                            if(c.b>c.r*1.5&&c.b>c.g)ocean++;
                            if(c.g>c.b*1.2){land++;if(c.g>60){double ratio=(double)c.r/c.g;minRatio=Math.Min(minRatio,ratio);maxRatio=Math.Max(maxRatio,ratio);}}
                        }
                        if(ocean<100||land<1000)throw new Exception("Coarse terrain lost land/ocean at detail "+detail+": "+land+" / "+ocean);
                        if(maxRatio-minRatio<.05)throw new Exception("Full-ring overview collapsed to flat land colour");
                        string folder=Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath,"../artifacts/validation/global-clouds"));Directory.CreateDirectory(folder);
                        File.WriteAllBytes(Path.Combine(folder,"terrain-detail-"+detail+".png"),pixels.EncodeToPNG());
                        Debug.Log("[RingworldSmoke] GLOBAL TERRAIN detail="+detail+" landPixels="+land+" oceanPixels="+ocean);
                    }
                    finally{UnityEngine.Object.Destroy(pixels);}
                }
            }
            finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(mat);}
        }
        private static double Capture(Camera camera,RenderTexture target,string path)
        {
            camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();RenderTexture.active=old;
            double sum=0;foreach(var color in pixels.GetPixels32())sum+=(color.r+color.g+color.b)/(255.0*3*target.width*target.height);
            File.WriteAllBytes(path,pixels.EncodeToPNG());UnityEngine.Object.Destroy(pixels);return sum;
        }
    }
}
#endif
