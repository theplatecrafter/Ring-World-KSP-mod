using System;
using System.Runtime.CompilerServices;
using Ringworld.Core;
using UnityEngine;
[assembly: InternalsVisibleTo("Ringworld.Clouds")]
[assembly: InternalsVisibleTo("Ringworld.Scattering")]
[assembly: InternalsVisibleTo("Ringworld.Parallax")]
namespace NivenRingworld.Extensions
{
 // Cloud/water provider ABI originated in host 1.1.5. Scatter/photo provider
 // interfaces require host 1.1.8 for Ringworld Parallax 1.0.0.
 // No extension DLL is referenced by the host; missing providers are valid.
 internal interface ICloudLayer { float RainBase{get;} float StormTop{get;} void Update(Settings settings,WeatherSample weather,double time); }
 internal interface ICloudProvider { AssetBundle Assets{get;} Shader AtmosphereShader{get;} ICloudLayer Create(Material material); }
 internal interface IDistantAtmosphere : IDisposable { void Update(Settings settings,double time); }
 internal interface IScatterLayer : IDisposable {
  void Update(Ringworld.Core.DVec observer);
  void Reposition();
  bool OwnsTrees(double along,double across,double size);
 }
 // Optional photo handshake: older/absent scatter providers remain valid.
 // Readiness includes population under the temporary photo quality settings.
 internal interface IPhotoScatterLayer { bool PhotoReady {get;} }
 internal interface IScatterProvider {
  string Status {get;}
  IScatterLayer Create(Settings settings);
  bool OwnsTrees(double along,double across,double size);
  void DrawSettings(RingworldFlight flight);
 }
 internal interface IScatteringProvider { AssetBundle Assets{get;}
  Shader WaterShader(bool refraction); IDistantAtmosphere Create(Transform parent,Settings settings);
  void UpdateWater(Settings settings,Material material,Shader fallback,DVec observer,Vector3d star);
  void ScreenCopy(bool value);void Underwater(bool value);void Attach(Camera camera);void Detach(Camera camera);
 }
 internal static class ExtensionProviders {
  static bool initialized;static ICloudProvider clouds;static IScatteringProvider scattering;static IScatterProvider parallax;
  static T Find<T>(string name) where T:class {
   foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
    var type=assembly.GetType(name,false);if(type==null)continue;
    try{return Activator.CreateInstance(type,true) as T;}catch(Exception e){Debug.LogError("[NivenRingworld] Optional extension unavailable: "+name+" / "+e.Message);return null;}
   }return null;
  }
  static void Initialize(){if(initialized)return;initialized=true;clouds=Find<ICloudProvider>("NivenRingworld.Extensions.CloudProvider");scattering=Find<IScatteringProvider>("NivenRingworld.Extensions.ScatteringProvider");parallax=Find<IScatterProvider>("NivenRingworld.Extensions.ParallaxProvider");Debug.Log("[NivenRingworld] Optional extensions: Clouds="+(clouds!=null)+", Scattering="+(scattering!=null)+", Parallax="+(parallax!=null));}
  internal static ICloudProvider Clouds {get{Initialize();return clouds;}}
  internal static IScatteringProvider Scattering {get{Initialize();return scattering;}}
  internal static IScatterProvider Parallax {get{Initialize();return parallax;}}
 }
}
