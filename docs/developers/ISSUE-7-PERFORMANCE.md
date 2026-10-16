# Issue #7: approach exceptions and CPU work

Local development changes, not included in the existing 1.1.9 or modpack ZIPs. No remote issue/repository changes or new release packaging are authorized by this work.

[Report](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues/7): the reporter observed repeated trajectory exceptions during a Kerbin-SOI approach, and sustained low FPS beyond the exception interval. On 14 October the owner supplied `Ringworld-issue-report-pack.zip`, containing the surviving Unity Player.log and BDAtest saves. All four images in the new comments were viewed. The two symptoms must not be treated as a single proven cause.

## Confirmed faults and corrections

### Final orbit patch slots are not future conics

The old `OrbitRelative` followed `nextPatch` after `EndUT` without inspecting `patchEndTransition`. KSP retains an uninitialized next slot after a FINAL conic; its `referenceBody` can be null. The old ID helper then dereferenced that null body. A native KSP fixture reproduces precisely that chain.

Sampling now stops at FINAL/INITIAL conics and follows actual transitions. Missing/uninitialized future patches, invalid orbit elements, non-finite query times and cyclic patch chains return unavailable from the `Try` path. Encounter checks and the camera-time trajectory head use this path; prediction ends at an unresolved transition instead of inventing an orbital parent or repeatedly throwing. A valid next conic is sampled in its own SOI and transformed relative to the selected ring host. Persistent asteroid anchor GUID behavior remains intact.

### Expensive work must not scale with a bad frame rate or flight path length

- Relevance scanning yields at a 1.5 ms CPU slice. Numerical prediction also uses a fixed slice instead of increasing to 8 ms when FPS is already poor. Numerical integration steps, sample limits, encounter roots and camera-time interpolation/projection are retained.
- Ordinary 1x Tracking Station updates do not rescan every uncontrolled orbit just to decide whether to stop warp that is already stopped. Explicit warp requests still run the guard.
- Nearby tiles are sampled across frames, keeping full vertex resolution and biome/color generation. Incomplete roots stay inactive and out of `ChunkReady`; collider, color texture, water and scenery finish before publication. A tile's construction chart is frozen across yields and its finished transform follows the live chart. Immediate relocation completes its collision footprint synchronously. A missing central collider near the ground also receives immediate priority.
- Keep near tiles for the current footprint, the pending LOD generation's hole and the still-visible LOD generation's hole. Retire obsolete tiles even when fast motion prevents the current footprint from finishing. The bound is three footprints, plus at most one unpublished job; it does not grow with flight duration. Do not retire a visible hole's tiles until that LOD generation is replaced.
- Finish the pending LOD hole rather than indefinitely waiting for a continuously moving current footprint. Abandoned current work does not starve that pending hole. At large jumps per update, prioritize useful pending work over repeatedly constructing and canceling obsolete current tiles.
- Photo readiness includes pending near work. It must not capture an incomplete near terrain generation.
- Celestial relative sampling reuses path storage and stops before the common ancestor, without allocating per-sample dictionaries, sets and ID strings. Exact double-precision relative arithmetic is retained, including distant systems. Gravity-source records are values rather than newly allocated tuples.
- Reuse synchronous Rigidbody deduplication buffers; avoid repeatedly resolving the same vessel/root center inside the part-force loop. Do not cache live physics across ticks or replace a third-party integrator's calculation.
- Remove boundary/root arrays from `TimeToArrival`. The same stable quadratic formula, margins, inclined coordinate chart and root checks are used.

No preset values, texture/shader assets, biome distributions, terrain generators, ring geometry, cloud/water effects, scatter densities or draw distances are reduced by these changes. The generation budget limits CPU scheduling, not final mesh detail. A single native collider upload/cook or scenery publication cannot be preempted; a timing slice is not a hard limit on every Unity operation.

### Repeated fallback cloud illumination caused a separate CPU bottleneck

The received save's atmosphere backend is the basic renderer. Its three 49-by-49 cloud decks previously repeated terrain/noise sampling and physical eclipse queries for each layer and across row. Deck lighting actually uses `Daylight(along,time)`: the identical input was evaluated 7,203 times per rebuild. Fast travel triggers rebuilding after twelve kilometres, potentially every frame. Native run reporter-5 measured atmosphere updates around 550 ms during this phase, independently of the corrected trajectory code.

