# Third-party integrations

The original Niven Ringworld Expedition code is covered by LICENSE (MIT). From v1.1.3, player release archives contain no bundled third-party mods.

## Cyla (optional, separately installed)

Cyla by Ghassen Lahmar (LGhassen / blackrack), copyright (c) 2024 Ghassen Lahmar: https://github.com/LGhassen/Cyla . The adapter targets upstream 1.1.0. Cyla supplies its own GPLv3 plugin and compiled-only shader notice; see its upstream License.md. Ringworld does not redistribute those components in this release or relicense them. The repository's vendor reference retains its original notices and provenance.

## Harmony (required, separately installed)

HarmonyKSP / Harmony 2: https://github.com/KSPModdingLibs/HarmonyKSP . Tested with 2.2.1.0. Upstream retains its MIT license and authorship. Install GameData/000_Harmony from its own distribution or CKAN's Harmony2 entry.

## Additional optional integrations (v1.1.5 development)

TUFX, Scatterer, Deferred/Shabby, Waterfall and BetterTimeWarp are independently distributed projects. Ringworld does not bundle their plugins, shaders, textures or source. Each separate installation retains its upstream licence and notices. Ringworld's runtime adapters and `TUFX-Profiles.cfg` are original project code/configuration, not copies of their implementations. See [Credits](CREDITS.md) and the [integration test matrix](docs/developers/VISUAL-INTEGRATIONS.md) for project links and supported behaviour.

## EVE cloud research (v1.1.5 development)

Environmental Visual Enhancements by Ryan Bray, Warwick Allison, Ghassen Lahmar and contributors: https://github.com/LGhassen/EnvironmentalVisualEnhancements . Public Redux source and raymarched-cloud documentation were consulted as design references. The Ringworld Clouds extension, configuration and generated noise assets are original implementation; no EVE plugin, shader, texture, documentation excerpt or preset is bundled. The public repository's MIT/GPL notices do not imply redistribution permission for separately distributed volumetric-cloud releases. Ringworld does not claim to include that implementation.

Water realism research: Scatterer/Proland ocean spectrum, slope and whitecap architecture was inspected as a reference. The bounded directional-wave and specular implementation added here is original; no Scatterer source, shader or spectrum assets are distributed. Scatterer remains separately licensed under GPL-3.0.
