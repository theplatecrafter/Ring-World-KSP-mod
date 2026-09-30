# v1.1.5 release verification and extension split

The release is not ready until the installed-mod regressions and independent-package checks below pass. Issue #3 is excluded from this test pass at the maintainer's request; its outstanding cases remain in ISSUE-REGRESSIONS.md.

## Release checklist

- [x] ReStock 1.5.1: Surveyor model loads with meshes/materials after ModuleManager patches.
- [x] SunkWorks 1.3.1: actual ballast and aquatic-engine modules recognize ring water, pump ballast, and reject dry intakes.
- [x] Displacement-calibrated 49-part fixture floats for 1,500 physics ticks, settling to 0.0192 m/s; zero-buoyancy sinking and positive displacement lift pass. Damage immunity remains enabled in this fixture; arbitrary boat stability is not certified.
- [x] Non-ring ocean/altitude queries and stock buoyancy dispatch remain unchanged. This is a dispatch check, not a separate Kerbin voyage.
- [x] Separate Ringworld Clouds and Ringworld Scattering repositories, code, assets, builds, documentation and optional installation.
- [x] Base-only, each-extension and combined installation validation.
- [x] Move repositories under `Ring World KSP mods`, with one shared `template_instance`; update scripts and workspace instructions.
- [ ] Publish extension v1.0.0 repositories/releases, then base v1.1.5; ZIPs retain a top-level GameData folder and never bundle dependencies.
- [x] Release notes list tested versions, required and optional dependencies, and remaining limitations. CKAN submissions are handled by the maintainer.

## Test selection

ReStock is the target of PR #2 and its 1.5.1 GitHub release asset had over 509,000 downloads when selected. SunkWorks directly provides the ballast and aquatic-engine query paths involved in issue #4. Its selection is based on relevance, not a claim that it is the most downloaded maritime mod. Installed test dependencies belong only in the shared KSP instance, not release archives.

## Current development state

- Base repository and shared game were relocated into the parent workspace on September 29. Old repository folder remains empty because Windows held its directory handle open.
- Separate local Git repositories exist for Ringworld Clouds and Ringworld Scattering. Provider DLLs compile with zero warnings; separate shader bundles compile successfully. All four installation-matrix tests passed on September 30; publication is the final step.
- Base uses optional provider discovery and does not reference extension DLLs. Initial friend-assembly ABI is version paired to 1.1.5; extension metadata pins that host version.
- ReStock Surveyor prefab/model/material verification passed repeatedly. Initial aquatic spawn fixtures failed to unpack; those are failed tests, not evidence of water support. The replacement regression uses installed SunkWorks module configurations on a controlled craft; full boat/autopilot claims require separate validation.

## September 30 validation

- Installed ReStock/SunkWorks: `RingworldSmoke-20260930-020644.log`.
- Base only: `RingworldSmoke-20260930-021205.log`.
- Clouds only: `RingworldSmoke-20260930-021529.log`.
- Scattering only: `RingworldSmoke-20260930-021943.log`.
- Combined GPU/flight/photo/water: `RingworldSmoke-20260930-022354.log`.

All builds passed 110,323 core checks with zero compiler warnings/errors. Full scenes used Slow; higher water/cloud shader modes were tested only in small GPU probes. Testing used Windows/Direct3D 11 on the development laptop. Other GPUs/APIs and general third-party autopilots remain unverified.