- Evaluate the same illumination input once per along column (49 evaluations), and terrain/noise once per shared column position (2,401). Preserve every deck vertex, colour, UV, mountain mask, fade, resolution and rebuild distance.
- Retain cloud vertex/colour/sample buffers, immutable UVs and triangle topology. Upload only live mesh data on relocation. Remove thousands of temporary per-quad index arrays; reuse the CPU cloud-texture buffer when the shader fallback is required.
- Resolve the illuminating source in the current ring chart once per sky bake. Keep the original 32 integration steps, directions, coefficients and 0.2-second refresh interval. Skip only sky/cloud work whose layer is already disabled.
- Conservatively reject bodies/panels that cannot intersect any ray to the luminous star disk before running the original eclipse tests. Shell segments entirely inside the convex radial hole cannot hit the hull. All eight star samples and actual body/ring/panel intersections remain in use. A native comparison across 160 positions, including 60 shadowed positions, matched the unfiltered visibility exactly. Sky radiance/opacity comparisons passed across 80 directions; all 7,203 cloud vertices/colours/UVs/coverage matched the original generation formula.
- Index coarser LOD neighbours by quadtree level rather than scanning the entire layout repeatedly for each block. Cache each generation's boundary topology and preserve the original neighbour/blending order. Core equivalence checks include negative and distant coordinates, the saved 160-million-metre range, and fractional community-config tile sizes.

## KSP Community Fixes composition

Ringworld's `RingOrientationPatch` is a postfix, not a replacement or skipping prefix. It republishes ring-relative surface fields after the original/KSPCF calculation and retains stock orbital data. The actual installed KSPCF owner is checked in the native regression. Current upstream [FlightIntegratorPerf source](https://github.com/KSPModdingLibs/KSPCommunityFixes/blob/master/KSPCommunityFixes/Performance/FlightIntegratorPerf.cs) also shows why preserving its calculation matters: it computes mass, center of mass, velocity and inertia. Owner names alone do not establish a conflict.

## Support diagnostics

Use either `-ringworld-profile` in KSP's launch arguments, or uncomment the optional `RINGWORLD_DIAGNOSTICS` node in `GameData/NivenRingworld/Diagnostics.cfg` and restart. ModuleManager/community configs can supply the same node from anywhere in GameData.

`[NivenRingworld] PERF` aggregates CPU milliseconds/call counts/maxima every ten seconds for flight update/late update, forces, gravity, surface streaming/positioning, near generation, LOD, relevance, prediction, map draw, surface-cache publication, packed coast, visual preparation, weather, atmosphere, sky baking and cloud baking. It also records frame count, map/warp state, loaded part count, near/LOD queues, managed-memory size and collection counts. Sections may nest: do not sum all totals as an independent frame cost. Coroutine suspension time is excluded. It measures Ringworld CPU work, not GPU time or all other mods. Disabled diagnostics do not write timing messages.

Ask the reporter to repeat the approach, map switch and looking-away comparison with the development fix and diagnostics enabled after the owner packages/publishes it. The received Player.log has no per-frame CPU timings. A fresh diagnostic log is needed to confirm the remaining cost on that exact mod stack. Do not close the issue solely on a derived workload or claim a universal FPS guarantee.

## Verification record

Core checks: 143,203 passed, including 10,000 exact-result comparisons with the previous arrival calculation on inclined, high-speed trajectories, a no-allocation arrival hot-loop test, and exact LOD-neighbour/blending-order comparisons. Normal and smoke builds pass with no warnings/errors.

Native regression script: `tools/VerifyIssue7Performance.ps1`. It uses the retained private KSP 1.12.5 fixture with real KSP Community Fixes 1.41.1, FAR 0.16.1.2, AJE/RealFuels and the existing Clouds/Scattering extensions. It runs at Slow; the streaming stress fixture uses the supported 200 km minimum LOD range and radius-two near footprint, with unchanged 32-cell terrain resolution. It is a backend stress sequence, not a reproduction of the reporter's entire save/modpack.

