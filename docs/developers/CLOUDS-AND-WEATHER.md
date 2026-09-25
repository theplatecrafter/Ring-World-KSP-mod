# Clouds and weather renderer

Technical reference for the shared local/distant cloud field, weather timing and day/night shading. For player controls, see [graphics settings](../guides/QUALITY-PRESETS.md). Numerical limits in older implementation notes are historical; the settings menu and current preset guide are authoritative.

<a id="global-clouds"></a>

## Full-ring clouds: seeded fBm coverage

The distant cloud ribbon remains visible from flight, other planetary views, map and tracking station. It sits at a representative 5.5 km altitude, with 16,384 segments and 32,768 triangles. Atmosphere off or cloud amount zero disables it.

### Generation and coverage

Coverage now uses an original seeded gradient-Perlin implementation with quintic interpolation, five fBm octaves (4,000 km down to 250 km), and low-frequency domain warping. Hashes use the complete 32-bit world seed. It no longer tiles the previous 512 km or 32,768 km coverage textures. Longitude repeats only at the ring's actual circumference; the across-axis hash domain exceeds the physical ring width by orders of magnitude.

Distant/fake clouds target approximately 50% coverage, measured as coverage-field values above 0.5. This is a statistical surface-area target, not a promise that half of every view is white. Smooth edges and translucent cloud shading reduce average opacity. Weather lightly shifts distant coverage; local weather is not capped at 50% and can remain clear or overcast. Across the existing handoff band, the field blends to the local weather's amount.

Integer lattice cells and fractional offsets are carried separately, with double-precision CPU origins, so local movement does not lose precision at large ring longitudes. Both renderers evaluate the same field with universal-time wind and ring rotation. In v1.1.5 the replacement volume uses a separate generated fBm/Worley shape texture; that texture no longer determines repeated large cloud banks. Distant fBm octaves are filtered when smaller than a pixel.

