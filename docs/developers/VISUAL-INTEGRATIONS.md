# Mod integrations and v1.1.5 work plan

This page retains the **v1.1.5 integration plan and dated development tests**. Current user documentation describes v1.1.7; historical test dates and version limits below remain evidence for those builds. An installed framework, an implemented adapter, and an installed-mod test are different levels of support. See the [7 October Parallax Continued research](PARALLAX-INTEGRATION.md) for the current custom-terrain integration proposal.

## Installation and automatic behaviour

Harmony remains required. Cyla and other visual mods are installed separately; their code, textures and shaders are not bundled. Ringworld should automatically use a supported installed integration. Individual rendering budgets still follow Ringworld quality presets. A custom profile selected in another mod takes priority over an automatic default.

## Compatibility matrix

| Mod / CKAN package | Role and Ringworld behaviour | Evidence / remaining work |
| --- | --- | --- |
| Cyla | Optional cylindrical atmosphere. Original remains the fallback; Slow and below use Original by default. | Existing integration; v1.1.4 locally corrected optical precision. NVIDIA/Proton confirmation and distant sampling bands remain pending. |
| TUFX | Automatic Economy/Balanced/Cinematic profiles in exterior ring flight when the player uses Default-Flight. Custom profiles remain selected. Live clouds and atmosphere are composited before TUFX post-processing. | Installed TUFX 1.1.1: entry, map isolation, return, manual selection and live render insertion passed. Frozen-photo composition still needs validation. |
| Scatterer | Installed custom Sun flares follow Ringworld panel daylight. Stock planets retain their Scatterer effects. | Installed 0.0878: ring night/day material mask and map isolation passed. This does not add Scatterer oceans or spherical scattering to the ring. |
| Deferred + Shabby | Deferred lighting can improve stock/part materials; local Ringworld Standard materials already provide a deferred pass. | Installed Deferred 1.3.5.0 + Shabby 0.4.2: flight camera confirmed DeferredShading with blue sky and foreground intact. New local terrain shader supplies forward/deferred passes; custom sky/water remain separate. |
| Waterfall + StockWaterfallEffects / RestockWaterfallExpansion | Engine plumes, using a matching engine configuration pack. Ringworld supplies part atmospheric density and vessel Mach. | Installed Waterfall 0.11.0: actual density and Mach controller methods returned ring atmosphere values. Real configured engine plume appearance still needs testing. Waterfall Core alone does not supply effects for every engine. |
| ReStock / ReStock+ | Replacement stock part art / additional parts; independent of ring terrain. | No geometry adapter needed for ordinary stock part rendering. Ring landing/EVA tests with these assets pending. |
| EVE Redux | Spherical planet cloud layers. Ringworld Clouds now replaces the old local volume model with an original cylindrical extension. | Public CloudsObject, VolumeManager and VolumeSection source inspected: body registration, local up and particle placement use a sphere. Reusing this renderer requires an adapter/fork, not a normal configuration. |
| Raymarched volumetric clouds | Volumetric planetary clouds, distinct from simply installing EVE Redux. | No automatic ring geometry adapter. Check the actual provider/version and distribution licence; do not bundle private shaders/assets. |
| Parallax Continued | PQS terrain/scatters, requiring its separately installed dependencies/assets. | No working ring adapter. The [source audit and biome/LOD design](PARALLAX-INTEGRATION.md) recommends a custom terrain provider or Ringworld placement feeding the installed evaluation shaders. A planet-name config alone is insufficient. |
| BetterTimeWarp Continued (`BetterTimeWarpCont`) | Custom stock warp-rate tables. Ring landing checks and configured rate ceiling remain authoritative; physics warp remains unsupported in the ring frame. | Installed 2.3.14.2: custom table, rate ceiling and airborne rejection passed. Live-table clamping also rechecks during warp. Installed-mod landed warp/unwarp regression also passed (about 496 s UT, 2.83 mm anchor drift). Slow motion/lossless physics and resource-mod combinations not certified. |
| Trajectories | Atmospheric/orbital prediction around celestial bodies. | Ringworld retains its own prediction. Upstream spherical altitude, atmosphere and PQS queries require a dedicated solver adapter; no claim of accurate ring impact prediction through its API. |
| Firefly | Reentry effects with per-body profiles. | Source checked: AtmoFxModule rejects bodies without atmosphere, checks spherical atmosphereDepth and selects profiles by mainBody. Needs scoped ring air, altitude and profile adapters. No Sun atmosphere mutation or compatibility claim. |
| VaporCones | Condensation effects. | CKAN package located; provider/version and environment-query audit pending. Do not conflate the separate VolumetricVaporCones part mod with CKAN VaporCones. |
| PlanetShine / Distant Object Enhancement | Planet light / distant-object visibility. | Planet-oriented lighting and body lists need investigation; do not treat the Sun as the ring surface. |
| Kopernicus | Celestial body and PQS infrastructure. | Not a drop-in cylindrical renderer. Keep the working rotating frame; a migration would need a separate physics, persistence and terrain design. |

