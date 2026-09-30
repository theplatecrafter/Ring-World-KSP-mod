# Optional Ringworld extensions

Extensions add optional rendering features designed for the ring's cylindrical surface. They are separate downloads for base v1.1.5. Install [Ringworld Clouds](https://github.com/theplatecrafter/Ringworld-Clouds-KSP) for cloud volumes and [Ringworld Scattering](https://github.com/theplatecrafter/Ringworld-Scattering-KSP) for enhanced water and distant atmosphere. Both require the base; neither is required by it. Their first releases target base 1.1.5. CKAN availability depends on separate indexing.

## Ringworld Scattering: water

Open the Ringworld panel, choose **Settings**, find **Ringworld extensions**, and enable or disable **Ringworld Water: enhanced surface rendering**. Click **Apply settings**, then save the game to retain the choice. Each ring stores its own choice. Quality presets change water quality but preserve the extension switch.

| Water quality | Rendering |
| --- | --- |
| Extension disabled / Flat | Basic translucent, depth-tinted water |
| Ripples | Animated normals, approximate sky reflection, underwater absorption and haze |
| Waves | Visual waves, finer ripples and shoreline foam |
| Detailed | Screen refraction, oblique-path absorption, sun glitter and 8-sample underwater light shafts |
| Ultra | Detailed rendering with finer surface detail and 16-sample underwater light shafts |

Use Ripples or lower on a laptop. Detailed and Ultra copy the visible screen once per camera render for the water refraction pass; they cost more even if only a small patch of water is visible. Photo mode can temporarily use a higher quality preset without changing the saved extension switch.

Close-range water already has terrain and collision beneath it. Transparent local LOD water now retains a separate seabed mesh rather than replacing the ground with its water surface. Far scaled-space water remains an opaque approximation. Ocean basin shapes and mean water levels are unchanged, preserving existing landing and swimming positions.

Water remains swimmable and buoyant when the extension is disabled. Wave crests are visual; physics uses the mean water level. Terrain generation, science and saves belong to the main mod.

This is original Ringworld rendering, not Scatterer running on the ring. Reflections currently approximate the sky; ships, buildings and the distant ring are not reflected. Refraction samples the current camera image: it cannot reveal objects outside the image, and displaced samples can show foreground-edge artifacts. Underwater haze uses camera-to-scene distance and stops at the mean water surface. Detailed/Ultra add approximate surface-modulated sunlight shafts, dimmed by depth and panel night. These are not ray-traced rays or shadows cast by vessels and terrain. Object reflections remain planned work; refraction is validated against the available scene depth.

### Scattering across the water surface

Detailed and Ultra integrate the submerged part of the view ray even while the camera is above water. Scene depth limits absorption to the first opaque submerged object or seabed; a shallow object is no longer attenuated using the entire depth of a deep ocean. Clear shallow water transmits more of the scene, while long underwater paths progressively lose contrast and red light. Deep oceans are not expected to reveal their entire floor.

**Settings -> Ringworld extensions -> Water light shafts (above and below surface)** controls the extra sampled illumination. It requires enhanced water and Detailed/Ultra water quality; Strong and higher presets enable it, lower presets disable it. Disabling shafts retains basic absorption and scattered water colour. Save the game after applying changes to retain the setting. Below-surface camera scattering and above-surface transmission share the same water-medium model. The expensive camera postprocess only runs while submerged; above-water scattering runs on visible water pixels.

The depth path uses opaque objects that participate in Unity's depth pass. Transparent objects or materials without a depth/shadow pass may not supply a usable endpoint. Refraction remains a screen-space approximation, and shafts are not shadows cast by scene geometry.

## Ringworld Clouds

With Ringworld Clouds installed, **Settings -> Ringworld extensions -> Ringworld Clouds** selects the replacement local cloud volume. Its switch is saved per ring. Disabling volumes keeps the lightweight/distant layers and evolving weather; set the cloud amount to zero to remove clouds altogether.

| Cloud mode | Features |
| --- | --- |
| Layers only | Lightweight local and full-ring cloud layers, no cloud ray marching |
| Economy | Shaped low clouds and storm towers, depth-clipped volumes, self-shadowing |
| Balanced | Economy plus fine erosion, a high cirrus layer and additional scattered light |
| Detailed | Balanced plus curl distortion and another scattered-light approximation |

Slow and lower presets select Layers only. Mid selects Economy, Good/Strong select Balanced, and Beefy through Absolute Cow select Detailed. Cloud step count, range and render resolution still determine the rendering cost. Changing a preset preserves the extension switch. Photo mode can select a temporary preset.

