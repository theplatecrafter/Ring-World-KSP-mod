using UnityEngine;
using Ringworld.Core;
namespace NivenRingworld.Extensions {
 internal sealed class RingworldClouds {
  readonly ICloudLayer layer;
  internal RingworldClouds(Material material,AssetBundle unused){if(ExtensionProviders.Clouds!=null)layer=ExtensionProviders.Clouds.Create(material);else material.SetVector("_CloudControls",Vector4.zero);}
  internal static bool Requested(Settings s){return ExtensionProviders.Clouds!=null&&s.CloudExtension&&s.CloudMode>0&&s.Atmosphere&&s.CloudAmount>0&&s.CloudDensity>0;}
  internal float RainBase{get{return layer==null?700:layer.RainBase;}}
  internal float StormTop{get{return layer==null?12000:layer.StormTop;}}
  internal void Update(Settings s,WeatherSample weather,double time){if(layer!=null)layer.Update(s,weather,time);}
 }
}
