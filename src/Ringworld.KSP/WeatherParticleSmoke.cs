#if RINGWORLD_SMOKE_TEST
using System;
using System.IO;
using UnityEngine;
namespace NivenRingworld
{
    internal static class WeatherParticleSmoke
    {
        internal static void Run(AssetBundle bundle)
        {
            var shader=bundle.LoadAsset<Shader>("Assets/Shaders/Precipitation.shader");
            if(shader==null||!shader.isSupported)throw new Exception("Precipitation shader unsupported");
            var m=new Material(shader);var mesh=new Mesh();
            mesh.vertices=new[]{new Vector3(-.8f,-.8f,0),new Vector3(.8f,-.8f,0),new Vector3(.8f,.8f,0),new Vector3(-.8f,.8f,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};mesh.triangles=new[]{0,1,2,0,2,3};
            var depth=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);var target=new RenderTexture(64,64,0);var old=RenderTexture.active;
            try
            {
                m.SetFloat("_Manual",1);m.SetFloat("_Saved",1);m.SetTexture("_WeatherDepth",depth);m.SetMatrix("_WeatherVP",Matrix4x4.identity);m.SetMatrix("_WeatherView",Matrix4x4.identity);
                string folder=Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath,"../artifacts/validation/weather"));Directory.CreateDirectory(folder);
                Func<string,float> capture=name=>
                {
                    RenderTexture.active=target;GL.Clear(false,true,Color.black);m.SetPass(0);Graphics.DrawMeshNow(mesh,Matrix4x4.identity);
                    var image=new Texture2D(64,64,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();float sum=0;foreach(var c in image.GetPixels())sum+=c.r;
                    File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);return sum/4096;
                };
                depth.SetPixel(0,0,new Color(100,0,0,0));depth.Apply();m.SetFloat("_Flake",0);float rain=capture("rain");
                m.SetFloat("_Flake",1);float snow=capture("snow");
                depth.SetPixel(0,0,Color.clear);depth.Apply();float hidden=capture("occluded");
                if(rain<.2||snow<.05||snow>=rain*.9||hidden>.001)throw new Exception("Rain/snow foreground clipping failed: "+rain+" / "+snow+" / "+hidden);
                Debug.Log("[RingworldSmoke] WEATHER PARTICLES rain="+rain+" snow="+snow+" foreground="+hidden);
            }
            finally{RenderTexture.active=old;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(depth);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(m);}
        }
    }
}
#endif
