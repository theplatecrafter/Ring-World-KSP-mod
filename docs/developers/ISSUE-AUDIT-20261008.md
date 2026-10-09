# Repository issue verification - October 8, 2026

This audit covers the two reports that were open at its start, [issue 3](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues/3) and [issue 4](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues/4). Both were closed as completed on October 9 after representative runtime verification. Each symptom needs its own reproduction; an abundance query or wet-nozzle predicate alone does not establish successful drilling or engine thrust.

The project owner's current request authorizes closing verified issues. This exception is limited to issue resolution; local Git operations, remote code changes and release publication remain outside the development workflow. Both closure comments identify the unpublished development status. The original reporter's craft is unavailable; the checks below verify representative stock and installed-mod cases, not every third-party controller, resource system or craft design.

| Case | Required verification | Current audit result |
| --- | --- | --- |
| 3.1 EVA ladder/hatch release | Natural contact, recovery through ordinary movement input and walking with damage enabled; no injected recovery event | Passed `residents-14`: real hatch/ladder release, contact, native recovery and 2.634 m walking; peak speed 3.836 m/s. |
| 3.2 Nearby multipart base | Cross unload/load physics range repeatedly; intact joints, limited displacement; damage enabled | Passed `residents-14`: two native unload/load/unpack cycles, all 49 parts retained; displacement 0.00927 and 0.01085 m. |
| 3.3 Gentle arrival | Inspect/replay current arrival and provide optional gentle placement if needed; retain ordinary descent | Passed `residents-7`: all 49 parts intact, speed 0.21183 m/s, damage enabled. Collider refinement lifted the craft 1.2344 m above the raised causeway. |
| 3.4 Resources/drills | Actual stock harvester production with host Sun Ore disabled and survey lock retained; shared CRP definitions | Passed `full-16`: stock recipe produced 0.000236 Ore / consumed 0.07559 EC; installed GoldStrike produced 0.007829 Ore / consumed 2.75458 EC. Real terrain impact, host biome still locked; ordinary Ore only, no rich-lode certification. Earlier `full-5` stock production was 0.007823 Ore / 2.7525 EC; these are measured runs, not guaranteed throughput. |
| 3.5 Map vessel switching | Use stock map switching between two supported residents | Passed `residents-14`: native map FocusObject selected the other landed resident and returned to Flight. Sandbox far-vessel switching permission was enabled. |
| 3.6 Airship lift | Real HL Airships and Heisenberg installed modules; varying pressure/gravity and effective upward lift | Passed `full-16`: installed HL and Heisenberg envelopes produce upward 2.9153 kN near the surface, falling to 0.8352 kN at 10 km as density decreases from 1.2003 to 0.3439 kg/m3. Native Heisenberg static lift gives 4.34008 kN. Whole-airship flight/controller behavior remains unverified. |
| 3.7 Science/biome labels | Stock instruments and SCANsat local instruments, science subjects identify ring/biome | Passed `actions-15`: actual stock biome analysis message identifies Interstellar Standard Ringworld / Explorer's landing field / Ruins; stock crew report has a RingworldV2 subject and location title; installed SCANsat reads 2.83% Ore / Ruins. Orbital SCANsat mapping remains unsupported. |
| 4.1 Ballast/submersion | SunkWorks real ballast flow changes resource mass and craft sinks; dry fill rejected | Passed `aquatic-11` and repeated in `full-16`: 34,176.78 IntakeLqd units added; mass 22.674 -> 56.850 t; altitude 228.189 -> 225.189 m. Installed-module dry fill/vent checks passed separately. |
| 4.2 Water stability | Sustained flotation of a representative balanced craft without position holding or buoyancy recalibration | Passed `aquatic-11` and repeated in `full-16`: 45 s wall-clock observation of upright flotation, all 3 parts intact, final speed 0.5077 m/s / peak 3.4054. Damage enabled; native hull buoyancy 0.7 retained. |
| 4.3 Aquatic engine | SunkWorks actual wet thrust/fuel flow and dry shutdown | Passed `aquatic-11` and repeated in `full-16`: 10 kN wet thrust / 3.0316 EC consumed; dry-nozzle thrust stops. |