This follows the gradient-noise, multiscale fBm and frequency-filtering concepts in [PBRT: Noise](https://www.pbr-book.org/3ed-2018/Texture/Noise). [OpenSimplex2](https://github.com/KdotJPG/OpenSimplex2) was also reviewed as an alternative. No third-party noise implementation or art assets were copied; Perlin was chosen to retain explicit cylindrical seam handling and cell/fraction precision.

### Map layering

In the v1.1.5 development build the cloud shell is visible from both above and below. An analytic ray/hull test prevents clouds bleeding through the exterior hull; disabling back-face culling does not make the hull transparent. This fixes the distant layer disappearing for a landed observer below the cloud sheet.

The local/global handoff uses the configured volumetric cloud range (65–95% of that range), with complementary opacity weights. The lightweight cloud deck keeps its own 180 km extent. Dynamic weather also morphs the shared domain warp smoothly between seeded weather epochs, so coverage changes shape as well as moving with wind. Disabling dynamic weather freezes that morphing; wind can still move clouds.

The base ribbon renders first, streamed terrain second, transparent clouds afterward. The scaled terrain uses a small depth bias. Night shading is evaluated from the same shadow-square phase in each layer; matching shadow multipliers on terrain and clouds is algebraically equivalent to darkening their final composite. There is no extra coplanar shadow mesh to z-fight.

Distant terrain no longer has deep crack-cover skirts. The quadtree limits cell curvature error and culls outside the ring before subdividing. Map/tracking use a shallower hull mesh without the extra flight-camera safety burial. Flight retains that margin to keep the distant fallback out of the local ground.

### Validation

Build shaders with `build-visuals.ps1`. `smoke-test.ps1 -GlobalCloudsOnly` samples a 128,000 km square coverage field on the GPU, checks roughly 45–55% cloudy samples, tests that offsets of 512 km and 32,768 km do not repeat it, and verifies ring seams, day/night shading and optical handoff. `-MapOnly` also checks skirt-free scaled chunks, large-distance save roundtrips and wall camera constraints, and captures inside/outside map views. These fixtures are not a guarantee of every camera/quality combination or constant laptop FPS.

<a id="weather-and-night"></a>

## Weather, warp and night visibility — 0.9

### The cloud jumps

The old Laptop renderer rebuilt an entire cloud mesh and 384×384 opacity image once per eight real seconds. High/Ultra replaced a 128×128 weather image on the same timer. At native 1,000× warp this advanced coverage by 8,000 game seconds in one step. The GPU detail used fractional frequencies (5.03 and 17.07) on a periodically wrapped coordinate, so the wrap also introduced a discontinuity. These were implementation issues; neither observation proves exhausted RAM.

Cloud motion now evaluates universal time every rendered frame. Both the lightweight cloud sheets and volumetric renderer use a shared procedural GPU coverage field. The Laptop cloud mesh is rebuilt only after moving far enough to need a new patch; its subdivision count is reduced from 112 to 48. The eight-second texture replacements are removed. All detail frequencies tile consistently across the wrapped origin. The noise remains spatially periodic, as most tileable noise textures are; this is not a prerecorded looping animation.

Unity garbage collection can cause frame-time spikes, but collecting unreachable managed objects does not mean all physical RAM was full and then emptied. See [Unity's memory-management documentation](https://docs.unity3d.com/Manual/performance-managed-memory.html). The weather regression records frame-time percentile, GC collection count and managed-heap delta; those measurements do not substitute for a system-wide RAM/GPU/CPU profiler.

### Weather controls

Open Ringworld → Settings, apply changes, then save the game to persist them.

- **Evolving weather fronts** enables deterministic variation with universal time. It advances during native stock warp and freezes with photo mode. Fixed weather uses the baseline directly.
- **Weather baseline/cloudiness** sets the fixed state and centre of reduced-variation weather. Zero forces clear skies, no rain and no lightning.
- **Weather transition timescale**: 1/6–168 game hours; default 6 hours. This is the interval between smoothly blended seeded control points, not a repeating weather cycle.
- **Weather variation**: 0–1. Zero keeps the baseline; one allows the full generated range.
- **Storm fraction of weather range**: 0–1, default 0.25. This remaps the upper part of the weather field; it is not a promise that exactly that percentage of play time is stormy. Zero prevents generated thunderstorms. A manually fixed maximum baseline can still create a storm.
- **Visual cloud drift**: 0–100 m/s, default 8. This moves cloud features without applying a wind force to craft.
- **Rain visuals / density**: up to 48 streaks in Laptop, 144 in High and 384 in Ultra/photo, scaled by storm strength and density. Disabling rain does not remove clouds.
- **Storm lightning**: optional seeded bolt and cloud illumination, without damage. Individual events are shown at up to 10×. Higher warp suppresses individual particles and bolts/flashes because very short events cannot be represented reliably when each frame advances minutes.

A continuous seeded regional front and time-varying severity drive fair skies, cloud cover, rain and thunderstorms. All quality tiers use the same state; their rendering detail differs. In v1.1.5 rain/snow are world-space local particles composited after clouds with scene-depth clipping, with additional cloud-linked distant shafts and anchored lightning. See the extension guide for the current approximations. There is no fluid weather simulation, precipitation accumulation, physical wind, thunder audio, lightning damage or global climate model. The far ring's cloud albedo remains a coarse separate representation; it does not show every local storm exactly.

### Stars and night bands

Only KSP's galaxy cube colour is attenuated in daylight near the ring atmosphere. It fades back with altitude or local night. Ring, shadow panels, Sun and planet renderers are not hidden or masked by this change. The ordinary atmosphere and real geometry can still obscure objects along a sightline. Map view uses the stock starfield. Stock updates restore their ordinary colour outside ring flight; stock planet skies are left to KSP.

The global ring now shades twenty moving night bands even with **Full-ring surface detail** off. Both structural faces and the interior use the same longitude/UT phase as the panel motion and local daylight. Large scaled terrain LOD blocks have per-fragment longitude shading, so distant terrain no longer inherits the observer's entire day/night state. Nearby terrain and water retain the local sunlight calculation. No extra floating shadow overlay is placed over the surface, avoiding another source of depth fighting.

The broad bands are analytical masks with a soft penumbra. They are not ray-traced shadows and do not model small-scale occlusion by terrain/clouds. Flight, map, space-centre and tracking-station ring geometry use the same mask. At extreme warp, a day/night boundary can still move a large distance between rendered frames; that is finite frame sampling rather than a texture-update pause.

For a reproducible visual storm test, turn evolving fronts off and set the baseline to 100%. For clear skies, set it to zero. Re-enable evolving fronts afterward for ordinary weather.


## Replacement cloud volume: v1.1.5 development

`Extensions/RingworldClouds.cs` loads four editable cloud-type profiles and supplies the independent volume switch, mode and density controls. `RingCloudVolume.cginc` replaces the previous fixed-band density/shadow model. The atmosphere and photo compositor remain shared with Cyla integration. Cylindrical shell intersections restrict the integration interval; radial altitude uses a rationalized difference to avoid subtracting giant float radii. Camera depth clips the integration at foreground surfaces.

Modes select type shaping, detail erosion, high cirrus, curl and low-order multiple-scattering approximations. The light integral and dual-lobe phase function are original approximations, not EVE code. The coarse global cloud layer retains the shared seeded coverage field. If the volume extension is disabled while Cyla still renders, the global layer no longer cuts a hole for a nonexistent local volume.

References: [EVE Redux source and licences](https://github.com/LGhassen/EnvironmentalVisualEnhancements), [raymarched cloud configuration](https://github.com/LGhassen/EnvironmentalVisualEnhancements/wiki/Raymarched-cloud-configuration). The public Redux source inspected uses geometry-shader particles and spherical placement. The documented newer raymarcher is a separate implementation. No EVE source, shader, texture or preset has been redistributed. See the [extension guide](../guides/RINGWORLD-EXTENSIONS.md) for supported controls and remaining differences.
