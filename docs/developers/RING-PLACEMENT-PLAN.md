# Flexible ring placement: implementation and verification

Unreleased work after v1.1.6. This checklist describes development status, not shipped support. Keep published v1.1.6 ZIPs and tags unchanged.

## Scope

- Select an existing celestial body, or a persistent asteroid/comet vessel, as the anchor. Follow its position and velocity through time and SOI changes.
- Preserve existing free-standing ring placement and ring settings.
- Set fixed X/Y/Z inclination, artificial gravity, rotation direction and optional day/night panels per ring.
- Include celestial bodies, panels and other rings in sunlight visibility. Terrain, clouds, water, atmosphere, solar panels and sun flares should agree in local and distant/map views.
- **No body/star creation or cloning.** Removed from scope October 4, 2026.

## Implementation

- [x] Double-precision orientation, anchor kinematics and ray-occlusion primitives.
- [x] Per-ring gravity, reverse spin, panel toggle and X/Y/Z inclination with persistence.
- [x] Existing body and asteroid/comet selection. No cloning.
- [x] Separate orbital reference, anchor GUID/body name and associated illuminator.
- [x] Anchor sampling across orbit patches, velocity on frame entry/exit and acceleration in relative dynamics.
- [x] Frozen local collision chart; celestial rendering mapped into that chart.
- [x] Landed orbit bookkeeping, save/reload, moving-center trajectory display and Tracking Station encounters.
- [x] Missing-anchor snapshots and editor warning; no fallback to an unrelated vessel.
- [x] CPU finite-star visibility against bodies, oriented panels, ring floors and walls.
- [x] GPU geometry for terrain, global clouds and extension atmosphere. A dynamic data texture avoids dropping blockers beyond a fixed array limit.
- [x] Associated-star directions for local atmosphere/water, Sun flare and stock solar tracking.
- [x] Compact-Kerbin runtime regression and map visual review.

## Coordinates and persistence

`anchorId` uses `body:<name>` or `vessel:<GUID>`. Old settings migrate from `referenceBody`; offsets retain their non-rotating axes. Ephemerides are composed to their common ancestor. Prediction samples the correct orbit patch and parent body at each time.

The active Unity chart freezes its anchor position at frame entry, avoiding per-tick translation of terrain colliders and landed rigidbodies. Actual orbits remain inertial. Frame transitions add/remove anchor and rotational velocities; relative forces remove origin acceleration. Scaled celestial transforms are temporarily mapped for rendering, then restored. Actual ephemerides are never rewritten.

Unloaded landed records follow anchor motion and spin through bookkeeping. Tracking guards use the moving center at both ends of each prediction chord. Atmosphere/collision simulation still requires Flight; guards stop warp without automatically opening another vessel.

Missing asteroids hold their last recorded position relative to the saved reference body and report a warning. Future prediction samples cannot overwrite the recovery snapshot. Missing reference bodies still require restoring the planet pack.

## Lighting

`RingLighting` shares current body/ring ephemerides, with eight finite-star samples for CPU visibility. GPU rendering uses smooth spherical penumbrae and oriented ring/panel intersections. A data texture includes all registered blockers; cost grows with body/ring count. Ring holes transmit light.

Terrain, weather, solar flux/tracking and flare suppression use ring-aware illumination. Distant terrain/clouds and the matching Scattering build use the geometry in normalized ring coordinates. Original atmosphere phase direction and Cyla's emitter use the associated star rather than assuming it is at the ring center.

One star is selected from the anchor's ancestor hierarchy. Simultaneous multi-star illumination and ring shadows on stock planetary surface shaders are outside this implementation. Ring shells approximate the structural hull, not individual mountain/scenery silhouettes.

## Verification

- Core suite: 110,373 checks, including inclined geometry, ephemeris composition, finite-star occlusion, annular holes and camera bounds.
- Base/Scattering Release and Unity shader builds pass.
- `RingworldSmoke-20261004-211839.log`: remote Kerbin-following ring passed landing, saving, native warp, reload, Tracking Station and explicit Fly.
- `RingworldSmoke-20261004-213548.log`: asteroid-following ring passed the same lifecycle; CPU/GPU Kerbin eclipse, inclined hull/open-hole and missing-anchor recovery probes passed.
- Compact Kerbin testing exposed an unsafe temporary Sun orbit during teleport; staging now includes anchor position and clears the reference body's radius.
- `RingworldSmoke-20261004-214706.log`: compact ring centered directly on Kerbin passed the full lifecycle on Slow. Additional probes passed for stock solar flux, disabled/enabled panels, a ninth ring in a 40-body GPU data texture, and orbit sampling across a simulated SOI patch transition.
- Inspected `RingworldMultiRingMap.png`: the inclined compact ring is visible around Kerbin alongside the original Sun ring. This is not a visual certification of every inclination or graphics backend.
- `RingworldSmoke-20261004-215551.log`: final source passed the full compact-ring run and an automatic arrival from a real Kerbin-referenced vessel; residual spin-matched speed was 0.0000314 m/s. The test uses the stock Sun hierarchy; arbitrary planet-pack binaries remain unverified.
- Installed third-party Scatterer/Restock emitted shutdown exceptions after the successful test; no Ringworld stack was implicated in those shutdown errors.
- Tests use an isolated save and Slow or lower. High-preset scenery, third-party planet packs and every graphics backend are not certified by these runs.

No release ZIP or tag changes. The harness restores the normal development build after each runtime test.