The shared instance already contains SunkWorks 1.3.1 and WildBlueCore. Selected airship stack: HL Airships Core 7.0.1.6 and Heisenberg 2.22.0 with the dependencies declared by current CKAN metadata. SCANsat 21.1 and Community Resource Pack 112.0.1 are selected for science/resource checks. Downloads and installation manifests belong under `artifacts/issue-audit-20261008`, not in release archives.

Full flight tests use Slow or lower on the development laptop. Tests use isolated saves and restore the normal DLL and user graphics settings. New external test mods are optional compatibility targets, not new base-mod requirements. No NetKAN edits or release package are part of this audit.

With the listed test mods installed and KSP closed, run from the base-mod directory:

```powershell
.\tools\VerifyRepositoryIssues.ps1 -Cases residents,full -Run local
```

Logs are written to `template_instance/RepositoryIssues-<case>-<run>.log`. The runner requires each case's completion marker, reports exceptions, limits its own process lifetime and restores the normal development DLL and backed-up settings even on failure. The `actions` case skips range cycles for a focused map/EVA/instrument check; the `aquatic` case isolates actual boat physics. Timing labels on flotation refer to wall-clock observation, not a guaranteed amount of simulated game time on a slow machine.

## Fixes and test setup

- The runtime cache assertion initially failed: stock `Vessel.UpdateCaches` overwrote precalculation's local surface values with host-orbit values. A second, scoped publication at that stock boundary passes the surface-speed check. The stock harvester also reads live local horizontal speed during initialization.
- The audit temporarily adds a zero-Ore host resource definition, retaining the stock survey lock. Production uses the stock drill configuration and real collider impact probe on the resident craft. This verifies the actual converter/flow path, not an entire deployed drill model.
- Heisenberg's WBIResources dependency replaces `ModuleResourceHarvester` on the stock drill. The audit exercises the unchanged stock module and installed GoldStrike module separately. The initial GoldStrike run failed in its planet-indexed lode lookup; the scoped ordinary-Ore fallback passed production verification.
- The selected SCANsat resource display has its own spherical survey/coverage gate. The local display bridge passed with installed SCANsat. No orbital SCANsat mapping support is claimed.
- `tools/VerifyRepositoryIssues.ps1` runs selected cases sequentially with an owned-process timeout and restores the normal DLL and original `settings.cfg` in `finally`. Nested test coroutines now report exceptions to the harness instead of stalling silently.

Run labels `full-2` through `full-4` contain intermediate failures and must not be presented as complete passes. In `full-3` the stock drill did produce Ore, but the test incorrectly inspected only the root battery; the corrected full-craft energy check passed in `full-4`.

`full-5` passed both drill implementations and the installed airship/ballast/nozzle query cases. It failed to load the synthetic boat because a new airborne proto starts in the stock inertial chart, while the observer uses the rotating chart. The corrected fixture explicitly loads and places the boat once before releasing all physics; it does not hold its pose during observation. `residents-5` failed the new gentle-placement test: clearance used the mass centre while Transfer places the root transform. This was corrected before run 6. Neither failed end-to-end case is counted as resolved.

Run 7 uncovered extreme packed-vessel heating: the original craft's recorded skin temperature reached 6,092,401,712 K while packed. The local atmosphere path called `RingworldFlight.Velocity`, whose packed fallback returned inertial orbital velocity containing ring spin. Packed records already preserve the correct local velocity; that data now feeds the local calculation. The new boat also exploded before its observation interval. Run 8 retests this correction with damage enabled. This is a reproduced development failure, not proof that it was the sole cause of the original report.

Gentle placement now checks the actual loaded terrain/scenery colliders after publication, in addition to analytic terrain and collider clearance. It remains a one-time placement; there is no subsequent pose hold or modified gravity. The training approach captures its ordinary-arrival choice rather than consulting the mutable UI toggle later.

## Water resolution