Acceptance: reproduced old exception; 1,000 safe corrected samples; pending/cyclic/null query rejection; real Kerbin-to-Sun transition; all installed body ephemerides compared with the previous generic calculation; real KSPCF override retained; prolonged eight-kilometer-per-update streaming with bounded tiles and LOD publication; visible holes fully covered; yielded mesh/color equivalence during chart rotation; immediate collision readiness; native map/tracking projection and cloud/atmosphere placement; landed trajectory suppression. Native run 3 passed all these issue-specific acceptance checks. Its fast-motion segment completed two LOD generations in 716 updates, with a peak of 50 published near tiles against a bound of 75. The chart was rotated during the sequence and sampled published meshes/colors matched the synchronous generation. The full log is `Ringworld Modpacks/artifacts/validation-game-1ffee7f947f84b39a4c62f0cb6db6857/Issue7Performance-3.log`. The final source additionally contains the gravity timing section and a no-residents fast path for stock orbit rendering; those do not change orbital/gravity results. Retained-save and part regressions exercise the final source separately.

Development logs are retained, including failed/aborted runs. Run 1 exposed whole-tile cost and an undersized test window for LOD completion. Run 2 exposed starvation when a continuously abandoned current tile prevented selection of pending-footprint work; it was deliberately stopped and the private fixture restored. These runs are not acceptance passes. Initial whole-tile calls measured roughly 50–70 ms on this laptop; sliced work normally measured roughly 1.5–3 ms, with occasional larger native uploads/GC/JIT effects (run 3 reported maxima up to roughly 22 ms in near-generation slices). Immediate preparation also remains synchronous: this fixture recorded roughly 0.5–1.2 second surface calls around immediate footprint completion/arrival. These are deliberately excluded from the normal streaming-slice claim. These are component timings, not a measured FPS improvement on the reporter's machine.

Save/installation policy: no serialization/identifier/assembly ABI changes, no new dependencies, no NetKAN edits, no config/extension version changes. Existing worlds, layouts, generators, seeds, resident/progress/settings data and old release archives must remain intact. Run relevant retained-save/scene regressions before release preparation. No release ZIP is regenerated by this development task.


The runtime log also retains exceptions in stock `AlarmClockUIAddPane`/PQS/SpaceTracking and upstream RealFuels/Kopernicus startup/teardown paths. No targeted Ringworld trajectory/ephemeris exception storm occurred. This is not a claim that the entire modded KSP session is exception-free. Diagnostics count calls at their actual synchronous boundaries; test-only backend streaming contributes to the Surface/NearBuild/Lod section, while the flight HUD counters refer to the flight controller's own streamer.


The earlier retained/part acceptance (`StartupCompatibility-issue7-retained-35.log`) passed: actual retained formats 3/4/pre-1.1.8 format 5 with original-file hash protection, world/seed/generator/poses/discoveries/settings preservation and repeat serialization; native legacy resident restoration; real KSPCF startup/cache composition; stock Ore/Water drilling and EC consumption; stock/AJE intake/environment scope; 1,500-tick flotation, calibrated buoyancy and sinking; EVA contact/walking and final-writer surface-cache correction. Historical binary versions for all fixtures are not independently pinned, and these are not exhaustive flight checks of every historical craft. The received-evidence follow-up is recorded below. Existing base 1.1.9 and modpack 1.0.1 ZIPs are preserved.

## Received reporter evidence and paired workload (14 October 2026)

Original ZIP SHA-256: `418f425ff14d7bda35765cffab83e1ad7059c772f7451c648c42e369f1674e03`. Extracted read-only evidence is under `artifacts/issue7-reporter-08e53199aa52457e9506f4cf56b73cd7`; `save-summary.json`, `exception-summary.json`, `github-comments.json` and the supplied `evidence-extract.txt` retain the audit. The original archive and saves are not overwritten.