## Test record

2026-09-23, `RingworldSmoke-20260923-092704.log`: TUFX 1.1.1, Scatterer 0.0878, Shabby 0.4.2, ModuleManager 4.2.3, ClickThroughBlocker 2.1.10.23, ToolbarController 0.1.9.14. Slow terrain, Adreno/D3D11. An isolated 16-view/1-light, one-eighth-resolution Cyla probe exercised ordering without raising the terrain preset. No non-finite optical samples. Screenshot reviewed: blue sky and foreground vessel retained. The scripted test passed automatic profiles, map restore, manual override, render insertion and Scatterer night/day (0/1) and map isolation. Existing stock alarm-clock fixture and Scatterer shutdown exceptions were observed; this is not a clean whole-session compatibility certification. Kerbal portrait noise remains visible in the frozen fixture and must not be called fixed.

2026-09-23, `RingworldSmoke-20260923-213155.log`: combined TUFX/Scatterer/Deferred/Waterfall test passed. Environment API scope passed. Waterfall density controller 1.092155 from local density 1.187884 kg/mÃ‚Â³; Mach 0.04802244. These are controller checks, not a plume-image test.

Global cloud probes passed: 49.85% field coverage, visible underside (mean luminance 0.645), exterior hull leakage 0, local handoff and night dimming. The shared cloud field now morphs smoothly between seeded weather epochs as well as moving with the wind. Low and high distant-surface shader probes both contain land and ocean colours.

## Release work checklist

- [x] Research CKAN identifiers and upstream integration interfaces.
- [x] Implement and exercise automatic TUFX profiles and Scatterer panel-flare mask.
- [x] Verify startup/flight with all optional mods absent; restore the development instance after the test.
- [ ] Complete profile scene-reload/photo lifecycle and global graphics-setting restoration checks.
- [ ] Validate TUFX photo output and scene-depth preservation.
- [ ] Exercise Deferred and Waterfall with actual installed binaries.
- [ ] Improve distant terrain/clouds and transitions listed below.
- [ ] Test and document performance on laptop presets; no unverified FPS promises.
- [x] Prepare local NetKAN suggestions for Cyla, TUFX, Scatterer, Deferred and BetterTimeWarp. Not submitted; remaining validation below still applies.
- [ ] Publish only after explicit release authorization. Do not change the published v1.1.4 archive.

## Whole-ring visuals requested for this development cycle

- [x] Keep coarse seeded land/ocean shading enabled at low presets too; high detail adds climate samples. GPU image probes passed. This is a coarse appearance layer, not a newly simulated full-ring height mesh.
- [ ] Validate the new 60Ã¢â‚¬â€œ200 km local-lighting-to-distant-colour blend against the rectangular edge in Ringworld-20260923-134023-469.png. Shader compiles and renders with Deferred, but the exact zoomed-out reproduction has not been approved visually.
- [x] Shared seeded cloud-field evolution and wind advection; GPU morphology test passed. Further cloud lighting/detail remains a visual-art task.
- [x] Render cloud-sheet underside while retaining exterior hull occlusion; use configured volume range for shared handoff instead of hidden 180 km cap. Isolated coverage/handoff tests passed; long-distance flight review remains.
- [ ] Improve cylindrical water reflection/refraction/wave shading. Assess what can reuse installed mods and what needs a Ringworld renderer; ray tracing is not a prerequisite.
- [ ] Assess EVE local volumetrics or a geometry-aware adapter before promising EVE on the ring.
- [ ] Retain earlier unresolved items: high-altitude Cyla banding/jitter, affected-GPU validation, custom/stock Sun-disc occlusion, optional-mod coexistence and photo mode.

## Sources and geometry boundaries

Planet packs normally register a real celestial body and provide per-body configs, textures and sometimes precomputed scattering tables. Ringworld is a rotating cylindrical mesh and local reference frame, so assigning those configs to the Sun would affect the wrong object.