The old local volume model has been replaced. Cloud types blend smoothly between low stratocumulus, cumulus and tall cumulonimbus as the shared weather field changes. Balanced and Detailed add wispy cirrus. A generated fBm/Worley shape texture supplies billows and eroded edges. Universal-time wind and upward motion keep running during time warp; night follows the ring panels. The distant layer remains a coarse approximation sharing the same coverage and handoff, not a full-volume render of the entire ring.

This is original Ringworld code inspired by EVE's public feature descriptions. It is **not EVE Redux or blackrack's Volumetric Clouds V5 ported into the ring**, and does not require either installation. EVE Redux's public renderer uses a different particle technique; the newer raymarched implementation is not present in that public source tree. There is no claim of complete visual or feature parity. Ground/object cloud shadows, lightning meshes, precipitation volumes, windshield droplets, wet surfaces, temporal reprojection, signed-distance acceleration and light-volume caching remain future work. Existing Ringworld rain/lightning effects remain independent.

### Editing cloud types

`GameData/RingworldClouds/CloudTypes.cfg` exposes ten named types. Altitudes are metres above the mean ring floor. `shape0` through `shape3` define cloud coverage at normalized heights 0, 1/3, 2/3 and 1. Other values control density, erosion, powder lighting, ambient fill and curl. Restart KSP after changing these configs. Invalid numeric values fall back to defaults; ranges are bounded for renderer stability. The supported type names are currently fixed; arbitrary extra layers need a renderer change.

## Organization for contributors

The two extensions have independent repositories, DLLs, shader bundles and releases. The base owns geometry, meshes, physics, weather and saved settings. It discovers optional rendering providers at runtime without referencing their DLLs. The initial provider interface is version paired: extensions 1.0.0 require base 1.1.5. General gameplay adapters should use the public surface API instead of this internal rendering interface.

See each extension's developer guide for build instructions. The shared local layout is documented in [Workspace](../developers/WORKSPACE.md). Removing an extension retains a functional base renderer and does not remove the ring or its vessels.

## Ringworld Scattering: full-ring atmosphere and water

Version 1.1.5 groups distant atmosphere and enhanced water under **Settings -> Ringworld extensions -> Ringworld Scattering**. They have independent saved switches. This is Ringworld's own rendering module, not a port of the external Scatterer mod. Ringworld Scattering is a separate optional package; CKAN indexing is handled separately.

**Full-ring atmosphere** adds a lightweight blue optical column over the entire inner ribbon in map, Tracking Station and distant flight views. It also remains visible along the distant ring while landed. It has no terrain-render-distance cutoff. The nearby contribution fades out between 250 and 600 km from the flight camera so Cyla or Original handles the local sky. Map cameras show the full layer. Shadow-panel night regions suppress the lit scattering, and the opaque hull blocks exterior views through the floor.

All presets retain this inexpensive layer when enabled. Original/laptop presets use the basic Rayleigh-like appearance; higher visual tiers add a forward-lighting approximation. Haze and atmosphere brightness affect it. Its cost is a fixed mesh plus one transparent pass; it does not generate additional terrain or march through the entire ring. Turning off atmosphere or this switch removes the layer. Existing saves default to enabled; presets preserve the switch.

The distant layer approximates optical thickness from viewing angle and caps grazing paths. It is not a full three-dimensional atmosphere simulation: there is no resolved atmospheric thickness at the ribbon edges, exact multiple scattering or exact Cyla colour matching. Cyla remains the optional nearby backend; its bounded local proxy cannot simply be expanded to the physical ring radius without the precision problems that motivated that proxy.

## Weather and water realism

Rain and snow use a bounded field of world-space particles. Snow biomes replace raindrop streaks with soft, drifting white flakes; stronger snowy storms increase sideways drift. Snow does not occur merely because a biome is cold: precipitation must be present. Local particles are drawn after the cloud compositor, with scene-depth rejection to keep them behind spacecraft and terrain. They are suppressed above 10x time warp. The old full-screen rain veil has been removed.

Distant rain shafts use the same cloud coverage and storm intensity as the cloud volume. Lightning endpoints remain attached to a seeded region of the ring and rise from the terrain into the cloud layer, with the upper bolt fading into the cloud. This is still an approximation: bolts do not trace a simulated electric field or sample cloud transmittance along their full path, and the particle field is not a fluid simulation.

