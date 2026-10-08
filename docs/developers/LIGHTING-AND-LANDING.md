# Ring lighting and moving-plane landing

## October 7 report

The shared template run used Interstellar Standard (0.1 light-year), seed 745138026, Original atmosphere, Slow rendering settings, the development Parallax bridge, Parallax Continued 1.0.4 and Deferred. The Aeris 4A screenshots show approximately 164-170 m/s near terrain at along 64,135,096,458.85 m / across -53,929,581.19 m. The pilot reports landing on wheels with repeated S input for a flare.

The original KSP.log, Player.log, settings and save are preserved under `artifacts/diagnostics/landing-20261007`. Those logs contain NaN anomalies in stock patched-conic calculations after transfer; they do not contain a blocked-function stack at the reported landing freeze.

## Lighting findings and treatment

Runtime light inventory found the distant host at intensity 1, Kerbol still at 0.9, and a separate habitat light. Probe-derived diffuse lighting in Deferred compounded the terrain brightness. Controlled captures ruled out incorrect terrain colour data and TUFX as the sole cause. Disabling the Original sky did not fix the washed-out terrain.

`RingStellarLighting` scopes starlight direction, secondary-star attenuation and physical eclipse visibility to ring camera renders. A native key light supplies vessel and terrain illumination; the habitat light becomes a fallback. This retains shadows between layers instead of splitting vessels and terrain between different lights. Secondary contributions are capped against their configured AU intensity so already attenuated curves are not multiplied down again.

Deferred's planetary probe-derived diffuse contribution is omitted for ring views. Its legacy/stock ambient term and specular reflections remain. This is a compatibility fallback for the ring's emissive hull/sky, not a new physically integrated diffuse irradiance probe. Every altered intensity, light pose and shader parameter is restored after the camera render, including nested captures. No star configuration or global body property is rewritten.

Reference interfaces: [Kopernicus starlight](https://github.com/Kopernicus/Kopernicus/blob/master/src/Kopernicus/Components/KopernicusStar.cs), [Deferred reflection/ambient shader](https://github.com/LGhassen/Deferred/blob/master/shaders/DeferredKSPShaders/Assets/Shaders/Lighting/Internal-DeferredReflections.shader), [Unity 2019.4 deferred rendering](https://docs.unity3d.com/2019.4/Documentation/Manual/RenderTech-DeferredShading.html). The adapter is original project code; upstream implementations are not bundled.

## Flight safeguards

Stock conic solvers can be recreated during unpacking or debris initialization. They must not solve rotating-frame velocity as a Keplerian orbit. The scoped Harmony guard skips their updates for registered ring participants. Transfer also publishes inertial bookkeeping immediately after setting local velocity. Kepler sampling requires finite orientation, anomaly, epoch and mean motion, together with a compatible SMA/eccentricity sign.

Near terrain previously accumulated until the entire distant LOD generation finished. Retirement now preserves only the current near footprint and the old LOD generation's near hole. A paused renderer traversal across 20 tiles tests this with the distant queue still busy; it does not modify gravity during an approach.

## Regression runner and limits

`./smoke-test.ps1 -MovingLandingOnly` uses the currently installed ring configuration and the report's seed/coordinates. It launches a fresh stock Aeris 4A, deploys gear, teleports 150 m above terrain, and performs a 170 m/s approach with crash damage enabled. `-GentleLanding` adds bounded pitch input through the stock fly-by-wire callback. This is an approximation of human input, not a replay of the pilot's exact keystrokes. Engine thrust is off; gravity remains 9.72 m/s². Hard-impact survival means the simulation remains responsive, not that the plane survives undamaged.

The isolated test save and screenshots do not alter the user's saved expedition. Shader/DLL builds and runtime verification are local; nothing has been packaged or published. The exact manual wheeled-landing freeze still needs confirmation in the updated normal build.

## Recorded checks

- Base core suite: 110,581 checks; smoke DLL and D3D11 visual shader bundle compile.
- `MovingLanding-lighting.log`: 1,101 frames through a moving impact, no conic NaN spam; guard and invalid-anomaly checks passed.
- `MovingLanding-gentle2.log`: 2,502 frames through impact; initial pilot approximation. This test preceded the stock fly-by-wire input correction.
- `MovingLanding-final.log`: 1,932 frames through impact with actual positive pitch input. Near retention peaked at 104 tiles for a 49-tile footprint during the 20 km renderer traversal, with 711 distant items still pending. This preceded the final native-key shadow-preservation refinement.
- `MovingLanding-native-key.log`: final lighting scope checks passed during the actual render; the controlled approach kept 40 parts through 7 m above terrain, reached a landed state with 39 parts, then suffered impact damage. The simulation continued for 1,848 frames. Near retention peaked at 104 tiles with 707 distant items pending. Invalid-anomaly rejection and conic-solver interception passed; conic NaN spam did not recur. Final captures retain terrain colour and vessel shading rather than the reported white/yellow washout. No intact-landing claim is made.

The shared template was restored to the normal base build and the preserved KSP settings after verification. The updated DLL and shader bundle are installed locally. No Git operations, ZIP packaging or publication were performed.

These results do not certify an intact landing, all presets, other GPUs, or the exact cause of the earlier released-build Windows hang.

## Post-crash Random terrain relocation

The later report used seed 208104817. The log records the Aeris cockpit exploding and stock KSP declining to save the dead vessel, followed by Random terrain repacking that same dead Aeris and six nearby debris. The pilot confirmed clicking Random terrain, rather than using a free-camera mod. The log ends after unpacking and has no blocked-function stack. Original logs, settings and the user's save are preserved under `artifacts/diagnostics/post-crash-camera-20261007`.

Relocation now requires a live vessel with a surviving root and part list. The panel disables relocation controls for a destroyed craft; all relocation entry points and the transfer coroutine also enforce the guard. A live cockpit survivor remains eligible. Failed requests do not select a new ring, move the frame, pack vessels or create residence records.

`PostCrash-biomes.log` repeats the moving Aeris approach at Slow with ordinary gravity, deployed wheels and bounded pitch-up input. It reached wheel contact with 40 parts, later crashed, then invoked Random terrain on the controlled dead vessel using the reported selection seed. The request was refused without packing or moving the corpse, changing the frame epoch or adding residence records. Sixty further frames ran, and the full impact observation passed at 1,929 frames. This verifies the unsafe relocation path is blocked; it does not establish a native hang stack or resolve every possible manual landing freeze.

The same installed-assets run loaded 16 Parallax scatter profiles, validated all three mesh/material LODs and checked policy coverage for all 13 biome labels. These are asset-binding and policy checks, not visual captures or performance certification for every biome. Scatterer emitted an `AtmoPreprocessor.OnDestroy` exception during application shutdown after the checks passed; that shutdown warning is separate from the tested relocation.
