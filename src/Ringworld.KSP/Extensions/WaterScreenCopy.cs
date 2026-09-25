using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace NivenRingworld.Extensions
{
    internal static class WaterScreenCopy
    {
        private sealed class Buffers { internal CommandBuffer Copy,Release; }
        private static readonly Dictionary<Camera,Buffers> cameras=new Dictionary<Camera,Buffers>();
        private static bool enabled;
        private static readonly int texture=Shader.PropertyToID("_RingWaterBackground");
        internal static void Enable(bool value)
        {
            if(enabled==value)return;
            enabled=value;
            if(value)Camera.onPreCull+=Prepare;
            else {Camera.onPreCull-=Prepare;foreach(var c in new List<Camera>(cameras.Keys))Detach(c);}
        }
        private static void Prepare(Camera camera)
        {
            if(camera!=null&&(camera.cullingMask&(1<<15))!=0)Attach(camera);
        }
        internal static void Attach(Camera camera)
        {
            if(cameras.ContainsKey(camera))return;
            var b=new Buffers{Copy=new CommandBuffer{name="Ringworld Water scene copy"},Release=new CommandBuffer{name="Ringworld Water release"}};
            b.Copy.GetTemporaryRT(texture,-1,-1,0,FilterMode.Bilinear,camera.allowHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default);
            b.Copy.Blit(BuiltinRenderTextureType.CameraTarget,texture);
            b.Copy.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            b.Release.ReleaseTemporaryRT(texture);
            camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,b.Copy);
            camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha,b.Release);cameras.Add(camera,b);
        }
        internal static void Detach(Camera camera)
        {
            Buffers b;if(!cameras.TryGetValue(camera,out b))return;
            if(camera!=null){camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,b.Copy);camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha,b.Release);}
            b.Copy.Release();b.Release.Release();cameras.Remove(camera);
        }
    }
}
