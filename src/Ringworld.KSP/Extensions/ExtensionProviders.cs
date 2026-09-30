using System;
using System.Runtime.CompilerServices;
using Ringworld.Core;
using UnityEngine;
[assembly: InternalsVisibleTo("Ringworld.Clouds")]
[assembly: InternalsVisibleTo("Ringworld.Scattering")]
namespace NivenRingworld.Extensions
{
 // Versioned first-party ABI. Extension 1.0 is paired with host 1.1.5.
 // No extension DLL is referenced by the host; missing providers are valid.
 internal interface ICloudLayer { float RainBase{get;} float StormTop{get;} void Update(Settings settings,WeatherSample weather,double time); }
 internal interface ICloudProvider { AssetBundle Assets{get;} Shader AtmosphereShader{get;} ICloudLayer Create(Material material); }
 internal interface IDistantAtmosphere : IDisposable { void Update(Settings settings,double time); }
 internal interface IScatteringProvider { AssetBundle Assets{get;}
  Shader WaterShader(bool refraction); IDistantAtmosphere Create(Transform parent,Settings settings);
  void UpdateWater(Settings settings,Material material,Shader fallback,DVec observer,Vector3d star);
  void ScreenCopy(bool value);void Underwater(bool value);void Attach(Camera camera);void Detach(Camera camera);
 }
 internal static class ExtensionProviders {
  static bool initialized;static ICloudProvider clouds;static IScatteringProvider scattering;
  static T Find<T>(string name) where T:class {
   foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
    var type=assembly.GetType(name,false);if(type==null)continue;
    try{return Activator.CreateInstance(type,true) as T;}catch(Exception e){Debug.LogError("[NivenRingworld] Optional extension unavailable: "+name+" / "+e.Message);return null;}
   }return null;
  }
  static void Initialize(){if(initialized)return;initialized=true;clouds=Find<ICloudProvider>("NivenRingworld.Extensions.CloudProvider");scattering=Find<IScatteringProvider>("NivenRingworld.Extensions.ScatteringProvider");Debug.Log("[NivenRingworld] Optional extensions: Clouds="+(clouds!=null)+", Scattering="+(scattering!=null));}
  internal static ICloudProvider Clouds {get{Initialize();return clouds;}}
  internal static IScatteringProvider Scattering {get{Initialize();return scattering;}}
 }
}
