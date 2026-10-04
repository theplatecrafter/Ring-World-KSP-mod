# Niven Ringworld ? release notes

Consolidated release history, newest first. Historical requirements apply only to their own version. This is the release publisher's single source for release descriptions.

## Current requirements ? v1.1.6

| Component | Requirement | Version |
| --- | --- | --- |
| Kerbal Space Program | Required | 1.12.5 |
| Harmony 2 / HarmonyKSP | Required, installed separately | 2.2.1.0 tested; CKAN minimum 2.2.1.0 |
| Cyla | Optional local atmosphere | 1.1.0.0 release (Cyla-1.1.0.zip) |
| Ringworld Clouds | Optional volumetric clouds | 1.0.0, compatible with base 1.1.6 |
| Ringworld Scattering | Optional enhanced water and distant atmosphere | 1.0.0, compatible with base 1.1.6 |

No dependencies or extensions are bundled. Extract each ZIP into the **KSP installation root**, merging GameData. The base works without either extension and uses Original atmosphere without Cyla. Optional metadata is supplied for CKAN submission; publication alone does not update the central index.

## Release-v1.1.6

KSP 1.12.5; Harmony 2 >= 2.2.1.0 is required separately. Optional: Cyla 1.1.0.0, Ringworld Clouds 1.0.0 and Ringworld Scattering 1.0.0. No dependencies or extensions are bundled. Extract the ZIP into the KSP root, merging GameData.


### Crash survivors and Tracking Station

- A grounded, settled active vessel can be saved even while nearby crash debris is still moving. Surface time warp retains its checks for all nearby vessels.
- Ring encounters no longer automatically open Flight from the Tracking Station. Approaching craft still stop unsafe warp; select Fly to control them. Incidental debris no longer triggers the station's encounter warp guard.
- These changes do not add unloaded atmospheric or collision simulation for debris.


### Stock intakes and terrain streaming

- Stock air intakes use local ring oxygen and airflow rather than the host star's oxygen flag. Stock pressure thresholds, intake alignment, shielding and resource consumption remain in effect.
- Terrain keeps the previous complete LOD layout visible while replacement meshes build. Nearby terrain is retained until the replacement layout is ready.
- Shared stock environment bridge also handles engine nozzle immersion, solar-panel underwater attenuation and the gravity instrument's local gravity/distance check.
- Shared environment API adds explicit oxygen availability, sound speed, atmosphere height, water elevation, local airflow and derived Mach/dynamic pressure for other mods. Mods reading the host body's fields directly still need integration.
- Stock intake production and staged terrain handoff passed the in-game module regressions; complete stock-plane flights and all third-party controllers are not certified.


### Extension control panel update

- Dedicated Extensions tab with collapsible Clouds, Scattering and Cyla controls, available in all game modes.
- Installed Ringworld extensions default to enabled; existing saved disabled choices are retained. Enable switches apply immediately. Quality presets continue to control cost, including layers-only clouds on Slow and lower.
- Normal Settings no longer overwrites extension-specific edits.

### Validation and compatibility

The changed stock intake/environment, terrain publication, Tracking Station and resident save/reload paths passed their targeted in-game regressions. The resident and four deployed science units survived the Space Center round trip. All 110,323 core checks passed. Other recorded regression results are retained from earlier builds; this is not a claim that every suite was rerun for 1.1.6.

The compatible rendering assembly identity is retained for extension 1.0.0 binaries. NetKAN changes to accept base 1.1.5 or newer are submitted separately; CKAN installation depends on those changes being merged and indexed. The exact reported plane breakup, freely moving debris physics and arbitrary third-party autopilots remain unverified. Unloaded atmospheric/collision simulation is not added.

## Release-v1.1.5

### Dependencies and optional integrations

- Required: KSP 1.12.5 and separately installed Harmony 2 >= 2.2.1.0 (tested 2.2.1.0).
- Optional extension: Ringworld Clouds 1.0.0 for advanced cloud volumes; base lightweight layers and weather remain available without it.
- Optional extension: Ringworld Scattering 1.0.0 for enhanced water, underwater optics and distant full-ring atmosphere. Water physics remains in the base.
- Optional local atmosphere: Cyla 1.1.0 (CKAN version 1.1.0.0); Original is the fallback.
- Optional integrations: TUFX profiles, Scatterer panel-flare handling, Deferred rendering and BetterTimeWarpContinued rate-table safeguards. These do not turn the ring into a PQS planet. Waterfall remains a part effect, not ring terrain rendering.
- Part compatibility targets: ReStock 1.5.1 (Surveyor model) and SunkWorks 1.3.1 (ballast/aquatic-engine checks). The regression document records the precise tested cases; general autopilot/boat support is not implied.
- Parallax, planetary EVE/scatterer configurations and Trajectories do not automatically gain cylindrical terrain/flight support. See the integration matrix for limits.

Each extension is a separate download with a dependency on base 1.1.5; none is bundled in the base ZIP. CKAN metadata is provided for submission and does not itself guarantee an extension is indexed. Extract all ZIPs into the KSP root, merging GameData.


