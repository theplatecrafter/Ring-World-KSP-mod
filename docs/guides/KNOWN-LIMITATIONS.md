# Limitations and troubleshooting

Niven Ringworld supports expeditions on a rotating habitat, but it does not behave identically to a conventional spherical planet pack.

## Installation problems

**Both ring and control button missing:** update the base mod to 1.1.9, replacing matching files and restarting KSP. Releases through 1.1.8 can abort startup when another Harmony patch changes a method expected by a strict Ringworld transpiler. The KSPCF harvester conflict and intake retry failure are corrected in 1.1.9; optional adapter failures show a warning and preserve rendering/controls. Keep saves and configs. The control button is available in Flight/flight Map View; rings also render in KSC and Tracking Station. See [issue-6 evidence](../developers/ISSUE-6-STARTUP-AUDIT.md).

Advanced emergency switches `stockHarvesters`, `stockModuleEnvironment` and `solverEngines` live in `GameData/NivenRingworld/Compatibility.cfg`; leave them enabled normally. Turning a switch off skips that feature's adaptation and requires a restart. A warning about essential compatibility blocks ring flight to protect saved residents; consult `KSP.log` for the named failed class and competing owners instead of deleting the scenario.

**Missing trees, structures, clouds or shaders:** check the extraction path first. The mod must be at `GameData/NivenRingworld`, not inside another extracted folder or a second `GameData`. Restart KSP after updating files.

**Cyla is unavailable:** install the supported optional version, **Cyla 1.1.0**, separately. Original atmosphere works without it. **HarmonyKSP 2.2.1.0 or a compatible newer version is required**, even when using Original atmosphere. See [installation](GETTING-STARTED.md).

## Performance and graphics

Start at **Slow** on a laptop, then reduce the preset or screen resolution if needed. Preset names describe relative rendering cost, not guaranteed frame rates. Terrain streaming, forests, physics and screen resolution all affect performance.

Distant terrain and forests use simplified representations. Visible LOD changes, coarse canopy shapes and delayed chunk loading can occur. The full-ring outline is a backdrop, not detailed terrain or collision coverage around the entire circumference. Small props and buildings have separate visibility ranges.

Cyla currently provides a local optical approximation. Whole-ring distant Cyla scattering is not implemented. The lightweight cloud layers are not volumetric; higher cloud settings use a bounded volumetric renderer. Weather is visual rather than a fluid simulation and does not apply wind forces.

Water is translucent and offers optional ripples/waves, but reflections approximate the sky. There are no simulated currents, hydraulic drainage or accurate object reflections. Scatterer does not automatically recognize the cylindrical ring water as a stock ocean.

Photo output preserves the screen aspect ratio. Very large captures can exceed GPU limits or take substantial time. Use Cancel to restore gameplay settings if preparation is too expensive.

## Flight and persistence

Save and ring-surface warp require a settled craft on dry ground. Check the Ringworld panel's blocking reason if either is unavailable. See [flight and saving](FLIGHT-AND-SAVING.md).

The central star remains the native KSP body. Stock orbit statistics and some third-party systems therefore retain star-relative assumptions. Arbitrary fleets, distant debris, docking/frame changes and all combinations of vessel modules are not fully validated. Other mods that replace stock aerodynamics, science or wheel systems may need dedicated adapters.

Detailed colliders exist around the loaded vessel. Extremely fast low-altitude travel can exceed useful streaming or physics precision. Arrival preserves velocity: the ring never supplies free braking.

## Exploration and science

Buildings are static scenery with simplified exterior contacts. Working elevators, inhabited cities, detailed interiors, industrial production and sunflower beam hazards are not implemented. Mountains and rivers are procedural interpretations rather than reconstructions of a complete canonical map.

Use stock experiments for Ringworld research. The science archive can group reports under the Sun internally, while the report titles identify their Ringworld locations. Expedition milestones are shown in the Research tab, not Mission Control. There is no new Career transport mechanic yet. See [science and rewards](SCIENCE-AND-EXPEDITIONS.md).

## Multiple rings

Habitats remain parallel and fixed relative to their reference body. One rotating physics frame is loaded locally. Overlapping habitat envelopes are rejected, occupied rings are protected, and off-centre illumination retains a central-star approximation. See [multiple rings](MULTIPLE-RINGS.md).

## Report a problem

Report reproducible issues on [GitHub](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues). Include your mod version, KSP version, installed mods, graphics preset/backend, location, reproduction steps and `KSP.log`. A screenshot and a reproducible craft/save are useful. Review logs and saves for personal information before sharing them.

For technical test evidence, consult the [validation archive](../history/VALIDATION.md). Historical test results describe particular fixtures rather than a guarantee for every craft or mod combination.
