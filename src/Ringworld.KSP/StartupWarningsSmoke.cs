#if RINGWORLD_SMOKE_TEST
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace NivenRingworld
{
    // Read-only startup evidence. Never dismisses, intercepts or alters warnings.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class StartupWarningsSmoke : MonoBehaviour
    {
        private IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-ringworld-startup-check") < 0) yield break;
            // Kopernicus checks every 121 Update calls; elapsed time alone can
            // miss its warning on a slow machine.
            int frames = 0;
            float deadline = Time.realtimeSinceStartup + 120;
            while (frames < 150 && Time.realtimeSinceStartup < deadline) { frames++; yield return null; }
            Debug.Log("[RingworldStartup] Observed menu frames=" + frames);
            if (frames < 150) { Debug.Log("[RingworldStartup] INCOMPLETE: too few menu frames"); Application.Quit(); yield break; }
            Debug.Log("[RingworldStartup] Terrain=" + PQSCache.PresetList.preset + " reflection=" + GameSettings.REFLECTION_PROBE_TEXTURE_RESOLUTION);
            foreach (var dialog in Resources.FindObjectsOfTypeAll<PopupDialog>())
            {
                if (!dialog.gameObject.activeInHierarchy) continue;
                Debug.Log("[RingworldStartup] POPUP " + dialog.name);
                foreach (var component in dialog.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var property = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                    if (property != null && property.PropertyType == typeof(string))
                        Debug.Log("[RingworldStartup] TEXT " + property.GetValue(component, null));
                }
            }
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("Kopernicus.UI.MissingTexturesPopup.MissingTextureLog", false);
                if (type == null) continue;
                var entries = type.GetProperty("Entries", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null) as IEnumerable;
                int count = 0;
                foreach (var entry in entries)
                {
                    count++;
                    Debug.Log("[RingworldStartup] MISSING_TEXTURE " + entry.GetType().GetField("TexturePath").GetValue(entry));
                }
                Debug.Log("[RingworldStartup] Kopernicus missing textures=" + count);
            }
            Debug.Log("[RingworldStartup] COMPLETE");
            Application.Quit();
        }
    }
}
#endif
