using System;
using HarmonyLib;
using UnityEngine;
namespace NivenRingworld
{
    // Change only the galaxy cube's own colour, after stock fading. No sky overlay,
    // camera layer mask or planet/Sun renderer is modified. Stock Update restores it elsewhere.
    [HarmonyPatch(typeof(GalaxyCubeControl),"Update")]
    internal static class RingStarfield
    {
        internal static float Visibility=1;
        private static void Postfix(Renderer[] ___cubeRenderers,MaterialPropertyBlock ___mpb)
        {
            Visibility=1;var f=RingworldFlight.Instance;
            if(f==null||f.Settings==null||f.Star==null||!f.Settings.Atmosphere||MapView.MapIsEnabled||FlightCamera.fetch==null)return;
            var camera=FlightCamera.fetch.mainCamera;if(camera==null)return;
            var c=f.Settings.Geometry.Coordinates(ConvertVector.Core((Vector3d)camera.transform.position-f.Center));
            if(c.Altitude< -1000||c.Altitude>f.Settings.Geometry.P.AtmosphereHeight||Math.Abs(c.Across)>f.Settings.Geometry.P.Width/2)return;
            float daytime=(float)f.Settings.Geometry.Daylight(c.Along,Planetarium.GetUniversalTime(),c.Across,c.Altitude);
            float air=1-Mathf.SmoothStep(0,1,(float)(c.Altitude/f.Settings.Geometry.P.AtmosphereHeight));
            Visibility=1-daytime*air;
            var colour=___mpb.GetColor(Shader.PropertyToID("_Color"));colour.r*=Visibility;colour.g*=Visibility;colour.b*=Visibility;
            ___mpb.SetColor(Shader.PropertyToID("_Color"),colour);
            if(___cubeRenderers!=null)foreach(var renderer in ___cubeRenderers)if(renderer!=null)renderer.SetPropertyBlock(___mpb);
        }
    }
}
