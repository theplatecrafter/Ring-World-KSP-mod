#if RINGWORLD_SMOKE_TEST
using System;
using System.IO;
using UnityEngine;
namespace NivenRingworld
{
    internal static class CloudVolumeSmoke
    {
        private static void CheckDeck(Settings settings,AssetBundle bundle)
        {
            var mat=new Material(bundle.LoadAsset<Shader>("Assets/Shaders/CloudDeck.shader"));
            var target=new RenderTexture(128,128,0,RenderTextureFormat.ARGBHalf);
            var old=RenderTexture.active;Texture2D image=null;
            try
            {
                RingCloudField.Apply(mat,settings,1234567,0,0,1234);
                mat.SetFloat("_CloudAmount",1);mat.SetFloat("_Extent",180000);mat.SetFloat("_Daylight",1);
                mat.SetVector("_CloudHandoff",new Vector4(1000000,2000000,0,0));
                RenderTexture.active=target;GL.Clear(true,true,Color.clear);Graphics.Blit(null,target,mat);
                image=new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                if(image.GetPixel(64,64).a<.1f)throw new Exception("Rainy lightweight deck has an overhead hole");
                if(image.GetPixel(2,64).a>.001f||image.GetPixel(2,2).a>.001f)throw new Exception("Lightweight cloud patch has a hard square boundary");
                var png=new Texture2D(128,128,TextureFormat.RGBA32,false,true);png.SetPixels(image.GetPixels());png.Apply();
                string dir=Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath,"../artifacts/validation/cloud-extension"));Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir,"lightweight-deck.png"),png.EncodeToPNG());UnityEngine.Object.Destroy(png);
                Debug.Log("[RingworldSmoke] CLOUD DECK rainy overhead coverage and transparent patch boundary passed");
            }
            finally{RenderTexture.active=old;if(image!=null)UnityEngine.Object.Destroy(image);target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(mat);}
        }
        internal static void Run(Settings settings,AssetBundle bundle)
        {
            CheckDeck(settings,bundle);
            var shader=bundle.LoadAsset<Shader>("Assets/Shaders/RingAtmosphere.shader");
            if(shader==null||!shader.isSupported)throw new Exception("Cloud extension shader unsupported");
            var mat=new Material(shader);var extension=new Extensions.RingworldClouds(mat,bundle);
            var depth=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);depth.SetPixel(0,0,new Color(200000,0,0,0));depth.Apply();
            var target=new RenderTexture(160,96,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            var previous=RenderTexture.active;
            string folder=Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath,"../artifacts/validation/cloud-extension"));Directory.CreateDirectory(folder);
            try
            {
                settings.CloudExtension=true;settings.CloudDensity=1;settings.CloudAmount=.9;settings.DynamicWeather=false;
                var weather=RingCloudField.Apply(mat,settings,1234567,0,0,1234);
                mat.SetFloat("_CloudAmount",.9f);mat.SetVector("_CoverageOrigin",new Vector4(8000,12,.37f,.23f));
                mat.SetTexture("_SavedDepth",depth);mat.SetVector("_Photo",new Vector4(1,-1,1,0));
                mat.SetVector("_RayForward",new Vector3(0,1,.45f));mat.SetVector("_RayRight",new Vector3(1,0,0));mat.SetVector("_RayUp",new Vector3(0,-.45f,1)*.6f);
                mat.SetVector("_Sun",new Vector3(.3f,0,.95f).normalized);mat.SetVector("_Habitat",new Vector4(0,(float)settings.Geometry.P.Radius,0,(float)settings.Geometry.P.Width/2));
                mat.SetVector("_WeatherMap",new Vector4(0,0,200000,.9f));mat.SetVector("_Quality",new Vector4(64,0,4,150000));mat.SetVector("_FrameSize",new Vector4(160,96,0,0));mat.SetVector("_CloudHandoff",RingCloudField.Handoff(settings,true));
                Func<string,Vector2> capture=name=>
                {
                    Graphics.Blit(null,target,mat,0);RenderTexture.active=target;
                    var image=new Texture2D(160,96,TextureFormat.RGBAFloat,false,true);image.ReadPixels(new Rect(0,0,160,96),0,0);image.Apply();double light=0,alpha=0;
                    foreach(var c in image.GetPixels()){if(float.IsNaN(c.r)||float.IsInfinity(c.r)||float.IsNaN(c.a))throw new Exception("Nonfinite cloud pixel");light+=c.r+c.g+c.b;alpha+=c.a;}
                    var png=new Texture2D(160,96,TextureFormat.RGBA32,false,true);png.SetPixels(image.GetPixels());png.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),png.EncodeToPNG());UnityEngine.Object.Destroy(png);UnityEngine.Object.Destroy(image);
                    return new Vector2((float)(light/(160*96*3)),(float)(alpha/(160*96)));
                };
                mat.SetVector("_Look",new Vector4(0,1,1,1));
                for(int mode=1;mode<=3;mode++)
                {
                    settings.CloudMode=mode;extension.Update(settings,weather,1234);
                    var day=capture("mode-"+mode);
                    if(day.y<.005||day.x<.001)throw new Exception("Empty cloud extension mode "+mode+": "+day);
                    Debug.Log("[RingworldSmoke] CLOUD EXTENSION mode="+mode+" luminance="+day.x+" opacity="+day.y);
                }
                for(int type=0;type<10;type++)
                {
                    mat.SetFloat("_CloudProbe",type+1);var sample=capture("family-"+type);
                    if(sample.x<.00001||sample.y<.00001)throw new Exception("Empty cloud family "+type);
                    Debug.Log("[RingworldSmoke] CLOUD FAMILY "+type+" luminance="+sample.x+" opacity="+sample.y);
                }
                mat.SetFloat("_CloudProbe",0);
                mat.SetFloat("_CloudAmount",.35f);extension.Update(settings,new Ringworld.Core.WeatherSample{Cloud=.35},1234);
                var fair=capture("fair-weather");
                mat.SetFloat("_CloudAmount",.9f);extension.Update(settings,weather,1234);
                var storm=capture("storm-weather");
                if(storm.y<=fair.y+.01)throw new Exception("Weather did not change cloud optical coverage");
                Debug.Log("[RingworldSmoke] CLOUD weather fair="+fair.y+" storm="+storm.y);
                double originalRange=settings.CloudRange;settings.CloudRange=8e12;
                var rangeCopy=Settings.Load();rangeCopy.Apply(settings.Save());
                if(rangeCopy.CloudRange!=8e12||double.IsNaN(rangeCopy.RenderCloudRange)||double.IsInfinity(rangeCopy.RenderCloudRange))throw new Exception("Cloud range capped or nonfinite");
                settings.CloudRange=originalRange;
                var lit=capture("day");mat.SetVector("_Look",new Vector4(0,0,1,1));var night=capture("night");
                if(night.x>lit.x*.2)throw new Exception("Cloud night lighting did not dim");
                mat.SetVector("_Look",new Vector4(0,1,1,1));
                depth.SetPixel(0,0,new Color(10,0,0,0));depth.Apply();var foreground=capture("foreground");
                if(foreground.y>.0001)throw new Exception("Clouds cover foreground depth");
                depth.SetPixel(0,0,new Color(200000,0,0,0));depth.Apply();
                settings.CloudExtension=false;extension.Update(settings,weather,1234);if(capture("disabled").y>.0001)throw new Exception("Cloud extension disable ignored");
                var restored=Settings.Load();restored.Apply(settings.Save());if(restored.CloudExtension||restored.CloudMode!=3)throw new Exception("Cloud settings roundtrip failed");
                settings.CloudExtension=true;extension.Update(settings,weather,1234);
                mat.SetVector("_Habitat",new Vector4(25000,(float)settings.Geometry.P.Radius,0,(float)settings.Geometry.P.Width/2));mat.SetVector("_CloudOrigin",new Vector3(0,0,25000));
                mat.SetVector("_RayForward",new Vector3(0,1,-.65f));mat.SetVector("_RayUp",new Vector3(0,.65f,1)*.4f);
                if(capture("above").y<.005)throw new Exception("Cloud layer invisible from above");
                Debug.Log("[RingworldSmoke] CLOUD EXTENSION day/night, disabled, foreground depth, above-layer and save checks passed");
            }
            finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(depth);UnityEngine.Object.Destroy(mat);}
        }
    }
}
#endif