- [CKAN recipes](https://github.com/KSP-CKAN/NetKAN/tree/master/NetKAN), inspected at 981ee6fc5a1cf94721a8700a2adc0c1f982591de.
- [TUFX loader and profile APIs](https://github.com/KSPModStewards/TUFX), inspected at 3dc4b1ad6d38a6dd8670be576d4836c241464857.
- [Scatterer planet configuration](https://github.com/LGhassen/Scatterer/wiki/PlanetsConfig) and [Sun-flare implementation](https://github.com/LGhassen/Scatterer/blob/c2d0b0f2a8798381040d3a4e735a00cad957eb47/scatterer/Effects/SunFlare/SunFlare.cs).
- [Deferred compatibility and stencil conventions](https://github.com/LGhassen/Deferred/blob/master/Readme.md).
- [EVE raymarched cloud configuration](https://github.com/LGhassen/EnvironmentalVisualEnhancements/wiki/Raymarched-cloud-configuration).
- [Parallax Continued](https://github.com/Gameslinx/Parallax-Continued).
- [Waterfall](https://github.com/KSPModStewards/Waterfall) and [current CKAN Trajectories source](https://github.com/linuxgurugamer/KSPTrajectories).

Ray marching integrates density and light along a ray; it is not hardware ray tracing of arbitrary surfaces. Cyla supplies atmospheric scattering. Ocean reflections/refraction and cloud lighting still require their own rendering paths.

## Utility and physics integration checklist

- [ ] BetterTimeWarp custom tables, airborne rejection, landed warp/unwarp, rate-table changes during warp, save/reload and optional-mod absence.
- [ ] Trajectories: cylindrical atmosphere/terrain and rotating-frame solver design; accurate ring impact and post-encounter trajectories. Do not substitute stock Sun predictions or silently describe them as supported.
- [ ] Kerbal Engineer / MechJeb instruments: check surface/vertical speed and ring altitude; autopilot/landing solver needs separate validation.
- [ ] kOS, alarm clocks and resource/background-processing mods: expose ring environment and validate packing/UT semantics before compatibility claims.
- [ ] FAR and Principia: separate aerodynamic/gravity architectures; no support claim without a dedicated adapter and tests.

Utility sources: [BetterTimeWarp Continued](https://github.com/linuxgurugamer/BetterTimeWarpContinued) at c802b4d9398547099da4128839d63d27a1612cc8; [current Trajectories fork](https://github.com/linuxgurugamer/KSPTrajectories) at d0ceacd0174474706c9e564a9f6fc9f6b207cffd. [Firefly](https://github.com/M1rageDev/Firefly) inspected at df228d8425826fe21b94f531640ee8a26aca66a2.

2026-09-23, `RingworldSmoke-20260923-220632.log`: full combined integration probe passed with BetterTimeWarp 2.3.14.2. A custom table containing 3Ãƒâ€”, 17Ãƒâ€”, 250Ãƒâ€” and 4,000Ãƒâ€” was applied via its actual API; the ring ceiling resolved by rate and airborne rails warp remained blocked by solid-contact checks. Original table and selection restored.

2026-09-23, `RingworldSmoke-20260923-221220.log`: landed stock-UI warp with BetterTimeWarp installed passed. UT advanced 495.53 s; the home planet moved 4,600.77 km; the ring resident drifted 2.83 mm after unpacking. Contact was obtained by ordinary simulated landing. This does not certify extreme custom physics-warp rates or other resource mods.

2026-09-23, `RingworldSmoke-20260923-221801.log`: optional-absence test passed with Cyla, TUFX, Scatterer, Deferred/Shabby, Waterfall, BetterTimeWarp and their optional support libraries temporarily absent. Original atmosphere flight and environment API remained available. Latest global terrain shader passed the non-flat regional-colour check; the diagnostic image was reviewed. All optional folders were restored and installed plugin/shader hashes match the current development build. No release archive was created.

## Ringworld-specific extensions (2026-09-24 development)

The first module is [Ringworld Water](../guides/RINGWORLD-EXTENSIONS.md), included with the host and independently switchable per ring. Detailed/Ultra add an original refraction shader; lower settings do not run its screen copy. This is not a Scatterer ocean adapter. Object reflections, depth-validated refraction, underwater fog and full ocean spectra remain open.

Terrain colour now feathers across 30% of the fine block beside a coarser neighbour, using that neighbour's actual colour grid. Changes in neighbour topology regenerate the affected patch. Forest filtering also transitions smoothly across its former 380 m sampling threshold. This addresses colour discontinuities without transparent ground or additional overlapping meshes. Exact screenshot reproduction and the detailed-to-global overview boundary remain pending visual validation; do not mark all LOD seams solved from a successful build.

Validation: `RingworldSmoke-20260924-005650.log` passed all five water GPU transparency probes, saved water-extension disable roundtrip, all 11 quality preset roundtrips, 512x341 and 1536x1024 photo output, and photo restoration/cancellation. Full scenes used Slow; Detailed/Ultra water were tested only on an isolated 64x64 camera. Repeated camera renders correctly distinguished red and blue submerged backings. The earlier failed probes retained previous test objects until frame end; the fixture now deactivates them immediately. Core tests passed 109,728 checks, including continuity across the forest filter threshold. Normal development binaries were restored. This does not certify high-quality refraction in every photo/third-party post-processing combination, object reflections, or the exact zoomed-out terrain boundary.

## Ringworld Clouds extension

The original fixed-band local volume shader has been removed. `RingCloudVolume.cginc` supplies cylindrical shell bounds, typed density profiles, shape/detail noise, high cirrus, curl distortion and self-shadowed phase/scattering approximations. `Extensions/RingworldClouds.cs` owns configuration and uniforms; the existing atmospheric compositor owns camera/photo resources. The saved switch and mode are independent of Cyla and of installed EVE. Slow and lower presets retain inexpensive layers. See the [extension guide](../guides/RINGWORLD-EXTENSIONS.md) for the deliberate distinction from the separate EVE products and all remaining parity work.

Remaining cloud validation: extended distant handoff visual review and movement/frame times. Ground/object shadows and advanced EVE-style features remain implementation work. Do not describe this as complete EVE Redux or V5 compatibility.

2026-09-24, `RingworldSmoke-20260924-011641.log`: replacement cloud GPU tests passed for all three volume modes (isolated 160x96 images), night dimming, foreground depth, disable/save roundtrip and above-layer visibility. Reviewed below/above images. Existing full-ring coverage remained 49.85%, exterior leakage zero; night/handoff/seeded evolution tests passed. No laptop high-preset scene or FPS claim. Diagnostic images are under `artifacts/validation/cloud-extension`.

2026-09-24, `RingworldSmoke-20260924-012127.log`: in-flight cloud enable/disable/restore routing passed, alongside all preset roundtrips, five water transparency probes and 512x341/1536x1024 Slow photo captures with restoration. Core tests passed 109,728 checks. A subsequent cirrus optical-profile selection adjustment compiled successfully but was not separately GPU-probed. Normal plugin and latest shader bundle were restored; installed hashes match build outputs. No release was created.

## Full-ring atmosphere (development)

`Extensions/RingworldScattering.cs` owns a 16,384-segment scaled-space optical shell shared by flight, map and tracking scenes. `FullRingAtmosphere.shader` uses a bounded optical-column approximation, panel phase, analytical hull occlusion and a 250-600 km local-camera handoff. It complements the local Cyla/Original backend and is grouped with water in the extension UI. No external Scatterer code or assets are included. The renderer deliberately does not claim physical volumetric parity; edge thickness and exact local/distant colour continuity need further work.

Cyla research: [upstream documentation](https://github.com/LGhassen/Cyla) describes cylindrical scattering and boundary modes. The installed bridge additionally limits the observer to 600 km altitude and caps the optical radius through ProxyRadius; these are Ringworld integration choices, not a claim that Cyla inherently cannot render distant cylinders.

Validation: `RingworldSmoke-20260924-085224.log` passed isolated scaled-camera GPU checks: daytime luminance 0.0863, night 0, nearby handoff 0, distant-from-surface 0.3856, exterior leakage 0; disabling and settings roundtrip passed. Reviewed the whole-ring diagnostic image with lit arcs and panel-night gaps. Existing global/cloud-volume checks and 109,728 core checks also passed. Installed normal plugin and shader hashes match build outputs. These probes validate the layer, not a complete end-to-end visual review of every map/photo/Cyla combination or an FPS benchmark. Initial fixture failures came from seeing the legitimately visible opposite ring during a near-only assertion, then constructing uninitialized test settings; both fixture errors were corrected before the successful run.

## Weather realism research and implementation

References inspected: [original EVE cloud volume code](https://github.com/rbray89/EnvironmentalVisualEnhancements/blob/master/Atmosphere/CloudsVolume.cs), [raymarched cloud configuration](https://github.com/LGhassen/EnvironmentalVisualEnhancements/wiki/Raymarched-cloud-configuration), [Scatterer FFT ocean](https://github.com/LGhassen/Scatterer/blob/master/scatterer/Effects/Proland/Ocean/OceanFFTgpu.cs) and its `OceanWhiteCaps.cs`. EVE's public main code is MIT (DDS loader differs); Scatterer is GPL-3.0. This pass uses original implementation and assets; no upstream implementation was copied. Public EVE particle code is not the newer V5 renderer. Reference image search found author/community demonstrations of layered clouds and precipitation; direct image retrieval failed, so these are not a verified pixel-matching target.

Changes under validation: world-space precipitation, post-cloud depth-tested particle composition (including TUFX insertion and photo output), cold-biome snow, anchored lightning, cloud-linked rain-shaft extinction, shallow dust/fog, longer cloud range with distance detail reduction and nonuniform ray sampling, and multi-direction water wave normals/specular filtering. Particle count remains bounded at 48/144/384. The graphics changes do not add weather forces or change mean water collision height.

Remaining realism work: projected terrain/object cloud shadows, cloud-transmittance-aware branching lightning, physically transported precipitation and temperature/humidity profiles, temporal reconstruction, light caches/SDF acceleration, full FFT ocean/whitecap spectra, underwater object-shadow integration, high-preset desktop benchmarks, and combined Cyla/TUFX/Deferred camera motion QA. Parallax-style Ringworld terrain is the next requested extension, not implemented by this weather pass.

Scientific caution: [rotation/climate model research](https://arxiv.org/abs/1404.4992) supports coupling rotation, convection and illumination, but concerns planetary climate, not a validated model of Niven's ring. Rim-shadow fog and desert-front weather are presentation choices. Do not market spectacular Coriolis storms or engineering-scale weather as established predictions without a ring-specific model.

2026-09-24, RingworldSmoke-20260924-181822.log: precipitation GPU probes passed for rain, rounded snowflakes and opaque foreground rejection; all three cloud volume modes, night dimming, above-layer visibility, settings persistence and full-ring coverage tests passed. Core checks: 109,732. These are isolated GPU checks, not proof that the exact reported moving-camera cloud boundary is resolved on every graphics backend.

Precipitation limitations: the current particle ceiling and bolt endpoints use default cloud-base approximations; custom cloud-type altitude overrides are not yet propagated into these endpoints. Lightning fades into clouds but does not integrate cloud transmission.

2026-09-24, RingworldSmoke-20260924-183031.log: live visual-options test passed all preset/optics roundtrips, cloud routing, five water-quality GPU transparency probes, storm photo captures at 512x341 and 1536x1024, restoration and cancellation. The larger photo was reviewed and contains rain streaks across the sky. This does not replace a moving-camera reproduction of the original cloud-boundary report. Full scenes used Slow; no desktop FPS claim. Normal development plugin and shader bundle were restored and their SHA-256 hashes match the build outputs. No release archive was created. Existing external Scatterer shutdown exceptions occurred after the successful test, in its settings/texture cleanup paths.

Water geometry now filters vertex waves against mesh spacing to avoid unresolved displacement aliasing; the fine normal spectrum remains. The GPU transparency probes cover both shader families but do not visually certify every coarse mesh transition or ocean horizon.


## 2026-09-25 cloud families and instrument audit

Ten editable cloud families now select layered/cellular profiles from local weather, with smooth low/middle/high weather blending and a hypothetical rim-shadow stratus response. Cloud-distance input no longer has a 5,000 km upper cap; a saved large value is preserved while GPU reach is bounded by habitat extent and finite arithmetic. High preset defaults are expanded; laptop defaults remain inexpensive. Precipitation ceilings and lightning tops now read configured cloud layer altitudes, superseding the default-altitude limitation noted above.

See the [cloud family guide](../guides/RINGWORLD-EXTENSIONS.md), [Parallax discussion reply](PARALLAX-REPLY.md), and [resource/KFS audit](RESOURCE-AND-DRIVE-COMPATIBILITY.md). The reports being answered are from **CKAN v1.1.3**. New scanner corrections and API v4 are development-only. KFS remains untested/unsupported; ring deposits and harvesting remain planned.

Validation (2026-09-25): RingworldSmoke-20260925-042043.log passed the combined GPU and live-flight suite. All ten cloud-family probes produced finite, nonempty images; fair/storm mean opacity differed (0.5066 versus 0.8493). Night dimming, opaque foreground rejection, above-layer visibility, disable/save roundtrip, large cloud-distance persistence, full-ring coverage/exterior rejection and scattering checks passed. Family and fair/storm diagnostic images were reviewed. The images establish distinct profiles, not V5 photorealistic parity.

The live Slow scene passed stock scanner action/display and host resource-unlock isolation, acceleration API direction checks, preset/optical-setting roundtrips, cloud routing, water transparency, storm photos at 512x341 and 1536x1024, restoration and cancellation. Core: 109,732 checks. Normal plugin, shader bundle and cloud config were restored to the development instance with matching hashes. No archive/publication or KFS runtime test. Higher cloud modes were confined to small isolated GPU targets; high-preset FPS and moving-camera long-distance quality remain unverified.

### 2026-09-25 cloud edge and rain coverage regression

Development build, not CKAN v1.1.3. `RingworldSmoke-20260925-124628.log` passed the global-cloud GPU suite after the lightweight deck radial fade, local cluster coverage and volumetric vertical-profile fixes. New deck probe verifies rainy centre coverage and transparent outer edges. All ten cloud families render; fair/storm mean opacity was 0.2032/0.8421. Distant coverage remains 0.4985. Core verification passed 110,323 checks, including precipitation beginning after the overcast threshold. Normal build restored and installed into template_instance.

The initial run's distant-coverage probe incorrectly sampled the newly detailed local field. It now calls the same macro coverage function as GlobalClouds.shader; distant coverage itself was unchanged. GPU probes are small off-screen renders. The reported exact landing location/full-resolution flight view has not been reproduced, and these results are not an FPS benchmark.

### 2026-09-25 environment/resource bridge groundwork

See [resource and drive compatibility](RESOURCE-AND-DRIVE-COMPATIBILITY.md) for current scope, config examples and optional adapter switches. Surface API v5, Resource API v1 and Terrain API v1 are development interfaces. `RingworldSmoke-20260925-172713.log` passes the live resource/environment fixtures and visual-options suite. Third-party craft tests and Parallax compute integration remain pending; do not describe these interfaces as universal mod compatibility.

### Water optics and seabed continuity (development)

RingUnderwater adds camera-depth absorption and scattered light independently of terrain tile bounds. It is enabled only while the flight camera is beneath sampled mean water, inside the ring frame, with Water extension and Ripples or higher active. Detailed/Ultra use 8/16 bounded samples of surface-modulated illumination; these are approximate shafts, not geometry-shadowed rays. Flat/off avoids the postprocess. Surface optics add broad sun glitter and angle-dependent refraction absorption. Transparent unscaled LOD water keeps ground triangles beneath it; coarse scaled-space water remains an opaque approximation. Existing physical seabed sampling and water levels are unchanged. Full underwater moving-camera, photo-mode composition and desktop performance validation remain necessary.

Validation: 2026-09-25, RingworldSmoke-20260925-181949.log passed the live Slow visual-options suite, five water transparency probes and 512/1536-pixel photo output/restoration. RingworldSmoke-20260925-182727.log passed the deterministic underwater GPU probe (fixed depth, dry bypass, absorption, day/night, finite shafts) and existing global visual checks. 110,323 core checks passed. This validates shader behavior, not reference-image parity or all moving-camera shoreline transitions. Normal development build restored; no release archive.

### 2026-09-28 water transmission and immersion

`RingworldSmoke-20260928-155426.log` passed the visual-options suite, including the underwater GPU checks, all five surface quality levels, 512/1536-pixel photo capture/restoration and live immersion physics. Above a deep seabed, near-object colour contrast was 0.839 versus 0.271 at 100 m; enabling shafts increased the measured scattered-light channel from 0.286 to 0.306. These are controlled shader probes, not an ocean screenshot comparison.

The live craft reports submerged parts and `Splashed`, sinks with zero buoyancy (-1.805 m/s in the fixture), and receives positive displacement lift when buoyancy is restored. Initial tests caught a missing vessel splash-state update; the replacement now mirrors the stock contact-to-vessel notification. Core checks: 110,323; build: zero warnings/errors. The optional SunkWorks adapter compiles and remains harmless when absent, but a SunkWorks-equipped craft has not yet been tested.

`RingworldSmoke-20260928-225932.log` passed the resident/deployed-science regression after the packing changes: normal-gravity touchdown, save eligibility, ring-specific stock science, four deployed science units with power, Space Center transition and reload. Position error was 0.000036 m. The older test's unrelated wall-renderer lookup was removed because the renderer now lives separately from the scaled root. This is not a large-base physics-range approach or EVA recovery test. The normal build was restored to `template_instance`; no archive was produced.