Weather descriptions now distinguish snow, blizzards, blowing desert dust and humid rim-shadow fog. Dust/fog add a bounded low-level atmospheric contribution on volume-capable settings. These are artistic hypotheses for a managed rotating habitat, not predictions from a validated Ringworld climate model. Cloud amount, precipitation amount, quality and weather evolution remain the existing controls.

**Volumetric cloud distance** accepts any finite distance of at least 30 km that can be represented in metres. There is no 5,000 km input cap. Absolute Cow defaults to 20,000 km, Extra Beefy to 10,000 km, Beefy to 5,000 km, Strong to 2,000 km, Good to 750 km and Mid to 300 km. Lower presets retain lightweight layers. Nearby sampling is denser; distant sampling uses coarser shape mipmaps and omits small-scale curl/erosion. Full-ring clouds take over across the final part of the range. Longer range spreads a finite sample budget over more distance; it is not free detail and may require more cloud steps.

Enhanced water now sums several seeded directions and wavelengths with gravity-dependent phase speeds. Higher tiers add more normal detail, filtered specular sun glints and irregular shoreline foam. It is a finite-wave approximation, not Scatterer's FFT ocean. Vertex displacement filters out wavelengths that the current mesh cannot resolve, while retaining finer wave normals. The coarse water mesh still limits wave silhouettes; this change does not provide object reflections, wave collisions or a fully simulated breaking-wave surface.


### Cloud families and weather (v1.1.5)

| Family | Default layer above ring datum | Appearance and role |
| --- | --- | --- |
| Stratus | 0.35–1.7 km | Low sheets; favoured in humid rim shadows |
| Stratocumulus | 0.9–2.4 km | Clumped low decks |
| Cumulus | 1.2–6.5 km | Cellular heaps in non-raining weather |
| Cumulonimbus | 0.9–12 km | Deep storms, spreading anvil profile |
| Nimbostratus | 0.7–6.5 km | Broad thick layers associated with sustained precipitation |
| Altocumulus | 2.5–5.5 km | Smaller middle-level cells |
| Altostratus | 2.5–7.5 km | Middle sheets, favoured as rain increases |
| Cirrus | 9–12.5 km | Thin stretched high ice wisps |
| Cirrostratus | 6.5–11 km | Broad high veils in wetter weather |
| Cirrocumulus | 7–10 km | Small high cellular patches, highest volume mode |

These are configurable art/model defaults, not universal cloud altitude limits. Noise scale, vertical profile, erosion and lighting distinguish the families; local weather blends the low/middle/high layers. Economy volumes omit the middle/high layers; detailed volumes include them, while the highest mode also includes cirrocumulus. Slow and lower retain lightweight clouds. Existing custom cloud configs override defaults after restart.

The formation model follows the distinction between lifted, cooling moist air, convective heaps and stable layered clouds described by [NWS cloud development](https://www.weather.gov/source/zhu/ZHU_Training_Page/clouds/cloud_development/clouds.htm) and its [cloud classification/photo reference](https://www.weather.gov/lmk/cloud_classification). It does not solve humidity, lapse rates or convection. Rim-shadow stratus is a plausible habitat-specific interpretation, not a proven cloud species unique to a Ringworld. Lightning and rain ceilings now use the configured storm/precipitating layers.

Very large requested distances remain in the save. Actual ray reach stops at the ring bounding extent (diameter plus width) and the shader's finite arithmetic limit. Sample counts remain bounded, so more distance can reduce detail or miss thin layers; whole-ring cloud LOD remains necessary. These settings do not promise high FPS or uniform voxel detail over arbitrary distances.

Long cloud rays skip empty space between near/far shell crossings instead of spending their sample budget there. Detailed volume modes use a small depth/opacity-aware spatial reconstruction filter to reduce stochastic speckle; this is not temporal reprojection and cannot recover all undersampled detail. Optical parameters blend with weather rather than switching abruptly between family lighting profiles.

### Cloud transitions and rain coverage

The lightweight cloud deck fades radially before its mesh boundary. Shared local cloud coverage adds irregular mesoscale clusters, and volumetric cloud profiles fade at their upper and lower surfaces. Wet weather closes coverage gaps before rain begins, avoiding precipitation beneath an otherwise empty local cloud field. Fair-weather lightweight clouds are brighter than overcast decks.

Automatic weather favours partly cloudy conditions over completely empty skies. Setting cloud amount to zero still disables clouds and precipitation. Cloud morphology, coverage and lighting evolve with weather and universal time; this remains a procedural weather model, not a simulation of atmospheric fluid dynamics. Thick cloud undersides can be gray even in daytime because less sunlight reaches them.
