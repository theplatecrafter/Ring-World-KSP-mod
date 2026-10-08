# Niven Ringworld Expedition 1.1.7

A Larry Niven-inspired, star-encircling habitat with a landable rotating interior, procedural terrain, atmosphere, science and persistent expeditions. The default world uses one-tenth of the published linear ring dimensions. Original geography and architecture are interpretations of the setting.

Spacedock page: https://spacedock.info/mod/4573/Niven's%20Ring%20World

[Player documentation](docs/README.md) · [Release history and dependency versions](RELEASE-NOTES.md)

## Install and play

Extract the release ZIP into the KSP instance root, beside `KSP_x64.exe`. Its `GameData` folder merges with the existing one. Do not extract the entire ZIP inside `GameData`. Alternatively, open the ZIP and copy only its `GameData` contents into the existing `GameData` folder.

**Install [HarmonyKSP 2.2.1.0 or compatible newer version](https://github.com/KSPModdingLibs/HarmonyKSP/releases) separately.** CKAN calls it **Harmony 2 (`Harmony2`)**. This ZIP contains only `GameData/NivenRingworld`, plus documentation; it no longer includes other mods. For the optional Cyla atmosphere, separately install [Cyla 1.1.0](https://github.com/LGhassen/Cyla/releases). Without Cyla, the built-in Original atmosphere is used. Do not remove an existing Harmony/Cyla installation when upgrading Ringworld. Restart KSP after updating DLLs. No Kopernicus or downloaded art pack is required. The supported release target is KSP 1.12.5 on Windows x64 / Direct3D 11.

Build and launch a lander with enough thrust for approximately 1 g. The stock toolbar's ring icon, or Left Alt+R, opens the Ringworld panel. Sandbox exposes site relocation, a random terrain visit and a spin-matched training approach; Science and Career show flight information without those development controls. A relocation starts above the terrain at rest relative to the ring: **you must brake and land it**.

For ordinary orbital arrival, enter the annular region near the ring. The mod preserves your velocity; it does not supply free braking. An unmatched solar trajectory encounters hundreds of km/s of air-relative motion and is destructive. Leaving the rotating frame restores the corresponding inertial motion, not a shortcut back to Kerbin.

When settled on dry ground, use **KSP's stock time-warp controls**. Universal time and the other celestial bodies advance normally. Save and return to the Space Center through the ordinary controls. **Revert Flight intentionally discards progress.** Airborne/physics warp in the ring frame is not supported.

## Multiple habitats (Sandbox)

Open the Ringworld panel → Settings → **Sandbox: manage ring worlds**. Select an existing star or no designated star, enter the center offset in km, dimensions and seed, then spawn a new ring. The list selects a ring for editing; **Visit selected ring** transfers the active vessel. All rings have distant outlines, while detailed terrain runs around the current habitat. Moving/deleting an occupied ring is blocked; at least one ring must remain. See [multiple-ring behavior and limits](docs/guides/MULTIPLE-RINGS.md).

## v1.1.7

Rings can follow moving bodies, with adjustable inclination, gravity, panels and wall height. Instance config packs initialize new saves; see [configuration packs](docs/guides/CONFIG-PACKS.md). Distant-star presets require Kopernicus and ModuleManager; interstellar gameplay is experimental and not yet fully tested.

Stock air intakes now use ring oxygen and airflow. Terrain retains its previous complete layout while replacement chunks build. Settled residents can save despite unsafe nearby vessels, and Tracking Station encounters no longer automatically send you into Flight. See the release notes for validation scope.

Enhanced rendering is now provided by two optional downloads: [Ringworld Clouds](https://github.com/theplatecrafter/Ringworld-Clouds-KSP) and [Ringworld Scattering](https://github.com/theplatecrafter/Ringworld-Scattering-KSP). The base mod retains lightweight visuals and all gameplay. Install either extension, both, or neither. Use extension 1.0.1 with base 1.1.7 for inclined water and physical eclipses. Settings are grouped in the Extensions tab, and installed extensions default to enabled unless previously disabled.

This release also adds stock-compatible immersion and displacement-based buoyancy, SunkWorks water-query support, and the ReStock Surveyor model fix. See the release notes for tested cases and remaining limitations.

## Previous v1.1.4 highlights

- Cyla optical-distance and scene-depth scaling corrects a locally reproduced near-surface black sky. Confirmation on affected NVIDIA/Proton machines is pending; please report the new session's KSP.log if it persists.
- Quality presets disable the unfiltered Cyla dithering that produced patterned skies. Low sample counts can still show broad gradients from high camera altitudes; further improvements are tracked.
- Stock jettisoned physical objects receive ring-frame acceleration. Stock EVA helmet safety checks recognize breathable ring air while retaining pressure/temperature limits.
- Cyla is an optional CKAN suggestion in this release's metadata; Harmony remains required. Mid and higher presets select installed Cyla automatically, with an advanced diagnostic override and Original fallback.

## Release contents

- Ring-relative centrifugal/Coriolis dynamics, local atmosphere and automatic frame entry/departure.
- Local navball cues, SAS direction targets, altimeter and vertical-speed gauge, parachute deployment gates and numerical map encounter preview.
- Landed residence, pause-menu saving, reload through the Space Center, Ringworld science subjects and tested deployed-science anchoring.
- Stock science by biome, landmark, structure, wall, altitude band and shadow square; a Research journal with twelve expedition milestones and Career funds/reputation rewards. See [science and expeditions](docs/guides/SCIENCE-AND-EXPEDITIONS.md) and the [add-on guide](docs/developers/MODDING-RESEARCH.md).
- Seeded terrain, smooth climate biomes, rivers, lakes, oceans, mountains, roads, grass, stones and continuous forest canopies.
- A full scaled ring with closed dark rim walls, moving night bands and sparse procedural cloud coverage; optional high-quality local volumetric clouds, water waves and frozen photo rendering.
- **92 Blender-authored scenery prefabs / 276 LOD meshes**. Twenty-five landmark placements and eight rare colossus templates include floating palaces, ruined cities and eight new colossi: an **80 km rim gate**, **120 km causeway** and **48 km floating city plate**. Machinery is scenery, not an operational simulation.

See [release notes](RELEASE-NOTES.md#release-v114), [colossi and continuous forests](docs/reference/COLOSSI-AND-FORESTS.md), [landmark designs and placement](docs/developers/ASSET-AUTHORING.md#landmark-assets), [dimensions/triangle inventory](docs/reference/LANDMARK-INVENTORY.md), and the [combined biome catalog](docs/reference/BIOME-ASSET-CATALOG.md).

## Settings and performance

New saves start at Slow. Eleven save-specific quality presets range from Rotten Potato to Absolute Cow. Slow and below use the lightweight Original atmosphere; Mid and above use Cyla. KSP texture quality, antialiasing, shadows and scatter controls remain separate. Water offers five levels; photo mode has its own preset and aspect-preserving output resolution through 16K where GPU limits permit.

Terrain LOD distance accepts a finite numeric value without the old two-million-km cap. Work and mesh budgets remain bounded, and only the physical ring extent is generated. A distant macro surface supplies the full outline; it is not detailed terrain everywhere. Large landmarks stream separately: the original kit uses 60 km, colossi 250 km plus their extent; small scenery remains within the near-terrain tiles. Initial distant-terrain generation takes time.

Dense forests use merged canopy patches with three LODs and pooled trunk colliders only near loaded vessels. KSP scatter density and Ringworld forest density control their population. See the forest catalog for exact budgets and validation notes for measured performance.

Blank seeds resolve once per new save. Existing saves retain their seed and terrain algorithm. Do not change world dimensions or terrain generation underneath an established expedition. Rendering and weather can be changed independently. See [settings](docs/guides/QUALITY-PRESETS.md), [high-end visuals](docs/guides/QUALITY-PRESETS.md#atmosphere-backend-and-photo-selection), and [weather/night](docs/developers/CLOUDS-AND-WEATHER.md#weather-and-night).

## Scale

| Property | Default |
|---|---:|
| Radius | 15,300,000 km |
| Ribbon width | 160,500 km |
| Rim-wall height | 160 km |
| Floor acceleration | 9.72 m/s² |
| Rotation period | 249,282.88 s / 69.245 h |
| Surface tangential speed | 385.637 km/s |
| Illumination cycle | 3 hours, configurable adaptation |

The stock Sun remains the native reference body. The ring's influence boundary is annular, not a native spherical SOI. Science subjects identify Ringworld, but the archive may group them under the Sun. See [canon/scale](docs/reference/CANON-AND-SCALE.md) and [residence/encounters](docs/guides/FLIGHT-AND-SAVING.md).

## Validation and limits

The release checks include 108,655 core assertions, real KSP pause-menu save, five damage-enabled warp cycles through 1,000×, stock SAS/parachute gates, flight/EVA/camera tests, powered deployed science, scene reload, asset imports/contact rays and live woodland streaming. Exact logs, fixture assumptions and measurements are in [VALIDATION.md](docs/history/VALIDATION.md).

This does not certify arbitrary fleets, every mod combination, every aircraft or docking configuration, detailed building interiors, or unloaded atmospheric trajectories. Water buoyancy is approximate; the coast preview ends at atmosphere entry. Read [known limitations](docs/guides/KNOWN-LIMITATIONS.md) and [mod interoperability](docs/developers/MOD-INTEROPERABILITY.md). Back up saves before upgrading. A new DLL cannot reconstruct a craft already destroyed or corrupted in an older version.

## Development and Blender sources

The base and extension repositories share a parent workspace. The development game is the sibling `../template_instance`; see [workspace setup](docs/developers/WORKSPACE.md). Editable art is under `art/first-set`, `art/habitat-kit`, `art/city-kit` and `art/landmark-kit`; the latest scene is `art/landmark-kit/Ringworld-Landmarks.blend`. The runtime ZIP contains compiled assets, not Blender or game files.

With a .NET SDK and your KSP 1.12.5 installation:

```powershell
.\build.ps1 -Install -Package
# Another game installation:
.\build.ps1 -KspRoot 'D:\Games\KSP' -Install
.\smoke-test.ps1 -StabilityOnly
.\smoke-test.ps1 -SceneryOnly -LandmarksOnly
.\smoke-test.ps1
python tools/verify_release.py
```

The build runs core checks, compiles .NET Framework 4.7.2 plugins and stages the distributable. Smoke tests use isolated saves and restore the normal plugin afterward. Blender uses `tools/blender_bridge.py` on localhost port 9876; rebuilding the asset bundle requires the Unity editor described in [BLENDER-ASSETS.md](docs/developers/ASSET-AUTHORING.md#blender-assets). Player installations do not need these development tools.

## Uninstall and attribution

Return ring vessels to ordinary spaceflight first. Close KSP and remove `GameData/NivenRingworld`. Craft containing the RW-1 require the mod to load; other mods may still need Harmony. The save's custom state is in `RingworldScenario`.

An unofficial fan project inspired by Larry Niven's *Ringworld*. Original code, descriptions, textures and meshes are covered by [LICENSE](LICENSE); Niven's setting and names are not licensed by that software license. No book passages, cover art or proprietary game assemblies are redistributed.



## Release history

See [release notes](RELEASE-NOTES.md) for version changes, dependency requirements and historical installation details.

## TODO
1. More structures?
2. Check compatibility with other visual/physics mods
3. Improve performance for high-resolution terrain and atmosphere rendering
4. Progression-gated arrival/return transport: remote reconnaissance locates a surviving terminal, then science/funds unlock a transfer that matches ring velocity. Preserve physical unmatched arrivals; do not require Sandbox teleport controls for a practical stock-parts Career expedition. Not implemented yet.
5. Native Mission Control contracts, tourism and rescue chains building on the research journal; current milestones have no deadlines or penalties.
6. Persistent field bases: supplies, repair expeditions, infrastructure restoration and resource logistics. Decide which systems belong in optional compatibility modules rather than mandatory life support.
7. Kerbal expedition experience and awards on safe return; avoid granting repeated XP for the same destination.
8. Add-on content validation for duplicate research IDs, missing prerequisites/cycles and generation migrations; localisation of journal/config text.
9. Science-overhaul adapters (especially Kerbalism), research-driven map markers and saved discovery coordinates for individual procedural colossi.
10. Optional Parallax Continued bridge: stable placement patches, ring biome/ecology masks, object LOD and distant forest aggregates; retain laptop fallback. [Research and staged implementation design](docs/developers/PARALLAX-INTEGRATION.md). No working Parallax adapter yet.
11. Ring resource deposits and shared scanner/harvester provider, per-ring exploration unlocks, stock drill tests and optional resource-pack adapters. KFS gravitic-drive integration needs ring up/acceleration, surface coordinates and safe warp handling.
12. Flattened map of Earth or Kerbol system stellar objects in the great oceans just like in the books.
13. Huge Attitude Jets around the circumference
14. Fix and smoothen out trajectories
15. Kopernicus says "You have changed terrain settings" for some reason for interstellar configs
16. RSS Compatibility and RSS features
17. Animals on the ringworld (Animal Mod compatibility)
18. Some dude said he lodged into the ground at a reletavistic speeds with a kraken drive and null-aero hack.
19. marker for interstellar ringworld in map
20. The outer side of the ring still has visible cloud and shadow layers, when it should be just scryth in shadow

### Active visual compatibility work

Continue NVIDIA/Proton verification, high-altitude Cyla banding/motion tests, native-Linux shader builds, and installed-mod tests for Waterfall, TUFX and other visual frameworks. EVE, Scatterer and Parallax do not currently replace the ring's clouds, water or terrain. Geometry-aware adapters need further work; see docs/developers/MOD-INTEROPERABILITY.md. These are not advertised as completed v1.1.4 features.