KSP 1.12.5, Windows x64 / Direct3D 11. This version separates enhanced rendering from the base mod. Users upgrading from the public v1.1.3 or v1.1.4 builds can install either optional extension separately. CKAN availability follows its maintainers' indexing process.

- ReStock compatibility for the legacy Surveyor, contributed by john-d-alexander (PR #2), with a stock model fallback when ModuleManager is absent.
- Ring water now supplies stock part immersion/contact state and displacement-based buoyancy, so additional ballast mass can overcome buoyancy. SunkWorks 1.3.1 ballast filling/venting and wet/dry aquatic-engine queries passed with the installed modules. These checks do not certify every boat, engine thrust configuration or autopilot.
- Resident unpacking no longer resets the active craft's global velocity frame when a neighbouring craft enters physics range. Saved contact poses are refreshed before packing; large-base approach tests remain pending.
- Detailed/Ultra water now uses scene-depth transmission and shared water scattering from both sides of the surface. Added a saved light-shafts toggle, enabled by Strong and higher presets; shallow submerged objects remain visible above deep seabeds.
- Enhanced water adds camera-depth underwater absorption/haze, quality-scaled approximate sunlight shafts, broader surface glitter and oblique-path absorption. Transparent local LOD water retains its seabed geometry; mean water levels are unchanged.
- Softer local cloud boundaries and irregular cluster coverage; rainy weather closes overhead gaps before precipitation starts, with more partly cloudy automatic weather.
- Ten weather-dependent cloud families, expanded upper-preset cloud distances, and uncapped finite cloud-distance input.
- Crustal resource definitions from stock ResourceCache now feed a shared ring abundance provider, stock scanner display and stock harvester queries; default primary-ring Ore configuration included. Orbital surveys/background mining remain unsupported.
- Surface API v5 adds position-based air/gravity/water queries and buoyancy helpers. Experimental KFS hover, Hooligan Labs and Heisenberg static-lift query adapters; full mod craft validation pending.
- Read-only loaded terrain mesh interface for prospective Parallax adapters; no Parallax scatter integration yet.

- Weather realism work: world-space rain/snow and post-cloud particle composition, longer cloud distances, cloud-linked rain shafts, local fog/dust and less regular water waves with filtered sunlight glints.

- Added a switchable full-ring distant atmosphere approximation for map, space and landed views, grouped with enhanced water as Ringworld Scattering. Nearby sky remains handled by Cyla or Original.

- Automatic TUFX profiles and a Scatterer panel-flare adapter when those mods are installed.
- Coarse land/oceans at every preset, visible cloud undersides, cloud-field evolution and configurable cloud handoff distance.
- Terrain lighting transition work and optional Deferred/Waterfall checks.
- Neighbour-aware colour feathering at fine/coarse terrain borders and a gradual forest colour filter transition.
- Replacement Ringworld Clouds volume extension with editable cloud types, new shape noise, self-shadowing, curl detail and quality controls.
- Ringworld Scattering water controls; Detailed/Ultra add screen refraction and coloured absorption.
- Read-only ring environment API and custom warp-rate table safeguards.

### Validation scope

ReStock 1.5.1 Surveyor prefab/model/material checks and SunkWorks 1.3.1 installed ballast/engine water queries passed. The base-only, Clouds-only, Scattering-only and combined installations passed their runtime rendering tests. Water probes cover transparency at all five quality levels, above-surface transmission, underwater absorption, shafts and dry-camera bypass. The Slow flight test passed photo output/restoration and 1,500 physics ticks of calibrated flotation.

The flotation fixture retained damage immunity; it does not certify every boat, engine-thrust setup or autopilot. Non-ring query/dispatch checks passed. Issue #3 was excluded from this verification pass and its unresolved cases remain documented. Tests ran on the development laptop with Direct3D 11; native Linux and other GPU/API results are not certified.

See the [integration matrix](docs/developers/VISUAL-INTEGRATIONS.md) for tested versions, limitations and remaining visual/utility work. Optional mods are installed separately and are never bundled.


## Release-v1.1.4

KSP 1.12.5; tested on Windows x64 / Direct3D 11. Install HarmonyKSP 2.2.1.0 or a compatible newer version separately. Optional Cyla: upstream release 1.1.0.0 (archive named Cyla-1.1.0.zip). No dependencies are bundled. Extract into the KSP root, preserving `GameData/NivenRingworld`.

### Cyla atmosphere fixes and diagnostics

Optical distances are now passed to Cyla in kilometre units, with matching scattering coefficients and a Ringworld-owned scene-depth conversion. This corrects the black sky reproduced from an affected save on the development machine while preserving foreground spacecraft. It does not change physical ring dimensions or atmospheric flight forces. Confirmation from the affected NVIDIA and Proton/Wine machines is still pending; this release is available for that retest.

Quality presets now leave Cyla dithering off, avoiding the structured pattern observed with unfiltered dithering. Existing custom settings are preserved; reselect a preset to take its revised defaults. Broad bands can still appear at low view-sample counts when zoomed far out. The complete high-altitude/motion-quality investigation is ongoing.

The first daytime Cyla session logs its graphics environment and small render-target sample statistics to KSP.log. This helps compare API, colour space, MSAA, camera settings and finite/dark shader output. It does not upload data. Unsupported or absent Cyla continues to use Original atmosphere. Presets select the backend automatically; the manual override is retained under Advanced Cyla optics and diagnostics for troubleshooting.

The stock Sun flare now follows the shadow-panel daylight mask in ring flight views. A live test confirmed zero flare brightness under a night panel and restoration in daylight. Custom flares from other mods and the separate Sun disc need further validation.

### Physics and EVA

Stock loose physical objects, such as jettisoned covers, now receive the rotating ring frame's acceleration instead of the reference body's gravity. Stock drag and object lifetime remain intact. A stock-converted loose-object test measured about 9.7154 m/sÂ² toward the floor.

Stock EVA helmet safety checks use ring air, oxygen availability and temperature. Pressure and temperature safety limits remain in force, and disabling the atmosphere prevents helmet removal. The Sun's atmosphere properties are not changed. The live EVA safety and existing science/career regression checks passed.

### Optional mods and remaining work

Harmony remains required. The version-specific CKAN metadata lists Cyla as optional (`suggests`); the [central NetKAN update](https://github.com/KSP-CKAN/NetKAN/pull/11604) is submitted and awaits maintainer acceptance. EVE, Scatterer, Parallax, TUFX and Waterfall research is documented, but this release does not claim new cylindrical EVE/Scatterer/Parallax rendering or a Kopernicus rebuild. Native-Linux rendering, NVIDIA confirmation and additional installed-mod tests remain outstanding.

## Release-v1.1.3

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Release-v1.1.3)

KSP 1.12.5, Windows x64 / Direct3D 11.

### Installation change: dependencies are separate

This ZIP contains **only NivenRingworld**, plus its documentation. Extract into your KSP root beside KSP_x64.exe, preserving the existing `GameData/NivenRingworld` layout.

- **Required:** [HarmonyKSP / Harmony 2](https://github.com/KSPModdingLibs/HarmonyKSP/releases), minimum declared version **2.2.1.0**, also the tested version. CKAN identifier: `Harmony2`.
- **Optional:** [Cyla by Ghassen Lahmar (LGhassen / blackrack)](https://github.com/LGhassen/Cyla/releases), supported version 1.1.0. Without it, Ringworld uses Original atmosphere.
- Upgrading from a bundled release: retain existing Harmony and Cyla folders; do not create duplicates. Other mods may also depend on Harmony.

The release includes separate `.netkan` and version-specific `.ckan` files for CKAN maintainer review. Harmony is declared required. Cyla is not declared under an invented identifier: optional indexing awaits its author's and CKAN's approval. These files do not mean that CKAN has accepted the listing.

### Sandbox: multiple ring worlds

Ringworld panel â†’ Settings â†’ **Sandbox: manage ring worlds**. Spawn, name, move or delete habitats with their own dimensions, seeds and saved identities. Select an existing star, including a loaded planet-pack star, or no designated star. Enter center offsets in kilometers. Use **Visit selected ring** for a spin-matched sandbox transfer.

Distant outlines, walls, clouds and panels render for each habitat; detailed terrain is streamed around the current habitat. Saved residents and science use per-ring identities. Occupied rings cannot be moved/deleted, overlapping habitat envelopes are rejected, and at least one habitat must remain.

This initial editor supports parallel rings fixed relative to a reference body. Without a designated star the reference is the stock Sun, and no star is created. Off-center illumination retains the existing central-star approximation. Arbitrary tilt, orbiting centers, combined multi-ring gravity prediction and physically accurate off-axis lighting are future work. Planet-pack stars are selectable but cross-mod behavior has not been validated against every pack.

### Exploration and science

Stock instruments, crew/EVA reports and surface samples now produce separate science for flight regions, biomes, named landmarks, individual megastructures, rim walls and shadow panels. Science values range from 6Ã— to 20Ã— stock subject multipliers. The Research tab tracks twelve expedition milestones; Career awards one-time funds and reputation on qualifying data delivery. Progress persists and duplicate delivery does not repay milestones. No new science part is required; legacy RW-1 craft remain supported.

Also includes the development fix for nearby/faraway rim-wall material consistency.

### Validation and limits

Core geometry/terrain/research checks and in-game regressions are recorded in `docs/VALIDATION.md`. Offset-ring landing, native warp, per-ring science and Space Center save/reload were tested at Slow. Missing Cyla was tested separately and fell back to Original. See `docs/MULTIPLE-RINGS.md` for the editor's placement/physics limits and `docs/CKAN-PUBLISHING.md` for metadata submission. CKAN catalog acceptance and a real CKAN client installation are separate from local package/schema verification.

### Published GitHub description

### Niven Ringworld Expedition v1.1.3

KSP 1.12.5, Windows x64 / Direct3D 11.

#### Installation change: dependencies are separate

This ZIP contains **only NivenRingworld**, plus its documentation. Extract into your KSP root beside KSP_x64.exe, preserving the existing `GameData/NivenRingworld` layout.

- **Required:** [HarmonyKSP / Harmony 2](https://github.com/KSPModdingLibs/HarmonyKSP/releases), minimum declared version **2.2.1.0**, also the tested version. CKAN identifier: `Harmony2`.
- **Optional:** [Cyla by Ghassen Lahmar (LGhassen / blackrack)](https://github.com/LGhassen/Cyla/releases), supported version 1.1.0. Without it, Ringworld uses Original atmosphere.
- Upgrading from a bundled release: retain existing Harmony and Cyla folders; do not create duplicates. Other mods may also depend on Harmony.

The release includes separate `.netkan` and version-specific `.ckan` files for CKAN maintainer review. Harmony is declared required. Cyla is not declared under an invented identifier: optional indexing awaits its author's and CKAN's approval. These files do not mean that CKAN has accepted the listing.

#### Sandbox: multiple ring worlds

Ringworld panel â†’ Settings â†’ **Sandbox: manage ring worlds**. Spawn, name, move or delete habitats with their own dimensions, seeds and saved identities. Select an existing star, including a loaded planet-pack star, or no designated star. Enter center offsets in kilometers. Use **Visit selected ring** for a spin-matched sandbox transfer.

Distant outlines, walls, clouds and panels render for each habitat; detailed terrain is streamed around the current habitat. Saved residents and science use per-ring identities. Occupied rings cannot be moved/deleted, overlapping habitat envelopes are rejected, and at least one habitat must remain.

This initial editor supports parallel rings fixed relative to a reference body. Without a designated star the reference is the stock Sun, and no star is created. Off-center illumination retains the existing central-star approximation. Arbitrary tilt, orbiting centers, combined multi-ring gravity prediction and physically accurate off-axis lighting are future work. Planet-pack stars are selectable but cross-mod behavior has not been validated against every pack.

#### Exploration and science

Stock instruments, crew/EVA reports and surface samples now produce separate science for flight regions, biomes, named landmarks, individual megastructures, rim walls and shadow panels. Science values range from 6Ã— to 20Ã— stock subject multipliers. The Research tab tracks twelve expedition milestones; Career awards one-time funds and reputation on qualifying data delivery. Progress persists and duplicate delivery does not repay milestones. No new science part is required; legacy RW-1 craft remain supported.

Also includes the development fix for nearby/faraway rim-wall material consistency.

#### Validation and limits

Core geometry/terrain/research checks and in-game regressions are recorded in `docs/VALIDATION.md`. Offset-ring landing, native warp, per-ring science and Space Center save/reload were tested at Slow. Missing Cyla was tested separately and fell back to Original. See `docs/MULTIPLE-RINGS.md` for the editor's placement/physics limits and `docs/CKAN-PUBLISHING.md` for metadata submission. CKAN catalog acceptance and a real CKAN client installation are separate from local package/schema verification.

## Release-v1.1.2

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Release-v1.1.2)

Maintenance release of the integrated Cyla build, with the original ZIP layout retained.

### Included

- Cyla atmosphere integration and advanced optical settings, with eleven quality presets.
- Translucent water, procedural ripples and optional waves; camera clearance follows the underwater ground.
- Photo quality selection and aspect-preserving output sizes through 8K/16K, subject to GPU limits.
- Ring-relative camera transitions, re-entry wall rendering fixes, landed save/warp checks and Tracking Station encounter handling.
- The complete scenery bundles, 92 registered scenery prefabs, 25 landmark assets and eight rare colossal structure templates.

These features were integrated on main for v1.1.1. This release rebuilds the current source, increments the version and clarifies installation; it does not introduce a new asset layout or a new atmospheric solver.

### Installation â€” unchanged ZIP structure

Extract the ZIP into the **KSP instance root**, beside `KSP_x64.exe`. The result must include:

```
GameData/NivenRingworld/
GameData/Cyla/
GameData/000_Harmony/
```

Alternatively, copy the contents of the ZIP's `GameData` folder into your existing `GameData` folder. Do not place the entire extracted archive inside another folder under `GameData`; the standard asset paths expect the layout above. Keep only one installation of each dependency.

The reported missing assets were traced to nested extraction. A file audit found all 26 runtime files present and byte-identical to the tested archive, but the asset loaders could not find them at the expected paths. The proposed alternate-layout changes were cancelled. This release preserves the established paths.

### Credits and limitations

Cyla is by **Ghassen Lahmar (LGhassen / blackrack)**: https://github.com/LGhassen/Cyla. Its unmodified binaries, original license and matching published plugin source are included. See CREDITS.md and THIRD-PARTY-NOTICES.md; the complete bundle is not uniformly MIT.

Slow and below use Original atmosphere. Cyla remains a local optical approximation; whole-ring Cyla scattering is not implemented. Scatterer does not automatically recognize ring water. Higher revised optical budgets and 8K/16K captures were not rendered on this laptop. No 30 FPS guarantee is made.

### Validation

The release build runs 108,655 core checks. Package verification checks archive CRCs, required asset registries, pinned Cyla hashes, source/license inclusion, installed-plugin parity and exclusion of test harnesses and KSP assemblies. Previous dated Slow-only runtime tests cover water transparency, photo size/restoration, gear landing/save/warp, guidance, tracking and re-entry; see [VALIDATION.md](docs/history/VALIDATION.md). This maintenance release does not claim those gameplay tests were newly rerun.

### Published GitHub description

### Niven Ringworld Expedition v1.1.2

Maintenance release of the integrated Cyla build, with the original ZIP layout retained.

#### Included

- Cyla atmosphere integration and advanced optical settings, with eleven quality presets.
- Translucent water, procedural ripples and optional waves; camera clearance follows the underwater ground.
- Photo quality selection and aspect-preserving output sizes through 8K/16K, subject to GPU limits.
- Ring-relative camera transitions, re-entry wall rendering fixes, landed save/warp checks and Tracking Station encounter handling.
- The complete scenery bundles, 92 registered scenery prefabs, 25 landmark assets and eight rare colossal structure templates.

These features were integrated on main for v1.1.1. This release rebuilds the current source, increments the version and clarifies installation; it does not introduce a new asset layout or a new atmospheric solver.

#### Installation â€” unchanged ZIP structure

Extract the ZIP into the **KSP instance root**, beside `KSP_x64.exe`. The result must include:

```
GameData/NivenRingworld/
GameData/Cyla/
GameData/000_Harmony/
```

Alternatively, copy the contents of the ZIP's `GameData` folder into your existing `GameData` folder. Do not place the entire extracted archive inside another folder under `GameData`; the standard asset paths expect the layout above. Keep only one installation of each dependency.

The reported missing assets were traced to nested extraction. A file audit found all 26 runtime files present and byte-identical to the tested archive, but the asset loaders could not find them at the expected paths. The proposed alternate-layout changes were cancelled. This release preserves the established paths.

#### Credits and limitations

Cyla is by **Ghassen Lahmar (LGhassen / blackrack)**: https://github.com/LGhassen/Cyla. Its unmodified binaries, original license and matching published plugin source are included. See CREDITS.md and THIRD-PARTY-NOTICES.md; the complete bundle is not uniformly MIT.

Slow and below use Original atmosphere. Cyla remains a local optical approximation; whole-ring Cyla scattering is not implemented. Scatterer does not automatically recognize ring water. Higher revised optical budgets and 8K/16K captures were not rendered on this laptop. No 30 FPS guarantee is made.

#### Validation

The release build runs 108,655 core checks. Package verification checks archive CRCs, required asset registries, pinned Cyla hashes, source/license inclusion, installed-plugin parity and exclusion of test harnesses and KSP assemblies. Previous dated Slow-only runtime tests cover water transparency, photo size/restoration, gear landing/save/warp, guidance, tracking and re-entry; see [VALIDATION.md](docs/history/VALIDATION.md). This maintenance release does not claim those gameplay tests were newly rerun.

## Release-v1.1.1

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Release-v1.1.1)

### Changes

- Removed the arbitrary 200 million km diameter limit and upper day/weather-period caps. Finite values, geometric relationships and coordinate precision are still validated.

- Airborne or unstable ring residents now explicitly fail stock save/exit checks; stable ground contact remains required.

- Tracking Station displays the ring-aware trajectory. Warp that would skip an encounter is rejected; imminent entry hands the vessel to Flight for real atmospheric/collision physics. Legacy airborne ring snapshots are discarded in favor of the current stock orbit, avoiding stale-position restoration.

- The scaled ring uses camera-relative double-precision placement, and approach terrain is repositioned after floating-origin updates. A dedicated dark-wall shader fixes the large diagonal wall/sky cutouts reproduced during the ascent/re-entry test at normal and enlarged ring sizes.

- Integrated Cyla's compiled cylindrical atmosphere through a flight-camera adapter, with automatic fallback to the original renderer if unavailable.

- Added depth-aware reduced-resolution Cyla rendering and preset-controlled atmosphere/light sampling.

- Slow and below select the original lightweight atmosphere; Mid and above select Cyla. Backend controls remain independently editable.

- Rotten Potato uses the minimum supported graphics controls, including a 200 km terrain horizon. Absolute Cow uses the maximum supported controls and whole-ring terrain coverage. Stock KSP settings and world generation remain separate.

- Translucent ring water now allows the camera below its surface, with five quality levels: flat, ripples, waves, detailed and ultra. Higher levels add procedural surface variation; reflections remain a sky approximation.

- Photo output resolution preserves screen aspect ratio, with long-edge choices through 8K and 16K, subject to GPU texture/memory limits. The world is rendered at the output resolution rather than upscaled.

- Advanced Cyla controls expose optical scattering coefficients/intensities, scale heights, asymmetry, 1â€“500 view steps, lighting boundaries and proxy geometry/offsets.

- Both photo entry points offer eleven temporary quality presets. Terrain, forest, water and atmosphere follow the selection; Resume/Cancel restore gameplay settings.

- Trajectory calculation uses a bounded frame-time-aware work budget to improve refresh under low rendering FPS without changing numerical integration.

- Atmosphere rendering is suspended during teleport transitions; Cyla uses a camera-owned background capture instead of a shared grab texture.

### Install

Copy all folders in `GameData` into KSP 1.12.5's GameData folder. Cyla and Harmony are included; no separate Cyla installation is required. Avoid installing duplicate copies under different folder names. Existing saves should reselect their desired graphics preset once to adopt the revised values.

### Credits and licensing

Cyla is by **Ghassen Lahmar (LGhassen / blackrack)**: https://github.com/LGhassen/Cyla.

The upstream license identifies GPLv3 plugin code and compiled-only shaders. Original copyright/license text, published plugin source and pinned binary provenance accompany this release. Cyla binaries are unmodified. Niven Ringworld's original MIT notices remain applicable to its original files; the whole archive is not uniformly MIT. Read CREDITS.md and THIRD-PARTY-NOTICES.md.

### Limits

- Cyla uses a 100,000 km optical proxy to avoid float precision failures at the ring's real 15,300,000 km radius. Actual geometry and physics retain the real dimensions. This is a local scattering approximation; **whole-ring distant Cyla scattering is not implemented**. Map/global ring visuals retain their existing renderer.

- A 2880x1920 live-physics camera sweep with clouds enabled measured **16.8 FPS** on this laptop at Rotten Potato. This release does not promise 30 FPS. Terrain, trees, clouds, resolution and physics all affect frame time.

- Higher photo presets can require several minutes of terrain preparation on a laptop; Cancel restores gameplay.

- The exact reported blue-dot sky/portrait glitch was not conclusively reproduced. Precision/camera hazards were addressed, but a universal fix is not claimed.

- Ring water is not automatically discovered by Scatterer: Scatterer requires a celestial-body ocean adapter, not a mesh tag. Existing ring buoyancy is approximate; the public surface API exposes water state for opt-in integrations.

- 8K/16K captures have not been rendered on this laptop; large outputs are intended for GPUs with sufficient memory.

### Validation

108,655 core assertions and a clean plugin build. Earlier in-game Cyla checks passed daylight visibility, foreground depth, camera isolation, map switching, eleven preset round-trips and endpoint checks, low photo capture, high photo cancellation/completion, and gameplay restoration. Current Slow-only checks cover the expanded settings and aspect-preserving photo output. Higher revised optical budgets and 8K/16K are not laptop-tested. See [VALIDATION.md](docs/history/VALIDATION.md) for dated fixtures and limitations.

### Camera transitions

Entering or leaving the ring-relative frame now blends the external camera view over two real-time seconds. The ship's physical coordinate conversion is immediate. The blend tracks the vessel and current stock camera target, restores stock state before each update, and leaves terrain/wall camera clearance active. Explicit relocation and scene changes clear the transition.

## Release-v1.1.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Release-v1.1.0)

### Published GitHub description

Implemented Cyla (https://github.com/LGhassen/Cyla)

## Release-v1.0.3

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Release-v1.0.3)

Added an immediate-apply quality preset dropdown with Absolute Cow, Extra Beefy, Beefy, Strong, Good, Mid, Slow, Better Potato, Potato, Aged Potato and Rotten Potato. Each sets terrain range/resolution, streaming budget, forest quality, atmosphere, water, ground detail and weather visual controls. Low tiers retain a 160,000 km horizon. Individual controls remain available; edits use Apply settings. Save the game to retain changes.

The Biome features section separates forest quality from atmosphere. Economy is cheaper than the former Laptop forest level: approximately one-quarter as many nearby crown shapes, one simple mesh per patch, no tree shadows and no intermediate distant crown meshes. Forest colour/relief remains in distant terrain. Nearby physical tree distribution is retained. Changes rebuild one existing scenery tile per frame. No terrain seed, density or flight physics changes.

Existing saves without forestQuality retain their former atmosphere-linked forest level on first load; thereafter it is independent. Restart KSP to load the new DLL. Quality names are relative cost tiers, not FPS guarantees. Coarse aggregate forests and LOD transitions remain visible.

[CKAN-PUBLISHING.md](docs/publishing/CKAN-PUBLISHING.md) documents hosting, dependency ownership and submission; distribution/NivenRingworld.netkan.example is a local draft requiring a real public hosting ID. Nothing has been submitted or uploaded.

Validation: 108,648 core checks and the live preset/scenery regression passed. Economy reduced stored near-canopy vertices from 2,392,388 to 70,365 in the fixture, with no intermediate distant crown meshes. This does not imply a corresponding FPS increase. See [VALIDATION.md](docs/history/VALIDATION.md).

## Release-v1.0.2

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Release-v1.0.2)

The square forest in 1.0.1 was the near-terrain rendering footprint, not a square biome. Forests now continue into terrain LODs as simplified, merged crown clusters, then area-filtered canopy colour and height at greater distances. Near and distant rendering share the same climate and woodland mask. The representation uses at most one extra canopy mesh per intermediate terrain block, without millions of additional tree objects or distant colliders. Existing terrain range/resolution and scatter settings apply.

The eight colossus templates now spawn rarely from the terrain seed across the ring, instead of clustering around named sites. Defaults: one candidate in 15% of 2,000 km cells, then dry-ground/hull checks. A 600 km exclusion around named landmarks leaves zero colossi there by default; ordinary buildings remain. Configuration and the artist-facing biome representation are documented in [COLOSSI-AND-FORESTS.md](docs/reference/COLOSSI-AND-FORESTS.md).

This release changes scenery, not save/warp physics. Restart KSP to load the new plugin/configuration. Existing seeds remain unchanged; megastructure positions change deliberately. Distant canopy is simplified geometry, not individual tree rendering.

Validation after the battery-interrupted run was repeated successfully: 108,648 core assertions, rare-placement checks, queued-frame isolation, and completed forest/terrain streaming. The Laptop fixture measured about 21 FPS. High-altitude views still show coarse intermediate clusters and visible LOD bands; see [VALIDATION.md](docs/history/VALIDATION.md) for measurements and limits.

## Release-v1.0.1

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Release-v1.0.1)

### Landing legs and flight instruments

Stock landing legs now read the ring's local apparent gravity for wheel contact, spring/damper adjustment and suspension load distribution. They no longer tune these systems to the Sun. Stored anti-drift orientation and its integral are reset on frame changes. The mod does not freeze Rigidbody motion, reduce gravity or change users' leg spring settings.

Collision-enhancer sweep history is reset after relocation/frame changes. Its penetration-recovery direction follows the ring normal; this avoids interpreting a coordinate-chart change as an impact, or pushing a recovered part beneath the ring floor. Normal impact and thermal damage remain enabled.

A separate rest filter handles persistent, bounded flex in attached parts: root rotation must remain below .05 rad/s, every part below .12 rad/s, and chatter above .05 rad/s requires one physics-second with root and relative part poses within 3 cm / 0.5 degrees of their reference. The existing .25 m/s surface-speed, contact, water and throttle gates remain. This permits tiny solver oscillations without ignoring sustained sliding or rotation.

The stock vertical-speed gauge and sink-rate warning use velocity along the local ring normal. The original gauge response and warning hysteresis are retained. Stock behaviour applies outside the ring frame.

### Scenery

Eight original superstructures include an 80 km rim gate, 120 km causeway and 48 km floating city plate. The asset library now contains 92 prefabs and 276 visual LOD meshes. Designs and dimensions are artistic interpretations; machinery is static scenery. See [the colossus inventory](docs/reference/COLOSSI-AND-FORESTS.md).

Forests use continuous overlapping canopy patches, merged geometry and three LODs instead of sparse seven-tree clumps. Nearby trunk colliders are pooled; terrain-scatter settings and ring forest density remain respected. Forests do not cover dry grasslands indiscriminately and do not extend to the entire distant terrain horizon.

### Validation

The natural-gravity regression launches a copy of the user's 38-part, eight-LT-2-leg craft in an isolated save. Initial placement puts the pod root 10 m above the ground (the extended feet are much lower), spin-matched. The ensuing touchdown uses normal ring gravity with no velocity controller, gravity adjustment or damage immunity. This is a touchdown test, not an unpowered fall from orbital altitude. It exercises grounded settling, the paused save gate, stock rails warp and scene reload. The instrument check inspects the actual stock gauge target and sink LED. Asset checks inspect imported units, LODs, supported materials and contact rays. Live forest counts and frame-rate samples are recorded in [VALIDATION.md](docs/history/VALIDATION.md).

This release remains scoped to tested KSP 1.12.5 Windows/D3D11 workflows. Residual stock suspension motion is possible; arbitrary damaged craft, all leg designs and every mod combination are not certified. The previous 1.0.0 ZIP is preserved. Restart KSP after installing the new DLL.

## Release-v1.0.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Release-v1.0.0)

Target: **KSP 1.12.5, Windows x64 / Direct3D 11**. This is the first packaged stable baseline for the supported local expedition workflow. It is not a claim that every KSP mod combination, vessel, or high-speed manoeuvre is certified.

### Install

Close KSP. Extract the archive's `GameData/NivenRingworld` into your KSP `GameData` folder. The archive also includes HarmonyKSP in `GameData/000_Harmony`; use a single compatible Harmony installation if your mod list already supplies it. Do not install a second nested `GameData` directory. Restart the game after updating DLLs. Back up an existing expedition save before changing versions.

The development instance has the normal plugin installed automatically. Blender, FBX sources, Unity import projects, game assemblies, test saves and smoke-test code are not needed by players and are not in the runtime ZIP.

### Fixed

- Pause-menu save rejection: saving can evaluate settled ground contact while time is paused. The same contact/motion safety checks still apply.
- Landed stock-warp destruction: thermal integration now reads zero ring-relative airspeed for a rails-anchored craft, rather than its enormous solar orbital velocity. Stock bookkeeping retains a valid inertial conic.
- Stock SAS prograde, retrograde, radial and normal targets use the ring frame. Near-zero velocity holds the current target rather than selecting a noisy direction.
- Stock parachutes use ring pressure and ground/water height for their deployment gates.
- Numerical map paths update with a bounded per-frame work budget and annular entry/escape markers. Predictions stop at the atmosphere; they do not predict atmospheric descent or thrust.
- Landed residence, stock science subjects, powered deployed-science anchoring and Space Center reload have dedicated in-game regressions.

### Assets and forests

31 new Blender assets bring the library to **84 prefabs / 252 LOD meshes**. There are 25 newly configured architectural landmarks: suspended palaces, ruined city rings, archive and garden complexes, rim gates, a 4 km elevator cathedral, viaducts, aqueducts, water-processing works, observatories and settlements. Designs are original, lore-inspired interpretations; machinery and inhabitants are not simulated.

Large landmarks stream independently to 60 km, one prefab per frame, with three visual LODs and simplified exterior contact meshes. They appear around the existing city, rim, spill-mountain, map-island, scrith and expedition sites. Flight scenery is not mirrored as individual map-mode building models.

Four individual tree variants and two seven-tree groves thicken woodland without changing the terrain seed or existing individual-tree coordinates. Additional grove caps are 16/24/32 per tile for Laptop/High/Ultra. Stock scatter settings and Ringworld forest density remain authoritative. The Laptop fixture reached 5,488 added tree silhouettes over 49 tiles at 28.9â€“35.5 FPS across two five-second samples; this is not a general FPS guarantee.

### Remaining boundaries

The stock Sun remains the native SOI and archive body owner; the ring has an annular local reference frame. Atmospheric/physics warp, arbitrary unloaded fleets, detailed building interiors, operational elevators, full-ring building impostors, third-party aerodynamics/thermal integration and all possible EVA or docking combinations are outside the validated release scope. Water buoyancy is approximate. The science archive may group Ringworld subjects under the Sun even though their names and subject IDs distinguish the ring.

Use the ordinary Save and Space Center controls to keep an expedition. **Revert Flight intentionally discards progress.** Existing corrupted or exploded craft cannot be repaired by loading the new DLL. Keep world seed and dimensions fixed after establishing a settlement.

See [VALIDATION.md](docs/history/VALIDATION.md) for exact test evidence, [KNOWN-LIMITATIONS.md](docs/guides/KNOWN-LIMITATIONS.md) for the detailed scope, and [LANDMARK-ASSETS.md](docs/developers/ASSET-AUTHORING.md#landmark-assets) for Blender sources, lore references and rendering/contact budgets.

## Beta-v9.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v9.0)

No release description or version-specific release-note file was available when this history was consolidated. See the linked release/tag for the archived files.

## Beta-v8.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v8.0)

No release description or version-specific release-note file was available when this history was consolidated. See the linked release/tag for the archived files.

## Beta-v7.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v7.0)

No release description or version-specific release-note file was available when this history was consolidated. See the linked release/tag for the archived files.

## Beta-v6.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v6.0)

No release description or version-specific release-note file was available when this history was consolidated. See the linked release/tag for the archived files.

## Beta-v5.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v5.0)

No release description or version-specific release-note file was available when this history was consolidated. See the linked release/tag for the archived files.

## Beta-v4.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v4.0)

### Published GitHub description

The Ringworld panel now has Expedition / Settings tabs (Alt+R shows the panel). Clear air transmits the ring and Sun: the previous nearly opaque daytime sky overlay has been removed. Cloud amount, moving weather fronts, haze and shadow-square day length are adjustable.

Terrain horizon distance reaches 160,000 km, with 8/16/32-segment quality levels and a laptop preset. Coarse distant blocks use scaled space; nearby ground retains collision detail. This is not 160,000 km of detailed ground or buildings. New worlds use warped gradient noise and spatial colour textures. Blank configured seeds generate once per new save and are then persisted. Old expeditions preserve their original seed and terrain algorithm.

Read [settings and horizon details](https://github.com/theplatecrafter/Ring-World-KSP-mod/blob/main/docs/SETTINGS-AND-HORIZON.md) for controls, migration and limits. World-generation controls lock after the first expedition; rendering and weather remain adjustable. Laptop frame rates have not been certified.

## Beta-v3.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v3.0)

### Published GitHub description

Adaptive terrain LOD replaces the sparse distant backdrop with nested blocks out to about 2,000 km. Seeded mountain ranges, foothills, dunes, variable-width rivers and lakes, road corridors, forest clustering and irregular rural buildings add more regional variation. Distant geometry takes several seconds to build after relocation. The same height function drives distant terrain and physical ground.

Camera clearance now includes water. EVA chase orientation uses the ring frame, and the stock flight altimeter shows clearance above ring ground/water without changing orbital altitude. Clouds use an advected procedural opacity texture over uneven layers; twenty shadow squares drive the configured three-hour light cycle. See [terrain and LOD notes](https://github.com/theplatecrafter/Ring-World-KSP-mod/blob/main/docs/TERRAIN-LOD.md) for design, source references and remaining visual limitations.

Terrain away from named-site pads changes in this version. An old wilderness surface save may need relocation above a destination to avoid intersecting the new terrain. EVA walking transitions now use local horizontal velocity instead of the stock Sun-relative speed cache. Ring navball markers and speed use the same local physics frame; see [validation](https://github.com/theplatecrafter/Ring-World-KSP-mod/blob/main/docs/VALIDATION.md) for real-key and rapid-reversal test results.

## Beta-v2.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v2.0)

No release description or version-specific release-note file was available when this history was consolidated. See the linked release/tag for the archived files.

## Beta-v1.0

[Published release](https://github.com/theplatecrafter/Ring-World-KSP-mod/releases/tag/Beta-v1.0)

No release description or version-specific release-note file was available when this history was consolidated. See the linked release/tag for the archived files.
