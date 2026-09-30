using System;
using UnityEngine;
namespace NivenRingworld.Extensions {
 internal sealed class RingworldScattering : IDisposable {
  readonly IDistantAtmosphere layer;
  internal RingworldScattering(Transform parent,Settings s,AssetBundle unused){if(ExtensionProviders.Scattering!=null)layer=ExtensionProviders.Scattering.Create(parent,s);}
  internal void Update(Settings s,double time){if(layer!=null)layer.Update(s,time);}
  public void Dispose(){if(layer!=null)layer.Dispose();}
 }
}
