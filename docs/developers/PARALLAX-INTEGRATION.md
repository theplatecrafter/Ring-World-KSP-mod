# Parallax Continued on Ringworld

Research date: **7 October 2026**. Status: **source audit plus an active, unpublished Ringworld Parallax bridge implementation; not runtime-certified for release**.

The sibling [Ringworld Parallax project](../../../Ringworld Parallax/README.md) now implements original placement/evaluation using installed Parallax models/materials. This document retains the initial design; its v1 interface descriptions below are historical groundwork. Current development adds Terrain API v2 and a first-party scatter-provider ABI. Read the extension validation/dependency notes before interpreting proposed features as completed. This proposal concerns Parallax Continued, not the older Parallax 2 implementation.

Parallax's scatter rendering is a plausible fit for Ringworld's custom terrain. The recommended integration is an optional **Ringworld Parallax bridge**: Ringworld owns cylindrical surface sampling and streaming; Parallax supplies its installed scatter shaders, assets and GPU rendering where a supported interface permits it. Ringworld does not need to become a spherical PQS planet to supply these inputs.

The Parallax developer's response supplied by the project owner supports this direction: mesh data is the essential input, while much of the surrounding infrastructure connects it to PQS. Their offer to investigate is not a promise that the current release accepts arbitrary meshes. The source audit below identifies what that investigation needs to cover.

## What the current implementation assumes

The following are observations from the publicly available `master` source, downloaded for inspection on the research date. They are not a verified binary interface for every released Parallax version.

| Stage | Existing assumption | Ringworld input or change needed |
| --- | --- | --- |
| Chunk initialization | `ScatterSystemQuadData` takes a `PQ`, reads its mesh/PQS UVs, resolves a body and samples fixed grid indices. | Mesh-independent patch descriptor, actual bounds, masks and metadata. Our default near mesh has a different grid size. |
| Chunk eligibility | PQS subdivision thresholds, mesh altitude extrema and a few biome samples select scatter families. | Physical patch bounds, cylindrical elevation bounds and conservative mask coverage. |
| Lifetime | `ScatterComponent` registers and removes data through PQ visibility/build callbacks. | Explicit ready, replaced and retired events with generation ownership. |

These are more than a constructor mismatch; populating a few public buffers leaves other PQ-dependent methods active. See [quad data](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/PQS%20Mods/ScatterSystemQuadData.cs) and [scatter entry point](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/Scatter%20System/ScatterComponent.cs).

