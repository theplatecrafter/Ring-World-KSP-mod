using System.Collections.Generic;
using UnityEngine;
namespace NivenRingworld
{
    // Only presentation: a scene-colour copy is needed when a water renderer
    // can enter this camera's frustum. Never use last frame's isVisible flag.
    internal static class RingWaterVisibility
    {
        private static readonly List<Renderer> water=new List<Renderer>();
        private static readonly Plane[] planes=new Plane[6];
        internal static void Add(Renderer renderer){water.Add(renderer);}
        internal static void Remove(Renderer renderer){if(renderer!=null)water.Remove(renderer);}
        internal static bool Visible(Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            for(int i=water.Count-1;i>=0;i--)
            {
                var renderer=water[i];if(renderer==null){water.RemoveAt(i);continue;}
                if(renderer.enabled&&renderer.gameObject.activeInHierarchy&&(camera.cullingMask&(1<<renderer.gameObject.layer))!=0&&GeometryUtility.TestPlanesAABB(planes,renderer.bounds))return true;
            }
            return false;
        }
    }
}
