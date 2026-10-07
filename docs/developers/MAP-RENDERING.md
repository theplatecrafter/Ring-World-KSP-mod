# Map and Tracking Station rendering

## Implementation

RingTrajectory previously stored absolute scaled-space float positions in a disabled LineRenderer and projected them through IMGUI. Prediction could span multiple player frames, so retained geometry contained old origin/camera data. Only its first vertex followed the current vessel. Drawing was also dependent on IMGUI repaint timing.

The replacement keeps absolute sample times and double-precision reference-body-relative positions/velocities. Prediction remains time-sliced. Projection subtracts the current camera in double precision before applying its final rotation/projection; visible curve sampling subdivides Hermite intervals to a 0.4-pixel midpoint criterion, with bounded depth and mesh size. Consumed prediction sections are trimmed at current universal time. Encounter tooltips use absolute event times.

Lines and encounter symbols use the Vectrosity backend included in KSP. Only tooltip text remains in IMGUI. The camera `onPreCull` event draws the current screen-space line, and its UI Graphic is rebuilt immediately to avoid waiting for a later Canvas update. Harmony patches scope stock OrbitRendererBase.DrawOrbit/DrawSpline and PatchRendering.UpdatePR suppression to ring residents and the replaced orbit. The DrawOrbit guard also covers zero-opacity early exits after a retained line has been reactivated. Cloned patched-conic Orbit instances are matched by their owning vessel, so their retained line and obsolete orbit widgets are also removed. Unrelated orbital trajectories retain their stock implementation. No celestial Orbit is replaced merely to render a line.

Massless-ring coasts outside the rotating frame sample the stock patched-conic ephemeris. Rotating-frame/massive-ring coasts use the existing numerical dynamics; this work does not add atmospheric/thrust/manoeuvre prediction to that integrator. A numerical path still stops at atmosphere, wall or terrain contact. Screen subdivision smooths the display, not a physical-model limitation. Native screen-space lines remain overlays as the earlier ring path was; this does not implement stock 3D-line occlusion or all stock orbit-widget interactions.

GlobalClouds and the Scattering extension's DistantAtmosphere share CameraRelativeRingMesh. Topology and UVs remain fixed. Each scaled-space camera receives freshly transformed, camera-relative vertices from the double-precision ring chart. The shaders preserve chart coordinates for hull occlusion and use relative distances for optical handoff. ScaledRing refreshes its panel parent pose at the same render stage. Event handlers unsubscribe on disposal.

The new Scattering DLL references a new base helper. Its next release must require the corresponding next base release; it cannot be paired with the old published base 1.1.7 binary. No other required external dependency changes.

## Research

- [Unity 2019.4 Camera.onPreCull](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Camera-onPreCull.html): camera-time preparation before culling.
- [Trajectories MapOverlay](https://github.com/linuxgurugamer/KSPTrajectories/blob/master/src/Display/MapOverlay.cs): public example of camera-render-time map drawing. Used for design research; no source was copied.
- Installed KSP 1.12.5 OrbitRendererBase, PatchRendering and Vectrosity APIs were inspected locally. Their source and binaries are not distributed. Stock orbit lines use VectorLine.Draw/Draw3D; the 2D backend ultimately uploads a UI Graphic mesh.

## Validation

Core suite: 110,581 checks passed. New cases verify constant-acceleration Hermite interpolation at enlarged ring coordinates, time-boundary trimming and degenerate intervals. Base, Scattering and both shader bundles compile successfully.

Run `./smoke-test.ps1 -MapRenderingOnly` in the base project. It creates an isolated timestamped save, uses Slow quality, preserves unrelated stock orbits, checks a ring-bound coast in flight Map View and Tracking Station, forces repeated camera renders, checks cloud/atmosphere placement, and verifies landed-resident suppression. It restores the normal base build afterward. Normal user saves and installed configuration files are not changed.

Runtime results: the first run (RingworldSmoke-20261006-230607.log) caught an overly broad nonzero-mass relevance filter; it was corrected. RingworldSmoke-20261006-231147.log passed Map View, Tracking Station, forced camera renders, scaled-layer positioning and landed suppression. Screenshot review then exposed a surviving cloned stock conic line; owner-based Harmony suppression was added. RingworldSmoke-20261006-232037.log passed with the updated shaders, owner-based conic suppression and line-head alignment against Unity projection. Final run RingworldSmoke-20261006-232548.log passed, including the DrawOrbit early-exit guard, actual retained-stock-line inactivity, camera projection alignment, both installed scaled visual layers, scene changes and landed suppression. The normal base build and matching Scattering build/assets are installed in the shared template instance. Screenshots are saved in the shared test installation as RingworldMapRendering.png and RingworldTrackingRendering.png. Graphics validation on the development laptop does not establish support on every GPU/API.
