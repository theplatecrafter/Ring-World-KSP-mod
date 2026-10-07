# Reported issue regressions

## Full-size interstellar plane landing ? October 6, 2026

See [Interstellar landing](INTERSTELLAR-LANDING.md) for the supplied hang evidence, startup-orbit and aircraft-attitude fixes, and natural-gravity Aeris 4A landing/impact tests with development and reported KSP graphics settings. The original freeze has not been reproduced or attributed conclusively.

## Map and Tracking Station camera movement ? October 6, 2026

See [Map rendering](MAP-RENDERING.md) for the camera-time trajectory backend, double-precision visual-layer positioning, Harmony suppression of cloned conic lines, and the isolated `-MapRenderingOnly` regression. Dependencies and pending release constraints are recorded in the base and Scattering release notes.

Development tracking for v1.1.5. A source change or isolated test is not equivalent to reproducing the reporter's craft. Public issues stay open until their remaining cases are verified.

| Report | Development status | Remaining verification |
| --- | --- | --- |
| [PR #2: ReStock Surveyor](https://github.com/theplatecrafter/Ring-World-KSP-mod/pull/2) | Merged; stock MODEL retained, optional ModuleManager patch selects the verified ReStock thermometer path. Science category retained. | ReStock 1.5.1 actual loaded Surveyor prefab, meshes and materials passed on 2026-09-30; manual editor appearance remains unreviewed. |
| [#3: EVA ragdoll](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues/3) | Existing ring frame/contact/walking adapters reviewed. Not yet reproduced with the reporter's craft. | Ladder release, natural landing and recovery without manually forcing recovery. |
| #3: nearby craft break apart | Removed global velocity-frame reset for non-active craft during unpacking; refresh saved pose before packing and collision history on unpack. Landed craft plus four deployed science units passed the Space Center save/reload regression (`RingworldSmoke-20260928-225932.log`), with 0.036 mm craft position error. | Multi-part landed neighbour crossing physics range with damage enabled; repeated warp regression. |
| #3: arrival drop | Arrival altitude currently intentionally starts a normal-gravity descent. | Optional gentle arrival remains a feature request; default descent has not been changed. |
| #3: resource lock/harvest | Development resource/scanner adapter uses ring deposits and does not require unlocking the host Sun biome. Shared abundance tests pass. | End-to-end deployed drill production, including configs removing solar Ore and modded drills. |
| #3: map vessel switching | Save permission is restricted to grounded, settled ring craft; stock focus/switch paths need a targeted reproduction. | Switching between two supported residents without weakening airborne save restrictions. |
| #3: Heisenberg lift | Experimental scoped air/gravity query adapters exist. | Actual Heisenberg craft; controller/autopilot support is not implied. |
| #3: Sun/LANDED scanner labels | Development scanner UI adapter and ring resource identity tests pass. | Third-party instruments may still read host-body fields directly. |
| [#4: cannot submerge](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues/4) | Replaced mass-independent lift with displacement-based buoyancy; populate part immersion/contact and vessel splash state. Live sinking/lift regression passed in `RingworldSmoke-20260928-155426.log`. | The combined September 30 test adds 1,500 physics ticks of calibrated 49-part flotation, ending at 0.0192 m/s. Damage immunity was enabled; arbitrary boat stability remains unverified. |
| #4: aquatic engines/ballast | Optional SunkWorks query adapter redirects ocean/intake checks only for ring vessels. | SunkWorks 1.3.1 installed-module wet fill, vent, dry intake rejection and aquatic-engine wet/dry nozzle checks passed (`RingworldSmoke-20260930-020644.log`). Non-ring ocean/altitude and buoyancy dispatch also passed. Full engine thrust, arbitrary boat designs and autopilots remain unverified. |

Water rendering tests cover both sides of the surface: depth-based transmission preserves nearby submerged objects over deep seabeds; optional shafts contribute through the surface as well as beneath it. These checks do not certify reference-image parity or desktop performance. Full scenes use Slow on the development laptop.

Validation logs are kept under `artifacts/validation` and the local KSP instance. Do not distribute decompiled KSP source used for API inspection.

## Stock intake and terrain handoff ? October 2, 2026

`RingworldSmoke-20261002-024537.log` and `RingworldSmoke-20261002-025548.log` passed the installed-issues route with new stock/streaming checks. A stock airScoop module configuration on the test vessel produced IntakeAir using the real patched ModuleResourceIntake.FixedUpdate. The second run also compared intake airspeed with ring-relative velocity, checked local gravity/validity helpers, and invoked the patched stock engine dry-nozzle method. The host body's atmosphere/oxygen flags were unchanged. This is module-level validation, not a complete flight of every stock spaceplane.

The LOD test generated a complete layout, crossed several tile boundaries with a moving observer, held near-ready false, and verified the previous visible layout stayed resident until the complete replacement could be published. All 110,323 core checks passed and the normal build was restored to the shared development instance. Subjective in-flight transition smoothness still needs review; the handoff is atomic, not an alpha fade.

See [Environment compatibility](ENVIRONMENT-COMPATIBILITY.md) for the complete audit scope and remaining gaps.

## Crash survivor save and unsolicited Flight regression

The Tracking Station encounter monitor previously called `FlightDriver.StartAndFocusVessel` for any incoming object, including debris already inside the arrival region. It now only stops unsafe warp for non-debris craft and leaves scene changes to the player. This also removes the repeated encounter-message loop at normal speed.

The save permission patch previously called the all-vessel surface-warp check. Saving now checks only the active resident's contact, velocity, throttle and rotation. Time warp still checks every nearby loaded craft, so the fix does not freeze airborne fragments into a surface anchor.

`TrackingSmoke` checks station persistence, debris exclusion, unsafe warp rejection and explicit Fly. `ResidenceSmoke` checks that an unsafe neighbour (nonzero throttle) blocks warp without blocking a settled resident's save, followed by a Space Center save/reload round trip. Neither fixture reproduces every possible plane-breakup or unloaded debris trajectory; these remain separate manual checks.

Tracking regression passed in `RingworldSmoke-20261002-130933.log`: unsafe warp was rejected, the station remained open during the observation interval, and an explicit Fly returned the craft at 205,599 m altitude. The test also rejected airborne save permission before and after the scene round trip.

The first two residence test attempts did not exercise the intended unsafe-neighbour state: deployed equipment discarded the injected rigidbody velocity (measured neighbour speed remained zero). The fixture now uses its nonzero-throttle guard, restoring the value before any physics tick. Those attempts are not counted as passing regressions.

The corrected residence regression passed in `RingworldSmoke-20261002-132932.log`: the unsafe neighbour blocked warp while the active resident remained saveable. The Space Center round trip restored the vessel and all four deployed science units, with a position error of 0.0000587 m. This verifies the permission separation and persistence; a natural stock-plane crash with freely moving debris remains a manual reproduction target.
