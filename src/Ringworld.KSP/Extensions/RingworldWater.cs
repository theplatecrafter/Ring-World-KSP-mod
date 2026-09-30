using Ringworld.Core;
using UnityEngine;
namespace NivenRingworld.Extensions {
 internal static class RingworldWater {
  internal static void Update(Settings settings,Material material,Shader fallback,DVec observer,Vector3d star){var provider=ExtensionProviders.Scattering;if(provider!=null)provider.UpdateWater(settings,material,fallback,observer,star);else material.shader=fallback;}
 }
}