- The 44.2 MB Player.log contains 524 `RingAnchorEphemeris.Id` exceptions at lines 459498–469440. They reproduce the old final-conic fault. The later flight continues beyond the storm; there is no Ringworld performance diagnostic in this released log, so it cannot itself assign all later frame time to a particular subsystem.
- The approach save retains seed 823303403, generator 4, standard ring radius/width, surface density 1,000,000 kg/m², 160,000,000 m LOD range and 32-cell near meshes. The density matters: this is a numerical-gravity workload, unlike the massless-ring map probe.
- Original affected craft: 16 parts, including Tantares engines and a non-stock tank, plus many third-party module types. The reporter explicitly cautioned against loading the complete complex career as a reproduction fixture. Native timing instead uses its exact saved UT, active orbit and ring options in a private training copy with one supported stock pod. This preserves the target conditions without pretending to supply missing parts or the whole upstream mod stack.
- Initial saved-orbit stage samples an approach roughly 356 km above the radial floor; subsequent stages deliberately place the pod at 220 km and exercise a roughly 394 km/s surface-relative flight, matching the reported extreme approach-speed condition. The latter is a derived stress phase, not the original craft's exact arrival trajectory. Frame counters use real elapsed time. No preset/detail/draw-distance reduction is applied.
- `ScaleModList` messages belong to stock `ApplicationLauncher`, as confirmed by assembly string metadata. They also occur in the private fixture. Their presence alone does not establish a runaway Ringworld physics list or explain this FPS collapse.

Native logs are under `../Ringworld Modpacks/artifacts/validation-game-1ffee7f947f84b39a4c62f0cb6db6857/`:

| Workload phase | reporter-5: before cloud/sky optimization | reporter-7: after optimization |
| --- | ---: | ---: |
| Saved orbit, map | 21.1 FPS | 20.3 FPS |
| Initially stationary high arrival | 17.0 FPS | 19.1 FPS |
| Fast ring-relative flight | 1.71 FPS | 12.09 FPS |
| Fast flight, map | 18.9 FPS | 16.5 FPS |

These are single runs on the same laptop/private KSP fixture, with unchanged saved options and diagnostic settings, not statistically controlled hardware benchmarks. The faster run advances physics/builds more terrain during its real-time window: 974 LOD blocks published by the end of its fast-flight phase versus none in reporter-5 at that point. Map timing consequently includes more visible terrain and is not evidence of a uniform FPS gain in every view. The repeated roughly 550 ms atmosphere update is removed; final run sky bakes averaged roughly 21 ms instead of roughly 38 ms, and cloud bakes roughly 25 ms. Initial synchronous collision preparation still takes around one second in this fixture.

Run reporter-4 used an out-of-hysteresis arrival height and is exploratory evidence only. Runs reporter-5/6/7 use the corrected 220 km arrival and target velocity. Reporter-7 passed exact fallback-deck data comparison, finite-star eclipse comparison and sky radiance/opacity comparison, and completed without the targeted trajectory exception. Its original source save was hash-checked unchanged.

Final native `Issue7Performance-final-8.log` passed the prolonged fast-streaming/rotating-chart check: 716 updates, 50 peak near tiles against a 75-tile bound, two complete LOD publications, complete visible-hole coverage and immediate contact readiness. Native Map/Tracking camera projection, scaled cloud/atmosphere placement and landed-line suppression also passed. The spatial-index tests include fractional tile sizes; normal and smoke builds passed 143,203 core checks with zero warnings/errors.

No new dependency, metadata submission, versioned ZIP, remote issue update or publication is part of this task. The owner must package/publish these source fixes before CKAN users can receive them, and request an exact-stack diagnostic retest before treating the reporter's entire session as resolved.

Final-source retained/part rerun `StartupCompatibility-issue7-retained-36.log` also passed: original format 3/4/pre-1.1.8 format 5 options, generators, poses and discoveries; repeated migration and native legacy restoration; actual CF cache/query composition, CTT/CRP resources, stock Ore/Water harvesting, stock/AJE intake scope, 1,500-tick flotation/sinking and EVA contact/walking. The script hash-checked the retained source saves unchanged and restored the private installation. The normal build passed zero warnings/errors and was installed in `template_instance`; no test process remains running. The original report ZIP, base 1.1.9 ZIP and modpack 1.0.1 ZIP hashes are unchanged. `artifacts/issue7-validation-20261014.json` records this follow-up separately from the earlier 13 October evidence.
