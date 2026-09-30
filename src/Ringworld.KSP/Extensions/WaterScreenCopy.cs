using UnityEngine;
namespace NivenRingworld.Extensions {
 internal static class WaterScreenCopy {
  internal static void Enable(bool value){ExtensionProviders.Scattering?.ScreenCopy(value);}
  internal static void Attach(Camera camera){ExtensionProviders.Scattering?.Attach(camera);}
  internal static void Detach(Camera camera){ExtensionProviders.Scattering?.Detach(camera);}
 }
}
