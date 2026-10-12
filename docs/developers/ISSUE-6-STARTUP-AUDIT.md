# Issue 6: startup failures in released 1.1.8

Investigation and local correction on October 12, 2026. Production source and private regression fixtures were changed; released ZIPs, user saves, NetKAN and remote repositories/issues were not changed.

## Confirmed evidence

[Issue 6](https://github.com/theplatecrafter/Ring-World-KSP-mod/issues/6) reports KSP 1.12.5 Windows with Ringworld 1.1.8, Kerbol Standard, Harmony 2.2.1.0 and ModuleManager 4.2.3. The reporter's DLL SHA-256, `3f6d4f9e5b85e30856d77a6ffad8ea13ba1e3d0733f7ac9966f2ba3b132e2441`, matches the DLL extracted from the prepared 1.1.8 ZIP. This is not the old EVA exception or a stale AssemblyVersion misunderstanding.

Two strict checks still abort startup:

- `RingResourceHarvester.Transpiler` expects a direct `ResourceMap.GetAbundance(AbundanceRequest)` call in `ModuleResourceHarvester.PrepareRecipe`, substitutes its scoped resource helper, and throws when no direct call remains.
- `RingIntakeEnvironment.Transpiler` expects a direct `CelestialBody.atmosphereContainsOxygen` field read in `ModuleResourceIntake.FixedUpdate`; missing that read throws.

Released 1.1.8's `StockIntegration.Install` called PatchAll before creating renderers/toolbar. Its installed flag remained false on failure, permitting another attempt after a partially completed installation. The report's persisted scenario and valid config are compatible with this presentation/startup failure; deleting them is not a repair. The local correction installs classes independently and only once.

## Source-level conflict and retry hypothesis

[KSPCommunityFixes 1.41.1 ResourceConverterPerf](https://github.com/KSPModdingLibs/KSPCommunityFixes/blob/1.41.1/KSPCommunityFixes/Performance/ResourceConverterPerf.cs) patches the exact harvester method and replaces its direct abundance call with `GetAbundanceCached(map, request, module)`. When that transpiler precedes ours, our required call is absent. This collision has now been reproduced with the actual pinned KSPCF release. HarvesterECConsumption targets GetInfo, a different method, and should not be blamed for this PrepareRecipe rewrite.

The later intake exception may be a consequence of our retry. Harmony 2.2.1 appends registered transpilers without deduplicating them. A retry can register an already-installed Ringworld intake transpiler again, so the second copy sees the first copy's replacement helper instead of an oxygen field read. Actual duplicate registration now reproduces the reported intake exception; actual repeated registration with the corrected helper succeeds. This validates the mechanism, without claiming to have captured the reporter's complete original patch order. FAR/AJE ModuleManager config patches alone are not evidence of IL rewriting.

## CKAN/PR distinction

[Published NivenRingworld 1.1.8 metadata](https://raw.githubusercontent.com/KSP-CKAN/CKAN-meta/master/NivenRingworld/NivenRingworld-1.1.8.ckan) has the correct required base dependencies and the prepared ZIP hash `d99b67d637eeeca5139df2940229ded0122398ea5587963c85da0fa28378ba0d`. Existing NetKAN recipes already discover new SpaceDock releases; publishing a version does not wait for an unrelated metadata PR to merge.

PR 11674 was open/unmerged when queried. Current CKAN suggestions still have older Clouds/Scattering minima and omit the Parallax bridge. [Scattering 1.0.2 metadata](https://raw.githubusercontent.com/KSP-CKAN/CKAN-meta/master/RingworldScattering/RingworldScattering-1.0.2.ckan) still permits base >=1.1.7, although the new extension requires >=1.1.8. That allows a separate mismatched installation and needs the pending metadata correction. It does not explain issue 6, whose base is genuinely 1.1.8 and whose failing patches are in the base itself. The issue also persists after removal of Parallax/SystemHeat-related packages.

## Follow-up implementation requirements

- Reproduce with the actual KSPCommunityFixes 1.41.1 patch, preserving non-ring resource behavior and its cache semantics.
- Make resource integration compose with the optimized query path rather than merely suppressing the exception.
- Make installation/retries idempotent after partial failure; prevent repeated registration of completed patch classes.
- Separate optional-feature failures from rendering/UI startup, with explicit diagnostics and safe degraded capabilities. Do not hide essential flight-physics failures or advertise intakes/drilling as working when they are not.
- Test initial startup and subsequent scene transitions, stock intakes and drilling, and prior-save preservation. No save format/default reset is needed to diagnose these patch failures.

## Implemented correction and runtime evidence

The strict harvester transpiler is replaced with an exception-safe PrepareRecipe query scope and ResourceMap boundary. A reflection-only KSPCF cache-helper prefix bypasses cached host abundance for that scope; non-ring queries retain CF caching. Unknown future CF helper signatures are diagnosed, not silently declared compatible. Stock vessel-speed caches refresh at recipe entry.

Per-class Harmony owners isolate failures and permit rollback without removing another mod's patches. Scene-like repeated Install calls do not append more patches. Optional module-environment/resource failures retain the ring and launcher with a visible warning. Essential failures gate flight, pause a loaded resident and suppress pose capture. Two default-on restart switches allow emergency disabling of stock harvesting / intake-engine environment reads.

The actual pinned KSPCommunityFixes 1.41.1 release reproduced the released harvester exception in `StartupCompatibility-issue6-1.log`. The corrected code passed native stock drill production, intakes, legacy resident restoration/resaving and EVA walking. Thus the harvester source-level conflict is now runtime confirmed. The reporter's exact intake trigger remains unproven; retry duplication is prevented and repeated helper passes are tolerated, while unsupported foreign rewrites produce an isolated diagnostic.

`StartupCompatibility-issue6-baseline-2.log` additionally loaded CTT 3.4.5, CRP/CCK 112.0.1 and B9PartSwitch 2.21.0.4. A real CF cache entry of zero for the Sun did not replace ring abundance 0.02834319 or get polluted by it. Unrelated queries and an exceptional native recipe restored their scopes. Native drill production was 0.02723905 Ore with 9.61044 EC consumed; host survey state was unchanged. IntakeAir and oxygen-disabled rejection passed. With KSPCF's buoyancy optimizer active, a displacement-calibrated one-part hull floated for 1,500 ticks at 0.0034813 m/s; zero-buoyancy sinking and immersion fields passed. EVA walked 2.6492 m with foreign transpilers active.

Tests run at Slow in a private KSP 1.12.5 instance. The synthetic legacy save preserves its Sun host, primary ID, seed, dimensions and resident despite an installed replacement interstellar config. This is not the reporter's actual save or exhaustive historical-save coverage. Two PQS teardown null references appeared during the fixture's initial stock-planet departure; they are retained in the logs and were not reproduced as a Ringworld startup failure. Do not describe this as an exception-free whole-session certification.

See [the full screenshot baseline](MAJOR-MOD-COMPATIBILITY.md) for actual test versions, prior evidence and pending integrations. Existing 1.1.8 artifacts remain unchanged.

## General surface caches and AJE follow-up

Stock `Vessel.UpdatePosVel` writes surface-speed/velocity fields after UpdateCaches. The source now republishes ring-relative caches at that final writer, improving ordinary module consumers without changing the orbital caches. The stock intake regression explicitly calls the writer and checks its resulting local caches.

SolverEngines/AJE additionally has its own engine, inlet and vessel-distributor loops. Their body oxygen, altitude/depth and raw velocity reads needed separate scoped adaptation. A native Aeris 3A launch with AJE 2.20.1, SolverEngines 3.14.0 and FAR 0.16.1.2 passed ignition at 16.225956 kN with 0.2487343 LiquidFuel units consumed (`StartupCompatibility-issue6-native-plane-25.log`). The unchanged host oxygen flag remained false. The distributor had previously received stellar airspeed and produced ~1.345 billion K inlet temperature, despite a valid ~287 K ambient environment. These are additional runtime defects, not a reason to globally modify the Sun or remove CF.

The original native burn used AJE 2.20.1 without RealFuels. Follow-up issue-specific coverage below adds AJE 2.20.0 and RealFuels. Full FAR flight/landing, every screenshot mod and the reporter's actual save remain separate coverage; a representative burn is not certification of every engine or every historical save.

Final regression `StartupCompatibility-issue6-final-stack-26.log` passed with the combined community/AJE/FAR stack, including an explicit native UpdatePosVel call followed by ring-relative cache checks. The normal development build passed 110,644 core checks and was installed in `template_instance`; test classes are excluded. Released ZIPs and NetKAN remain untouched. The compatibility checklist retains the complete screenshot inventory and pending background/utility/visual tests.

## Issue-specific acceptance tests — reported versions

The private fixture now uses KSPCF 1.41.1, RealFuels 15.14.0.0, AJE 2.20.0, FAR 0.16.1.2, B9PartSwitch 2.21.0, Harmony 2.2.1 and ModuleManager 4.2.3. RealFuels-Stock 5.1.0 is the pinned stock configuration tested; the reporter did not specify its version. Required upstream support includes SolverEngines 3.14.0, CRP 112.0.1, ROUtils 1.2.0.0, SerializationFix 1.0.1 and KerbalChangelog 1.4.2. The fixture also retains CTT/CCK and the existing ring extensions. These are private test installations, not new Ringworld dependencies.

- `StartupCompatibility-issue6-reporter-fresh-29.log`: first startup in Space Center, two Tracking Station/KSC round trips, and reopening the saved game. Kerbol Standard's complete saved ring configuration remained identical; the renderer survived every scene; all 120 owned patches remained registered exactly once; startup reported zero failed compatibility classes. This first install occurs in KSC, rather than installing patches ahead of time in the main menu.
- `StartupCompatibility-issue6-reporter-runtime-30.log`: actual old harvester and duplicate-intake failures reproduced; the corrected intake tolerated real repeated Harmony passes. Native Ore production was 0.02723802 with 9.610095 EC used; native Water production was 0.23910074 with 4.782015 EC used. CF cache hits/non-ring queries, resource overrides, exceptional scopes, local velocity publication, stock/AJE intakes, flotation, legacy restoration/resaving and EVA walking (2.688344 m) passed with RealFuels installed. Intentional partial/essential failure injections are labelled separately in this log and restored before continuing.
- `StartupCompatibility-issue6-reporter-plane-32.log`: normally launched Aeris 3A configured with native RF Fuselage/Kerosene tank and part-resource save nodes. AJE 2.20.0 / RF / FAR produced 16.120142 kN and consumed 1.516715 Kerosene units at local altitude 162.3375 m; the Sun's oxygen flag stayed false. Run 31 correctly rejected a zero-fuel craft; adding only RF tank metadata without its separately persisted part-resource state was an incomplete fixture. The corrected test uses native resource loading/flow and solver calculations.
- `StartupCompatibility-issue6-retained-scenes-34.log`: actual retained scenario formats 3 (one resident), 4 (two residents) and pre-1.1.8 format 5 (no resident) passed data load/save/load checks. All stored options, ring IDs, generator choices, resident poses and discoveries/research were retained; authoritative serialized state was repeatable. Original files were copied read-only and their hashes checked afterward. The same run passed native Flight Map → Tracking → KSC with the synthetic landed legacy resident, scaled renderer and 120 unchanged patch registrations. Retained files are dated September 14, September 16 and October 6; their exact originating mod binary versions are not independently pinned. This is real retained-data coverage, not a claim that every historical craft was flown or every released-version upgrade was tested.

Earlier aborted harness outputs remain available: run 27 omitted ROUtils' SerializationFix dependency and was stopped; run 28 entered KSC without first writing the fixture's persistent.sfs. Run 29 corrected both the installation and fresh-game setup. Stock AlarmClock and KerbalChangelog teardown exceptions and an optional Kopernicus/BurstPQS loader warning remain recorded; these runs must not be described as globally exception-free or as complete certification of those unrelated mods.

Run 33 selected an old format-1 scenario with no stored ring options or residents, so it correctly failed the occupied-world fixture prerequisite. Run 34 uses actual retained occupied formats 3/4 plus format 5 instead. The test does not invent a historical ring where no world data was saved. Redundant OPTIONS copies are normalized when comparing modern authoritative RING nodes; newly persisted anchor fallback metadata may appear during the first legacy round trip without changing world identity/pose.

The normal control-panel launcher currently exists in Flight and flight Map View. Scaled rings render in KSC and Tracking Station; this fix does not claim that the Flight-only editor was added to those other scenes.

## Delivery / issue closure

The reported startup defects are corrected locally and the issue-specific acceptance checks pass. The owner requested a separately versioned 1.1.9 ZIP. Published/CKAN 1.1.8 still contains the old strict patches; preparing this artifact does not update that download. The owner must publish 1.1.9 before users can receive this correction. No dependency removal, config deletion, scenario reset or new save is required for a valid upgrade. Extension versions/install paths and the stable assembly ABI remain unchanged.

The GitHub issue has not been remotely modified under the owner-managed repository workflow. Close it after the corrected release is available and identify that version in the response. The reporter's original full log/save has not been supplied; preserve this distinction from the reproduced pinned-stack evidence.
