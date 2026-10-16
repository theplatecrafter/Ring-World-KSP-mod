# Rendering performance follow-up — 15 October 2026

**Later follow-up:** the startup-warning correction deliberately changes this instance’s global KSP PQS preset from Low to High. The original paired measurements below remain valid for their original settings; see the final follow-up section for the subsequent run.

Scope: base NivenRingworld, Ringworld Clouds, Ringworld Scattering and Ringworld Parallax. Local source/build/test work only. Versions, dependency declarations, installation directories and existing ZIPs are unchanged. This follows the separate [issue #7 CPU/streaming investigation](ISSUE-7-PERFORMANCE.md).

Final acceptance: `FramePerformance-optimized-7.log`, photo run `performance-4` and native streaming/map run `rendering-final-9` passed. Normal development DLLs/bundles are installed in `template_instance`; original graphics settings and existing release ZIP hashes were verified unchanged. Machine-readable logs/results and installed hashes are in [the evidence receipt](../../artifacts/render-performance-20261015.json).

| Same development workload | Before: baseline-1 | Final: optimized-7 | FPS change |
| --- | ---: | ---: | ---: |
| Ground, basic atmosphere + Parallax | 13.54 FPS | 14.61 FPS | +7.9% |
| Ground, volumetric clouds + Parallax | 2.01 FPS | 2.25 FPS | +11.9% |
| Map | 16.43 FPS | 22.81 FPS | +38.8% |
| Ocean, volume + Parallax | 3.15 FPS | 3.34 FPS | +6.2% |

These are incremental comparisons with the previous development source, not public-release benchmarks or guarantees. The native resolution is 2880×1920, with a 1440×960 realtime volume target. Clouds/ocean remain GPU-heavy on this laptop. The final underwater sample was 3.02 FPS; the original baseline did not include that phase, so no before/after underwater gain is claimed. Timings varied across exploratory runs, and the camera-ray cloud microbenchmark was slower despite improved native whole-scene samples; details below.

## Work removed

### Base mod

`CameraRelativeRingMesh` composes ring inclination, spin and the foreign-ring frozen flight frame once per camera. Previously every vertex applied separate spin and basis transforms; foreign-ring vertices additionally resolved the scene frame and its trigonometry individually. The affected meshes include 65,536 cloud-shell vertices and 32,770 atmosphere vertices. The new loop retains double precision until the camera-relative subtraction and final float conversion. Exact unchanged transforms/observers reuse the previous upload; camera placement and material chart state still update before rendering.

Bounds come from a conservative enclosing cylinder with outward float rounding instead of another full mesh traversal. Nothing reduces topology, distance, cloud coverage or atmosphere geometry. A test-only reference comparison checks every uploaded point and bounds containment, including an inclined full-size ring and a foreign ring while the local flight frame is frozen.

Cloud noise wrapping uses one unsigned longitude remainder instead of three signed remainders. Across-coordinate periods are powers of two, so bit masks replace their signed modulo chains. Unsigned magnitude handles negative coordinates without overflowing `abs(INT_MIN)`; neighbours wrap with a comparison/mask. This is applied in both the base visual bundle and Clouds. Hashes, gradients, coordinates, seeds and weather evolution are unchanged. Frozen original GPU noise and signed-boundary comparisons verify the equivalence.

Live near/LOD water renderers register with `RingWaterVisibility`. A current-camera frustum test ignores disabled, staged and retired renderers. It does not use Unity's potentially stale `isVisible` flag. Repeated activation of already-visible LOD roots is also avoided. No terrain, collision, streaming ownership or save data is changed.

### Ringworld Clouds

Warp, distance and texture LOD are shared between cloud types at each density query. Shadow rays retain independent queries and the original illumination model. An experimental cache spanning primary/light samples increased oblique-sun submission time in run 5 (34.95 → 57.16 ms in that exploratory setup) and was removed rather than treated as an optimization.

The controller calculates the conservative altitude envelope of weather-active types using the configured cloud bands. Density queries outside that envelope return zero before evaluating coverage noise; the existing integration interval and sample locations remain unchanged. Explicit diagnostic type probes bypass that rejection. The global density/optics limits remain unchanged. Repeated identical material writes are skipped. No cloud type, erosion/curl model, texture resolution, lighting coefficient, shadow step count, preset or rendering distance is reduced.

Frozen reference shaders validate sampled density/lighting and a complete small rendering using live camera parameters. Shader compiler reassociation can produce small final linear-RGB differences; the test reports the measured maximum and applies a 1/4096 absolute lighting tolerance, rather than claiming exact RGB equality. Density tolerance remains 1e-5. Vertical and slanted sunlight, three cloud modes, rain/storm combinations and six heights are exercised. Earlier experimental runs remapped samples into the active band; that change was removed because it altered quadrature and did not improve whole-scene cloud FPS consistently.

The DLL and rebuilt shader bundle must be installed together when the owner requests packaging. `build-visuals.ps1 -ShadersOnly` rebuilds shaders using existing generated textures; a full visual build still regenerates them normally.

### Ringworld Scattering

Automatic refraction scene copies attach only when live water can intersect that camera's frustum. The visibility hook is optional and resolved once: older supported base binaries keep their previous full-copy behaviour. Explicit manual attachments remain forced, including photo/probe cameras using other layers. No view-dependent culling affects water physics or removes water geometry.

Shader asset lookups and time-independent coefficients of all twelve wave components are cached. Time, phases, sunlight and weather still update. Medium detection and underwater rendering share a terrain sample only within the same frame, terrain object and exact coordinates. Disabled full-ring atmosphere layers stop before repeated lighting setup. Above-water transparency, underwater scattering, refraction and wave budgets remain available.

### Ringworld Parallax

Patches are indexed by scatter family. A camera's six frustum planes are extracted once and reused, instead of scanning all patches for each family. Bounds enclose every candidate's existing GPU culling sphere, including rotation. A patch is rejected only when no sphere can pass the existing GPU frustum/range checks. Families with no submitted patch skip counter resets, compute finish and indirect draw submission; live culling adds no GPU readback.

The GPU still chooses object visibility, distance fades and all three LODs with the original evaluator. Biome masks, distribution, assets, population limits and shadows are unchanged. Contact checks remain independent of view culling, using the same 18–80 m safety/contact ranges. They reuse vessel storage and avoid per-candidate LINQ closures. Planning caches distances in value records and constructs full patch/contact objects only for admitted cells; ordering and capacity rules remain intact.

Development checks compare actual GPU per-LOD object counts before/after patch rejection in four tangent camera directions. The separate photo regression exercises camera-centred capture and Resume/Cancel settings restoration.

## Measurement method and limits

`tools/VerifyFramePerformance.ps1` builds all four components with diagnostic hooks, runs an owned KSP 1.12.5 process in `template_instance`, then restores normal development binaries and original graphics settings. It does not modify user saves or release archives. Extension timing hooks are absent from normal binaries, preserving older host ABI requirements.

The workload uses the same Slow preset, original instance graphics settings, ring seed, terrain settings and Parallax limits before/after. Although launch arguments request 1280×720, KSP reapplies the instance setting: run 6's added runtime counters report **2880×1920**. Do not describe those FPS figures as a 720p benchmark. Earlier runs restored the same settings but did not record `Screen.width/height` directly. The GPU is Qualcomm Adreno X1-85 under D3D11. The workload exercises a ground/basic-atmosphere view, local volume clouds, map view, ocean and underwater view. Parallax Continued and its separately installed stock assets are real. The template also contains other visual mods; this is neither an isolated renderer benchmark nor the reporter's complete affected craft/career.

Fixture overrides are identical before/after: Sun anchor; 15.3-billion-metre ring radius and 160.5-million-metre width; zero ring surface mass density; 200 km LOD range and radius-two nearby footprint; Parallax range 450 m, density 0.35 and 12,000 object limit. These are test overrides, not changes to shipped presets. The previous reporter-derived massive-ring/high-speed workload is a separate test. Terrain/scatter generation continues during sampling: the ground phase ended with 777 candidates/118 pending in the final run versus 717/128 initially. Both had 25 near tiles and 403 LOD meshes. Map reached 1,163 candidates/79 pending in the final run, so the object populations are not artificially held equal.

Frames use real elapsed time, with median/p95 frame duration, collection counts, memory change and population/queue counts logged. Terrain/scatter generation continues: a faster run can have more objects ready during the same wall-clock window. Short single-run FPS comparisons are noisy and do not prove universal speedups. Higher-preset desktop and other graphics APIs are not certified. GPU-heavy clouds can remain the dominant bottleneck on this integrated GPU.

The final 320×180 frozen-reference cloud test submits six draws per variant and synchronizes **each** draw by reading back a pixel, preventing a tile GPU from eliminating overwritten intermediate draws. Its milliseconds include CPU submission/readback; they are not hardware GPU timestamps and must not be presented as whole-game FPS. The comparison uses an explicit far linear-depth texture through the shader's photo-depth path, avoiding dependence on another camera's current depth/Z-buffer state. A second small comparison uses the current camera rays and optical/weather settings, with the same controlled far depth and explicit copies of Unity's nonserialized cloud vector arrays.

Earlier run 2/4/5 isolated costs used one final readback after six overwrites and inherited global depth state; run 5's camera-material clone also did not explicitly restore nonserialized uniforms/arrays. Those exploratory numbers are not final performance/appearance certification. Run 6 correctly refused to accept that clone as a cloud test. The final harness explicitly copies all live inputs, verifies an enabled cloud configuration and nonempty opacity, and uses controlled far depth. Native whole-game frame measurements remain separate from these probes.

## Validation and timings

`ParallaxPhoto-performance-4.log` passed an actual 512-pixel capture with visible scatter LOD counts 3/24/41; the image was inspected and contains grass and trees. All eleven preset budgets applied/restored without rendering the higher presets. Camera-centred population readiness, frozen time/pose, world/sky alignment (zero angular difference; viewport error 1.69e-7), Resume/Cancel restoration and disabled-extension bypass passed.

`Issue7Performance-rendering-final-9.log` in the private compatibility instance passed stock final-conic/SOI rejection, body ephemeris equivalence and KSP Community Fixes composition. The prolonged rotating-chart streaming check completed 716 updates, two LOD publications and a peak of 50 near tiles against a 75-tile bound, retaining visible-hole coverage and immediate collision readiness. Native Map/Tracking camera-time drawing, full-ring layers and landed trajectory suppression also passed. This fixture's installed shader/assets/mod stack differs from the all-extension timing instance; it validates the affected base streaming/scene code, not the final cloud GPU cost.

The core suite passed 143,203 checks and bridge placement/configuration passed 18,050 checks. Normal and smoke C# builds report zero warnings/errors. Unity shader builds completed; retained original-reference shaders deliberately retain signed-modulo performance warnings. They are not shader compilation failures.

Final run 7 passed all reference/functional checks. The 65,536 GPU noise comparisons were exact; 786,432 signed-boundary pairs, including INT_MIN/INT_MAX, matched. Across 331,776 density/illumination samples, maximum density and lighting differences were both **zero**. The complete 14,400-pixel camera-ray/far-depth comparison also had zero RGBA difference and nonempty opacity (maximum 0.9377). This validates the tested inputs, not every possible hardware/configuration. Primary integration sample positions and all selected budgets remain unchanged.

Four view directions retained identical GPU per-LOD scatter counts with patch rejection off/on. In those views, 1–5 otherwise-ready patches were rejected before dispatch (10–50% of the measured subset); no population reduction was applied. Water probes retained transparency, depth absorption and above-water scattering: near/far red-column contrast 0.933/0.231; green scattering 0.282 enabled versus 0.259 disabled. Visible/off-frustum/disabled/staged copy attachment checks passed.

Final controlled 320×180 cloud submission/readback costs were 43.98 → 33.81 ms for vertical light (23.1% lower) and 43.94 → 37.81 ms for oblique light (13.9% lower). The separate camera-ray/far-depth case was **10.50 → 12.25 ms, 16.6% slower**. Those use different rays/optics/weather and are not native whole-frame measurements. There is no universal GPU shader speedup claim; this latter case remains a profiling target. Native cloud phases submitted one atmosphere pass per measured frame, at 1440×960; map/basic phases submitted none.

Exploratory failures remain in the log set. `optimized-3` passed mesh/culling checks but stopped at the original 1e-5 lighting comparison (observed difference 2.76e-5 in the discarded cache variant). Run 6 stopped when the new configuration guard detected that the material-clone test was not valid. Neither is an acceptance pass. The baseline runtime completed, but its initial restoration build failed while a source edit was in progress; normal binaries/settings were restored afterward and final scripts use nested restoration guards.

The first completed comparison (`baseline-1` / `optimized-2`) measured 13.54 → 15.57 FPS in the ground/basic scene and 16.43 → 22.26 FPS in map view. The volume and ocean scenes showed no whole-game gain (2.01 → 1.93 and 3.15 → 3.09 FPS). Run 4 measured 14.73/1.76/22.98/3.46 FPS and run 5 measured 13.65/1.86/22.67/2.67. These older candidate variants and noisy samples are retained rather than selecting only the best results; use run 7 for the final source. Early isolated costs used the weaker measurement setup described above.

## Compatibility and delivery

No serialized fields, world identity, seeds, generation versions, resident poses, progress or saved preferences change. No migration/reset is needed. Normal extensions compiled against published base 1.1.9 assemblies without the development profiler dependency; exact minimum-host runtime matrices are not implied. Authoritative minimums remain Clouds ≥1.1.5, Scattering ≥1.1.8 and Parallax ≥1.1.8, with the same separately installed Parallax Continued/assets requirements. No NetKAN changes are made or required for these optimizations alone.

The owner must request new artifacts before this source work is included in distributable ZIPs. No publishing or repository operations are performed.


## Startup / overcast follow-up

After the [startup warning correction](STARTUP-WARNINGS.md), skip broad-field coverage noise when overcast weight is exactly one (seven Perlin calls per coverage query), and skip coverage noise entirely at zero cloud amount. The same function is used by the base local-cloud/precipitation field and Clouds extension. Intermediate blends, cluster field, cloud types, integration positions, quality and distances are retained. No further reduction of rendering budgets was applied.

`FramePerformance-overcast-followup-high.log` passed with all three real extension providers. The expanded GPU probe covers amounts 0/.5/.65/.8/.81/1, four longitude periods and eight noise frequencies: 393,216 GPU comparisons within 1e-6 coverage tolerance and exact scalar Perlin comparison, plus 786,432 integer-boundary pairs. Density/illumination comparisons across 331,776 samples still had zero maximum error. The complete 14,400-pixel camera-ray render had maximum RGBA difference **1.490116e-7**, with nonempty opacity. Four-direction scatter culling, scaled positions/bounds and water transparency/visibility checks passed.

Controlled 320x180 submission/readback costs versus the frozen original shader:

| Coverage | Sun | Reference ms | Current ms |
| --- | --- | ---: | ---: |
| .8 | vertical | 51.98 | 32.61 |
| .8 | oblique | 49.03 | 37.16 |
| 1 | vertical | 45.93 | 28.30 |
| 1 | oblique | 49.43 | 27.38 |

The complete camera-ray cost was 11.38 to 8.69 ms. These references include the earlier optimization work; they do **not** isolate the new coverage branch or establish an equivalent whole-game FPS gain.

Native High-PQS follow-up FPS at 2880x1920: ground/basic 14.25, volume 2.71, map 22.70, ocean 4.10 and underwater 3.58. The global KSP terrain setting differs from the original Low-PQS runs; adaptive populations and short timings also vary. No new percentage improvement is inferred by comparing those runs. Heavy volume clouds remain a laptop bottleneck.

Both shader builds and normal/smoke builds passed; core/placement checks were 143,203/18,050. Normal development binaries/bundles are restored after the workload, preserving the corrected High setting. No save fields, dependencies or versioned ZIPs changed. Evidence: [follow-up receipt](../../artifacts/startup-cloud-followup-20261015.json).
