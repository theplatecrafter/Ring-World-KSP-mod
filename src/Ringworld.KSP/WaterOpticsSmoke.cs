#if RINGWORLD_SMOKE_TEST
using System;
using System.IO;
using UnityEngine;
namespace NivenRingworld
{
    internal static class WaterOpticsSmoke
    {
        internal static void Run(AssetBundle bundle)
        {
            var shader=bundle.LoadAsset<Shader>("Assets/Shaders/RingUnderwater.shader");
            if(shader==null||!shader.isSupported)throw new Exception("Underwater shader unsupported");
            var mat=new Material(shader);var rt=new RenderTexture(64,64,0,RenderTextureFormat.ARGBHalf);
            var old=RenderTexture.active;var source=new Texture2D(1,1);source.SetPixel(0,0,Color.white);source.Apply();
            var previousDepth=Shader.GetGlobalTexture("_CameraDepthTexture");var previousZ=Shader.GetGlobalVector("_ZBufferParams");
            var depthTexture=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);depthTexture.SetPixel(0,0,Color.white);depthTexture.Apply();
            var image=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
            try
            {
                Shader.SetGlobalTexture("_CameraDepthTexture",depthTexture);Shader.SetGlobalVector("_ZBufferParams",new Vector4(0,0,0,.01f));
                mat.SetVector("_WaterAlong",Vector3.right);mat.SetVector("_WaterAcross",Vector3.forward);mat.SetVector("_WaterChart",Vector4.zero);
                mat.SetVector("_WaterUp",Vector3.up);mat.SetVector("_WaterSun",new Vector3(0,.8f,.6f));
                mat.SetVector("_ViewForward",Vector3.forward);mat.SetVector("_ViewRight",Vector3.right);mat.SetVector("_ViewUp",Vector3.up);
                Func<float,float,Color> capture=(depth,light)=>{
                    mat.SetVector("_WaterOptics",new Vector4(depth,light,4,0));Graphics.Blit(source,rt,mat);RenderTexture.active=rt;
                    image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();
                    foreach(var c in image.GetPixels())if(float.IsNaN(c.r+c.g+c.b+c.a)||float.IsInfinity(c.r+c.g+c.b+c.a))throw new Exception("Nonfinite underwater optics");
                    return image.GetPixel(32,32);
                };
                var dry=capture(0,1);var day=capture(20,1);var night=capture(20,0);
                if(dry.r<.95||day.r>=dry.r||day.g<=day.r||night.g>=day.g)throw new Exception("Underwater absorption/daylight regression: "+dry+day+night);
                capture(20,1);var png=new Texture2D(64,64,TextureFormat.RGBA32,false,true);png.SetPixels(image.GetPixels());png.Apply();
                File.WriteAllBytes(Path.Combine(KSPUtil.ApplicationRootPath,"Ringworld-underwater-probe.png"),png.EncodeToPNG());UnityEngine.Object.Destroy(png);
                Debug.Log("[RingworldSmoke] UNDERWATER dry bypass, absorption, day/night and finite shafts passed");
            }
            finally{Shader.SetGlobalTexture("_CameraDepthTexture",previousDepth);Shader.SetGlobalVector("_ZBufferParams",previousZ);UnityEngine.Object.Destroy(depthTexture);RenderTexture.active=old;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(mat);UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(source);}
        }
    }
}
#endif