The placement kernel uses spherical height, direction-based noise, triangle/index-based seeds and biome filtering at triangle centres. Its evaluation stage is a more promising reuse boundary: it transforms candidate positions, performs distance/frustum culling, and divides results into three object LOD buffers. Terrain tessellation changes would alter the placement inputs if we simply dispatched over every current terrain mesh. [Distribution and evaluation kernels](https://github.com/Gameslinx/Parallax-Continued/blob/master/Assets/Shaders/Scatters/TerrainScatters.compute).

CPU setup also derives population from PQS subdivision, binds a shared biome map, and refreshes planetary normals. Collider eligibility depends on maximum PQS subdivision. [ScatterData](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/PQS%20Mods/ScatterData.cs).

Biome setup builds allowed-colour textures from celestial-body attributes and corrects density for cube-sphere distortion. A cylindrical ribbon needs neither a spherical biome atlas nor that cube-sphere correction. [BiomeLoader](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/Scatter%20System/BiomeLoader.cs).

Renderer activation follows PQS body events, with one current biome texture and body-oriented registries. The bridge must have an independent rendering context so an ordinary planet unloading cannot disable ring scatters. [ScatterManager](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/Scatter%20System/ScatterManager.cs).

The drawing backend already supports three meshes/materials and indirect instancing. However, its inspected implementation uses a fixed draw bound around the world origin; ring-specific bounds and camera submission require attention. Increasing scatter distance alone is not sufficient. [ScatterRenderer](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/Scatter%20System/ScatterRenderer.cs).

## Practical integration routes

| Route | Advantages | Limitations | Recommendation |
| --- | --- | --- | --- |
| Upstream custom terrain provider | Reuses distribution, evaluation, assets and rendering; avoids duplicating internal lifecycle code. | Needs developer support and geometry-neutral placement inputs or a ring placement kernel. | Preferred long-term route. |
| Ringworld placement feeding installed Parallax evaluation/shaders | Removes spherical placement dependencies; our biome/height rules stay authoritative. Reuses genuine Parallax culling, object LOD and materials. | Requires a version-tested buffer/shader contract, separate lifecycle/drawing driver and lighting validation. Placement is ours. | Best limited proof of concept if no provider exists yet. |
| Permitted small upstream changes/fork | Can replace every spherical/PQS dependency directly. | Permission and ongoing maintenance; not a drop-in config. | Only with the author's agreement. |
| Synthetic PQ objects and Harmony patches | Might quickly exercise existing classes. | Fixed topology, PQS callbacks, body registries, shader assumptions and collision jobs remain coupled. | Avoid as the production architecture. |
| Rebuild Ringworld as PQS | Integrates with the usual planet pipeline. | Spherical terrain, reference frames and persistence would require a much larger redesign. | Not justified for scatter integration. |

The second route has a specific source-level basis: the evaluation kernel consumes local candidate positions/scales/rotations and triangle references; it does not perform the placement kernel's biome or spherical-altitude calculations. A bridge could generate that input against a stable placement mesh, bind correct matrices/up directions and use the installed evaluator. This is an **inference from source**, not a tested feature or a supported public API. The installed shader must expose the required kernel and buffer layout. [Kernel source](https://github.com/Gameslinx/Parallax-Continued/blob/master/Assets/Shaders/Scatters/TerrainScatters.compute).

Harmony can intercept managed activation and lifecycle methods. It cannot change the arithmetic inside an already compiled compute shader. ModuleManager can supply scatter/config rules, but cannot make spherical coordinates cylindrical. Neither replaces the geometry adapter.

## Ringworld's interface: useful groundwork, not yet sufficient

`RingworldTerrainApi` v1 exposes close-range top meshes, live transforms, ring IDs and double-precision along/across patch coordinates. Its IDs identify Unity-object lifetimes, not persistent placement identities. It has no far-LOD registration, readiness/replacement events or masks.

Other relevant local implementation details:

- `SurfaceStreamer` registers a ground mesh before attaching its renderer and collider. A future ready event must occur after complete initialization/publication.
- Ground appearance colours are uploaded as a texture, not assigned to the near mesh's `colors` array. Parallax terrain-colour tinting needs an explicit buffer; visual colours must not double as biome IDs.
- `TerrainLod` stages replacement blocks and publishes a completed layout atomically. Its skirts, water meshes and scaled-space meshes must not be treated as ordinary scatter surfaces.
- `RingworldSurfaceApi` v6 samples environmental state using a loaded vessel context. A scatter provider also needs sampling by **ring ID and cylindrical coordinates**, without depending on the active craft's host body.
- `Ecology`, `BiomePresentation`, `ForestCanopy` and ground cover already share some climate/forest rules. The bridge should consume these rules to keep forest silhouettes consistent.

A proposed v2 terrain-provider contract should supply:

| Data | Purpose |
| --- | --- |
| Stable ring/patch key plus terrain revision and separate generation ID | Persistent placement, cache invalidation and stale-readback rejection. |
| Along/across bounds in metres; coordinate wrap convention | Seam-safe placement and ring-specific ownership. |
| Read-only placement mesh, topology, local normals, bounds and transform epoch | GPU inputs without guessing topology or reading absolute astronomical floats. |
| Surface kind and terrain LOD; visible/staged/retiring state | Exclude hull, wall, water and crack-cover geometry; schedule transitions. |
| Cylindrical elevation and local surface-up data | Height filtering, slope, orientation and fixed-water-level placement. |
| Biome IDs, ecology weights, dry/wet, slope and exclusion masks | Consistent vegetation, shoreline and landmark rules. |
| Arbitrary ring-coordinate sampling and frame transforms | Generate independent placement patches beyond the near collision window. |
| Ready/replaced/retired events and an explicit resource lease | Keep buffers alive until drawing/readback completes. |
| Scatter-family ownership and aggregate canopy metadata | Avoid duplicate native foliage and support far forest LOD. |

This is a proposed contract, not an API already available. Keep the existing v1 snapshot API intact for current consumers.

## Biomes and asset configuration

All thirteen current labels can have explicit profiles: Ocean, Lake, River, Wetland, Grassland, Forest, Desert, Mountain, Snow, Scrith, Ruins, Rimwall and Road. Coverage should also use continuous ecology weights, since the current forests are not confined to one categorical label.

Separate **categorical biome identity** from **continuous density weights**. Use stable string IDs/config names for profiles, with sampled patch-local masks rather than a whole-ring texture. Point-sample identity masks; filter continuous density masks with border padding. Check candidates near water and exclusion boundaries individually. A triangle-centre test alone can put land plants in water when a coarse triangle crosses a shoreline.

Suggested default families:

| Environment | Scatter families | Important exclusions |
| --- | --- | --- |
| Forest/wooded grassland | Broadleaf/conifer variants, undergrowth, ferns, litter, deadwood | Water, roads, occupied structures; allow ecologically defined clearings. |
| Grassland | Grasses, flowers, shrubs, stones | Dense forest suppression and landing/road clearances. |
| Desert/mountains/snow | Scrub, rubble, exposed rock, snow-covered rock variants | Slope/height limits; distinct material profiles. |
| Wetlands/river/lake shores | Reeds, rushes, gravel and driftwood | Water-depth and shore-distance ranges. |
| Oceans/lakes below water | Sparse submerged rock/plant families if enabled | Explicit seabed ownership; ordinary land vegetation disabled. |
| Scrith/ruins/roads/walls | Sparse engineering debris or intentionally bare surfaces | Authored structures remain separate; do not fill every landmark with plants. |

An extension config can reference model/material paths from separately installed, authorized asset packs, or Ringworld-owned assets. Ring IDs select profiles; biome/ecology predicates select families within them. Unknown ring IDs or missing assets should produce actionable diagnostics and retain native fallback rendering. Existing planet packs' scatter configs can inform material and model choices, but their body/biome/altitude mappings are not automatically valid here.

Our asset authoring contract needs ground pivots, physical dimensions, normals/tangents, alpha-cutout foliage, textures and three coherent model LODs. Tree billboards/impostors need matching silhouette and colour. Better meshes help, but density, lighting, transition handling and masks are equally necessary to obtain Parallax-like forests.

## Coordinate precision and orientation

Keep placement identity and surface sampling in double-precision ring coordinates. Subtract a local double-precision anchor before uploading floats. Update live matrices after floating-origin, ring spin, inclination and moving-host changes; those changes must not regenerate the population.

For a ring-axis unit vector `a`, ring-centred position `q`, and radial component `r = q - a*dot(q,a)`, inner-floor altitude is `R - length(r)` and surface-up points inward, `-normalize(r)`. Compute absolute elevation on the CPU or supply local per-vertex elevation; do not subtract astronomical radii in a float shader. A local check gives float spacing near the standard radius of **1,024 m**, and near the full-size radius of **16,384 m**. This quantization is larger than ordinary vegetation and terrain relief.

The inspected material helpers also have spherical assumptions: one normal path points away from `_PlanetOrigin`, while the two-sided path derives up from the instance matrix. Wind uses world-position sampling. These need explicit audits for inward-facing terrain, seams and frame shifts; choosing the correct object transform alone is insufficient for every shader variant. Set context-specific inputs on ring materials rather than overwriting global planetary values. [Scatter material helpers](https://github.com/Gameslinx/Parallax-Continued/blob/master/Assets/Shaders/Scatters/InstancingShaders/ParallaxScatterUtils.cginc).

Another production constraint is orientation at the opposite pole: the inspected evaluation helper's vector-alignment formula becomes singular when target up is exactly opposite its starting up vector. Arbitrary ring orientation can encounter that case. Include near/exact antiparallel directions in the prototype; a robust upstream alignment fix or an independently authored evaluator may be required before the native-evaluator route supports every ring orientation. Its slope and range-fade helpers also need testing with inward normals and stable local coordinates. [Compute helpers](https://github.com/Gameslinx/Parallax-Continued/blob/master/Assets/Shaders/Scatters/ScatterUtils.cginc).

## Terrain LOD and scatter LOD must remain independent

The critical rule: **changing the rendered terrain mesh must not change where a tree exists**.

Use fixed, deterministic **placement patches** inside a bounded streaming region. Their keys derive from ring ID, placement schema/seed and canonical along/across cells. Candidate identity should be independent of temporary Unity IDs and rendered terrain triangle order. For the initial prototype, keep a stable tessellated placement mesh alive across visible terrain LOD changes; triangle references then remain valid. A later ring-specific GPU distributor can use canonical candidate IDs directly.

Do not multiply population merely because terrain subdivisions change. Define desired density per square metre, with climate/mask modulation and an explicit maximum candidate budget. Candidate positions sample the authoritative terrain; use projection or height blending to meet the actual visible mesh during transitions without letting objects float above coarse geometry. Preserve exact collision placement near craft.

| Viewing scale | Representation |
| --- | --- |
| Ground/near flight | Detailed Parallax object meshes, limited nearby collider proxies. |
| Middle distance | Lower object meshes, GPU culling, stable density reduction and supported billboards. |
| Beyond individual-tree range | Aggregate forest canopy/coverage derived from the same masks and asset colours. |
| Map/whole ring | Biome/canopy colour and rough height; no full-ring instance population. |

Scatter loading distance can extend beyond close terrain collider distance. That requires independent placement sampling or selected medium-distance patch exports; publishing all `TerrainLod` meshes to Parallax would be wasteful. Stop individual-object work when projected sizes become tiny, then retain a recognizable forest silhouette through aggregates.

Transition ownership must prevent parent and child patches from drawing the same candidates. Stage incoming GPU data while outgoing data remains visible. Publish a complete replacement only when its buffers/materials are ready. Add hysteresis at streaming/LOD boundaries. Where the shader supports it, use complementary dither fades; otherwise a density/aggregate blend is safer than drawing two opaque forests. Current Parallax evaluation selects a single LOD buffer, so smooth cross-LOD blending must be verified or added, not assumed.

Unity's indirect drawing call does not perform per-instance culling or transparency sorting for us; the compute stage and correct overall bounds remain necessary. [Unity 2019.4 indirect-instancing reference](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Graphics.DrawMeshInstancedIndirect.html).

## Collision, lighting and coexistence

Initially render rocks/trees without Parallax colliders. Later expose a bounded collider service keyed to loaded craft/EVA proximity, with the same stable candidate IDs and transforms used visually. Grass/litter should remain non-collidable. Parallax's current collision pipeline consumes PQ-backed position/transform data and parents proxies to quads, so it needs an adapter too. [CollisionManager](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/Collision%20System/CollisionManager.cs).

Keep stock planetary Parallax registries, biome maps, textures and shader globals untouched. A ring has its own renderer context. Suppress Ringworld's native foliage only for successfully supplied families/areas; retain authored houses, landmarks, megastructures and fallback canopy. Missing or incompatible Parallax must leave ordinary Ringworld rendering usable.

Test forward/deferred lighting, night panels, body/ring eclipses, cloud attenuation, distant atmosphere and underwater tint together. Parallax's material quality cannot fix incorrect ring lighting or an incompatible post-processing path by itself.

Terrain shading/tessellation is a separate second phase. `TerrainShaderQuadData` also uses PQS subdivision, body-level materials and radial height assumptions. Scatter integration does not automatically provide Parallax's displaced terrain or scaled-planet shaders. Retain Ringworld's ground shader initially, then design geometry-aware terrain material inputs if desired. [Terrain shader lifecycle](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/PQS%20Mods/TerrainShaderQuadData.cs).

## Licensing, dependencies and extension layout

The current official README declares **All Rights Reserved** (with a named texture exception). Do not copy/reframe its source, redistribute modified shaders, or bundle its models/textures on an assumed open-source licence. The developer's technical encouragement is not an explicit redistribution grant. Request agreement on the bridge interface and intended runtime asset usage; request separate permission for any source/shader changes or asset redistribution. Own assets remove the asset-copy requirement, not the rights attached to Parallax code. [Official README and licence](https://github.com/Gameslinx/Parallax-Continued#license).

Keep any future bridge separate from the base mod, following the existing extension-control model. The base remains usable without Parallax. Enable a supported installed bridge by default, expose its budgets/toggle in Extensions, and fall back with a clear reason if required capabilities are absent.

Current official CKAN metadata makes `ParallaxContinued` depend on `ModuleManager`, `Kopernicus`, `KSPBurst`, and its Terrain/Scatter/Planet texture packages; it recommends Deferred and conflicts with older Parallax packages. A bridge depending on Parallax through CKAN inherits those relationships unless the upstream packaging changes. Own Ringworld models do not cancel existing transitive CKAN dependencies. No bridge CKAN identifier or version constraint is established yet. [Official NetKAN definition](https://github.com/KSP-CKAN/NetKAN/blob/master/NetKAN/ParallaxContinued.netkan).

The README currently directs Linux users to Windows KSP through Proton/DX11. Verify actual installed shader/API capabilities and GPU readback support; do not promise native Linux or every GPU backend. There is no installed Parallax package in the shared development instance at the time of this audit. [Platform instructions](https://github.com/Gameslinx/Parallax-Continued#installation-instructions---linux).

## Smallest useful proof of concept

1. Agree on a supported entry point and authorized assets; select one exact installed Parallax release for the experiment.
2. Render one static grassland placement patch with a rock and a tree family. Start with Ringworld-owned meshes if asset reuse is unresolved.
3. Supply ring altitude/up, dry-land masks and camera matrices; verify the native evaluation/material path without synthetic PQ objects.
4. Expand to a forest/grassland/shore boundary with three object LODs. Keep stable positions as terrain blocks split/merge.
5. Add lifecycle leases, ring-specific contexts, native-scatter suppression and bounded streaming; only then add colliders and optional terrain shading.

Measure main-thread time, GPU time, candidate/visible counts, draw counts, resident buffers and generation spikes. Frustum culling reduces drawing, not all generation/memory costs. Do not generate a dense full ring. Validate on this laptop at **Slow or lower**; higher-profile desktop performance needs separate hardware evidence. No FPS target has been demonstrated.

Required acceptance cases:

- Deterministic placement across unload/reload, wrap seam and terrain LOD changes, without duplicate forests or exposed streaming holes.
- Every biome profile, ecological clearings, shore/water boundaries, steep slopes and structure/road exclusions.
- Tilted/reverse-spin/moving-host rings and multiple ring IDs without planetary-global contamination.
- Surface-up directions parallel, perpendicular and antiparallel to the evaluator's starting up axis; no NaN instance transforms.
- Floating-origin changes, teleport, vessel switch, pause/warp, photo cameras and Flight/map/Tracking transitions.
- Stale GPU readback during retirement, empty populations, missing assets, unsupported versions and bounded append-buffer capacities.
- Collider/visual agreement near craft; no collision work for map-only vegetation.
- Ordinary Kerbin/other planet Parallax still works, with Clouds/Scattering/Cyla and forward/deferred paths tested explicitly.

## Focused follow-up for the Parallax developer

> We can provide immutable placement-patch meshes, live transforms, stable ring/cell IDs, cylindrical elevation/up data and patch-local biome/ecology/exclusion masks. Our rendered terrain LOD changes independently, so we would keep placement patches stable and aggregate forests at large distances.
>
> I checked ScatterSystemQuadData, ScatterData and the compute stages. The remaining dependencies include PQ subdivision/lifecycle, spherical height/noise, shared body biome state and PQ-backed colliders. Is a custom terrain-provider constructor feasible, with those operations supplied by a provider?
>
> If that is too much for an initial experiment, would you support us supplying candidate positions to the installed Evaluate kernel and instancing shaders, while Ringworld owns distribution and lifecycle? Which kernel/buffer/material contracts would you be comfortable supporting, and may our configs reference separately installed Parallax scatter assets? We would not bundle your code or assets. We can supply a minimal rotating-cylinder scene and one patch for your investigation.

No message has been sent automatically. Source snapshots and their hashes are recorded under `artifacts/integration-research/ParallaxContinued/SOURCE-MANIFEST.json` for local research only; they are not release content.
