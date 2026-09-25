# Reference frames and mod interoperability

## Decision: retain the ring-relative frame

The mod uses its own ring-relative frame. No native-frame adapter is enabled, and no changes are made to the Sun's rotation threshold.

Inspection of the installed KSP 1.12.5 assemblies confirmed `CelestialBody.inverseRotThresholdAltitude`. `OrbitPhysicsManager.checkReferenceFrame` compares spherical altitude above the dominant rotating body against that threshold. Switching calls `setRotatingFrame`, changes `inverseRotation`, and adjusts unpacked vessel velocities using the body's rotating-frame velocity. Centrifugal and Coriolis calculations also depend on that body flag. Local inspection outputs are development tools, not redistributed KSP source.

The ring already performs the analogous operation in `RingworldFlight`: arrival/exit converts loaded vessel positions, rotations and linear/angular velocities; saved vessel records carry frame state. Its cylindrical surface, rotation and surface acceleration cannot be represented by the Sun's spherical threshold. Changing that threshold alone would not implement a ring frame and could affect ordinary solar vessels.

## Read-only API, version 3

`RingworldSurfaceApi.TryGetSurfaceState(Vessel, out RingworldSurfaceState)` supplies surface-relative velocity, up direction, cylindrical location, terrain/water elevations, biome, frame epoch and physical tangential ring speed. Units are metres and seconds; vectors use the current Unity world axes. The API returns false for orbital/non-ring vessels, packed vessels and frame transitions. Call on the Unity main thread and refresh each physics tick. Do not use these rotating-frame velocities to construct stock Keplerian orbital elements.

Version 3 adds TryGetEnvironmentState with ring air density, pressure (kPa), temperature (K), sound speed, surface-relative Mach, daylight and weather coverage. It has the same main-thread and vessel/frame scope as the surface snapshot.

This is an opt-in integration point for instruments and future adapters. It does **not** automatically adapt an autopilot, FAR, Principia or other third-party physics system. Those combinations remain unverified.

## Toolbar and game modes

The panel uses the stock `ApplicationLauncher.AddModApplication` lifecycle, with ready/destroy subscription and cleanup. It does not assign a toolbar slot or replace another mod's button. Alt+R stays synchronized with its selected state; F2 hides the window. The panel starts closed. This follows the same stock launcher pattern used by [MechJeb](https://github.com/MuMech/MechJeb2/blob/dev/MechJeb2/MechJebModuleMenu.cs). [ToolbarController](https://github.com/linuxgurugamer/ToolbarControl) is an optional ecosystem alternative, not a new dependency here.

Sandbox exposes expedition transports and world settings. Science/Career keep information, stock-warp status and photo mode, without relocation, training, forced frame exit or world-edit controls. Automatic physical arrival and exit still work in all modes. Physical tangential speed and vessel surface-relative speed are separately labeled.

## Graphics and content boundaries

Ring shaders, materials and bundles use ring-specific names. Existing scenery registration supports external `.mu` replacements ahead of bundled fallbacks. No EVE or Scatterer configuration is injected into the Sun: their body-based systems require dedicated adapters for this geometry. See [Scatterer](https://github.com/LGhassen/Scatterer) and [Kopernicus](https://github.com/Kopernicus/Kopernicus) for the respective source projects. Shared post-processing, camera ordering and alternate aerodynamic models still require installed-mod tests; no broad compatibility guarantee is implied.

## Multiple configured rings: design status

Since v1.1.3, multiple habitats are stored in the save's `RingworldScenario` RING nodes and managed by the Sandbox editor. `NIVEN_RINGWORLD` still supplies the initial defaults; duplicating that config node does not spawn another habitat. Only one rotating physics frame is loaded at a time. Surface API v2 adds `RingId`, `RingName` and `Center` to the existing snapshot; refresh it each physics tick and use the habitat center rather than HostBody.position. See [MULTIPLE-RINGS.md](../guides/MULTIPLE-RINGS.md) for placement and lighting limits.

Future extensions should preserve deterministic frame selection, inertial transfer states and saved residents. Overlapping capture volumes, tilted rings and third-party gravity solvers need dedicated handling.

## Landing-leg and instrument adapters (1.0.1)

The Sun remains the native body, but stock ModuleWheelBase, ModuleWheelSuspension and SuspensionLoadBalancer read local ring gravity through scoped adapters. This covers the wheel gravity vector, spring/damper tuning, automatic friction and distributed sprung load. No global CelestialBody fields or Vessel.gravityForPos values are overwritten. Drift-correction direction/integral reset when the coordinate frame changes. Third-party wheel implementations that bypass these stock classes need their own integration.

CollisionEnhancer receives a ring-normal recovery direction for ring participants. Explicit relocations/chart changes refresh its previous-position cache so the discontinuity is not treated as a swept collision. Ordinary contact damage and sweep checking remain active. The stock vertical-speed gauge reads local radial velocity only while the vessel belongs to the ring; its normal response curve and warning thresholds are preserved.

Ring-surface save/warp tolerates modest attached-part solver chatter only when a one-second pose window stays within 3 cm / 0.5 degrees, the root remains below .05 rad/s and every part below .12 rad/s. Snapshots reset on frame changes, topology changes or excessive pose deviation. This is local to the ring's contact gate; it does not change Unity rigidbody damping, global physics settings or stock planetary warp.

## Water (1.1.1)

Stock planet ocean flags and Scatterer's `hasOcean` configuration belong to named celestial bodies. [Scatterer's official configuration](https://github.com/LGhassen/Scatterer/wiki/PlanetsConfig) requires a planet-list entry and per-body ocean settings. This cylindrical ring is not a spherical PQS ocean: adding a Unity water tag or setting the Sun's ocean flag would not provide a valid adapter. Neither is changed. Scatterer does not automatically shade ring water.

Other mods can opt into `RingworldSurfaceApi.TryGetSurfaceState` for `OverWater`, `WaterElevation`, `TerrainElevation`, local up and surface-relative velocity. Water render meshes are non-colliding; buoyancy/damping use the generated mean water elevation. The camera now uses submerged terrain clearance rather than the water surface. This is not stock spherical ocean buoyancy or fully tested submarine/EVA swimming integration. Do not infer stock SPLASHED persistence support from the visual water update.

All visual levels are translucent near shore, becoming nearly opaque over deep water. Flat avoids procedural fragment noise; Ripples adds filtered waves and a smooth random normal field; Waves enables displacement; Detailed/Ultra add extra noise octaves. Reflection colour approximates the sky, not boats, terrain or planar reflections. Distant scaled water remains an inexpensive surface colour; no costly global ocean simulation is added.

## Optional visual integrations

See [Visual integrations and test status](VISUAL-INTEGRATIONS.md) for supported adapters, versions tested, geometry limitations, research sources and the unpublished development checklist.