Issue 4 was closed as completed on October 9 after `aquatic-11`, with a public comment identifying the unpublished development status and precise test scope. The crewed test craft uses actual SunkWorks procedural hull and Ebb Tide parts plus a stock command pod. Its normally editor-generated ballast capacity is initialized with SunkWorks' own calculation before observation; empty initial ballast and native buoyancy are retained. This does not calibrate a force or hold the craft's pose while floating/sinking.

Earlier real-craft attempts exposed a geometry mismatch: a 21.6 t generated hull still presented a 1.9 x 6.5 x 1.2 m template drag cube. The optional geometry bridge reads SunkWorks' generated cavity volume and local hull dimensions. Bounds are cached until dimensions/volume change. The stock one-time zero-cube initialization step is also retained. Other parts retain the existing generic stock-cube path. No universal boat/autopilot/swimming compatibility is implied.

## Nearby resident investigation

The first clone fixture reused part IDs and overlapped a neighbouring building; those attempts are not physics-range evidence. The corrected fixture uses unique part/persistent IDs and a clear location on the causeway. The observer crosses real native load/unload distances; only its route is controlled, not the neighbour's pose or forces.

Earlier range attempts reported hundreds of kilometres of displacement. The detailed `residents-13` trace corrected that interpretation: the stored and actual root-transform poses agreed, while the vessel remained packed at 230 m, outside KSP's native 200 m landed unpack range. The packed position query had read cached inertial CoMD rather than the saved ring-local anchor. That query now uses the landed record. The corrected fixture is 180 m away and explicitly requires actual unloading, loading and unpacking; both cycles passed in `residents-14` with all 49 parts and approximately 1 cm displacement. The earlier apparent displacement is not evidence of a physical jump.

Source inspection also identified lifecycle boundaries that must preserve the chart: unloading clears parts before its late GoOnRails call, and unpacking can place parts from inertial ephemeris after a prefix. Local pose/velocity are captured at the start of Unload and restored after loaded placement. Ring pose changes keep `vesselTransform` consistent with the part tree, because stock pristine-coordinate placement reads that assembly transform. The two real range cycles verify the combined lifecycle behavior; they do not isolate every change as an independently reproduced cause.

The first EVA fixture sent ladder release during KSP's ladder-acquisition state, where that event is ignored. `residents-14` waits for acquisition to finish, releases the hatch normally, lets the Kerbal contact the terrain and supplies ordinary W input. Stock active-EVA recovery requires movement input; the test does not invoke its recovery event or force a walking state. Native recovery and subsequent walking passed. The original reporter's particular craft is unavailable; this verifies a representative crewed, multipart craft with damage enabled.

`full-14` stopped at the new altitude sweep. That fixture moved the vessel and immediately read KSP's cached CoMD/WCoM before precalculation refreshed them. Run 15 waits for ordinary physics updates before comparing high-altitude density and lift. `full-14` is not an end-to-end pass. The companion actions check also verifies the stock biome scanner's actual screen message rather than only its enabled action.

`full-15` verified the HL envelope's lower density/lift at 10 km, but then exposed another fixture error: restoring cached Unity coordinates after KSP's floating-origin shift left the next envelope at altitude. The sweep now preserves and restores ring-chart coordinates and refreshes its expected environment per module. This is a test-fixture correction, not an airship runtime fix; `full-15` is not a complete pass.

## Completed audit - October 9

`residents-14`, `actions-15` and `full-16` reached their respective completion markers without a harness failure. The final combined run repeated the actual SunkWorks craft tests after the resident lifecycle changes. Both build variants compiled without warnings/errors and all 110,644 core checks passed. The normal development DLL matches the installed DLL, the smoke fixture types are absent from that normal assembly, the original settings backup matches `template_instance/settings.cfg`, and no owned KSP test process remains.

Public resolution records: [issue 3](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues/3#issuecomment-6075750735), [issue 4](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues/4#issuecomment-6074985139). No code was committed/pushed, release published, NetKAN metadata changed or ZIP created.
