# Local environment compatibility

Ringworld has a cylindrical habitat and a rotating local frame. `vessel.mainBody` remains the orbital host (usually the Sun). A body's radius, gravitational parameter, spherical coordinates, PQS and sphere of influence are not interchangeable with ring terrain, effective spin gravity or ring atmosphere. Never globally change those host-body properties or replace the body's identity to make one part work.

## Shared API

Use `RingworldSurfaceApi.TryGetEnvironmentAtPosition(vessel, worldPosition, out sample)` for a loaded ring vessel. Version 6 provides pressure (kPa), density (kg/m?), temperature (K), sound speed (m/s), atmosphere height (m), oxygen availability, ring altitude, terrain elevation/clearance, biome, surface up, effective stationary-frame gravity (m/s?), vessel surface-relative velocity, Mach, dynamic pressure (kPa), water elevation/depth/density, and air/water buoyancy per displaced cubic metre. Water elevation is NaN on dry ground: use `OverWater` first. Point Mach/dynamic pressure use the context vessel's translational velocity, not rotational airflow at an individual part.

The point query returns false for packed vessels, other ring frames and positions outside the loaded habitat region. Fall back to stock calculations then. Underwater points do not report breathable oxygen. `TryGetSurfaceState` additionally supplies ring identity, frame epoch, host body and full moving-frame acceleration. `TryGetEnvironment` supplies vessel-level weather and atmosphere. Resource abundance and science location have their own shared APIs.

Do not use a scalar planetary `GeeASL` to infer the direction of ring gravity. Effective stationary acceleration, velocity-dependent Coriolis acceleration, and physical stellar attraction serve different purposes.

## Stock consumer audit ? October 2, 2026

The reproducible IL audit found 3,590 distinct method/opcode/target references in the installed KSP 1.12.5 `Assembly-CSharp.dll` matching body fields/methods, FlightGlobals getters and selected vessel environment fields. This is a read/call inventory, not 3,590 bugs or a certification of every part. It includes orbital code, tutorials, writes and UI. The tool parses IL operands rather than searching arbitrary byte sequences.

The [stock part-module inventory](STOCK-ENVIRONMENT-READS.csv) lists matching module call sites, including already-adapted and intentionally unchanged reads.

Run:

```powershell
dotnet run --project tools/ApiInspect -- "../template_instance/KSP_x64_Data/Managed/Assembly-CSharp.dll" --environment-reads
```

| Consumer / property | Treatment and limits |
| --- | --- |
| FlightIntegrator pressure, density, temperature, Mach, aerodynamic/thermal constants | Existing scoped ring atmosphere integration; host body stays intact. |
| Stock ModuleResourceIntake oxygen, airflow, ocean and submerged inlet checks | Shared scoped bridge; all parts using this stock module retain stock shielding, orientation, Mach curve, pressure threshold and IntakeAir resource flow. No individual part-name patches. |
| ModuleEngines ocean/nozzle checks | Shared bridge preserves `disableUnderwater` and tests local water height. Stock engine pressure/density curves already read populated part fields. |
| Solar-panel underwater attenuation | Shared bridge substitutes local water height/density; star tracking remains stellar. Existing panel-shadow integration remains separate. |
| GRAV instrument | Shared bridge samples local effective gravity and removes the star-radius distance cutoff for a ring vessel. TEMP still measures part temperature; PRES uses vessel pressure; ACC retains actual acceleration. |
| Parachutes | Existing pressure/altitude bridge; stock deployment safety and resources remain relevant. |
| EVA helmets | Existing safety checks with shared ring oxygen predicate; stock pressure/temperature safety thresholds retained. |
| Wheel springs, motors, brakes, suspension | Existing scoped effective-gravity consumers. No global gravity field replacement or double application of forces. |
| PartBuoyancy and stock immersion state | Existing cylindrical water/contact integration; mean water level, not visual waves. |
| Surface resource scanner / harvester | Existing ring abundance adapter, surface resources only. Atmospheric/oceanic resource harvesters remain unsupported; intake air is a separate stock flow path. |
| Biome/science identity | Existing ring research subjects and biome adapter; host orbital identity intentionally remains visible to callers that ask for it. |
| Orbital survey/map overlays and GPS | Remaining gap: planet-index/spherical maps are not ring maps. Do not unlock or paint the Sun's resources. |
| EVA experiment temperature limits | Remaining gap: `ValidEVASituation` reads body temperature; its EVA/ladder context needs a focused experiment test before changing it. |
| ModuleSurfaceFX and firework atmospheric triggers | Remaining visual/secondary-effect gap: direct spherical altitude and body atmosphere reads need dedicated validation. |
| Cargo deployment / inventory / orbital calculations | Existing residence/cargo adapters cover selected operations. Other spherical transform assumptions require operation-specific review; replacing all radius/position reads would corrupt orbital behavior. |

The new bridge deliberately targets known stock methods. It does not rewrite every third-party DLL or assume every `CelestialBody` read describes local conditions. Missing the expected intake oxygen gate fails patch installation visibly instead of silently claiming compatibility with an unknown stock implementation.

## Third-party source checks

- [MechJeb VesselState](https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/VesselState.cs) reads global gravity, body altitude/coordinates and current-main-body oxygen in its intake prediction. Working stock intakes do **not** certify MechJeb atmospheric predictions or autopilots. Those need a vessel-state adapter and flight tests.
- [kOS BodyAtmosphere](https://github.com/KSP-KOS/KOS/blob/develop/src/kOS/Suffixed/BodyAtmosphere.cs) describes an actual celestial body, including its atmospheric flags and sea-level constants. A script asking about the Sun should still get the Sun. Ring-aware scripts need a separate vessel-local binding to the Ringworld API.
- [KFS gravitic engine](https://github.com/Angel-125/FlyingSaucers/blob/master/Source/FlyingSaucers/PartModules/WBIGraviticEngine.cs) reads vessel gravity and uses PQS/spherical terrain for some operations. Existing experimental compatibility does not imply every hover/translation/ground-following mode is supported.
- Heisenberg/Hooligan Labs have scoped airship adapters already. An actual installed airship/controller regression remains necessary; arbitrary part mods are not certified by the stock tests.

[Harmony transpilers](https://harmony.pardeike.net/articles/patching-transpiler.html) redirect known runtime reads; ModuleManager modifies configuration. Neither automatically infers whether a body read means local air, an orbital target, a survey map or a global physical constant. Shared environment sampling plus small, isolated consumer bridges is the compatibility strategy.

## Terrain publication

Replacement LOD layouts build under the frame budget while the previous complete layout stays visible. Moving the observer does not restart an unfinished generation. Publication waits for the replacement meshes and near tiles; nearby old tiles remain until that handoff. This is an opaque atomic swap, not an alpha fade: overlapping transparent ground introduces depth fighting. Two LOD generations may be resident temporarily, and high distances can delay detail updates.

## Validation

The first October 2 in-game run passed real stock airScoop intake production, atmosphere-disabled oxygen rejection, unchanged host flags, staged LOD readiness with a moving observer, and installed ReStock/SunkWorks regressions. The follow-up `RingworldSmoke-20261002-025548.log` also passed ring-relative intake-speed verification, local gravity/validity helper checks and the stock engine dry-nozzle method, with the expanded bridge installed. Solar-panel attenuation and the complete gravity-instrument UI were not separately exercised. Full stock-plane flight, all engine variants, third-party autopilots and visual transition appearance are separate validation targets; do not infer blanket support from module-level tests.
