# Full-size interstellar plane landing

## Report and evidence

The report concerns released Niven Ringworld 1.1.7 and Clouds/Scattering 1.0.1 installed through CKAN, with the interstellar full-size replacement configuration. The saved options specify radius 150,000,000,000 m, width 1,605,000,000 m, rim walls 1,600,000 m, Original atmosphere, forest quality 0 and a 160,000 km terrain horizon. The teleport longitude in the player log matches Explorer's Landing Field.

Windows recorded Application Hang event 1002 for KSP_x64 at 23:19:35 in the supplied session's timestamps. The player log continues beyond the last flushed KSP.log entry and ends after the Landing progress event. No matching native crash dump or resource-exhaustion event was found. That identifies a freeze but not the blocked function or its root cause. The logs also contain unrelated Scatterer GUI index/duplicate-instance errors; their presence is not proof that Scatterer caused the freeze.

Read-only evidence copies are retained locally in artifacts/diagnostics/interstellar-landing-20261006. User saves and the CKAN installation were not edited.

## Fixed Ringworld defects

Ringworld called packed-vessel orbit sampling before KSP had initialized a valid orbit. The reported exceptions originate in RingworldFlight.PrepareArrival ? Velocity ? RingAnchorEphemeris. Orbit readiness is now checked, transient samples have a non-throwing query path, and packed numerical updates defer rather than writing an invalid orbit during the transition. Anchor/body configuration errors remain reported normally.

Teleport rotation used vessel.transform.up as though it represented gravity up. For aircraft this is a longitudinal craft axis. Arrival now rotates from the vessel's prior surface-up axis to the ring's surface up, preserving pitch/roll instead of deliberately standing the plane upright.

Neither finding alone proves the cause of the original Windows hang.

## Runtime regression

Run `./smoke-test.ps1 -InterstellarLandingOnly`; add `-MatchCkanGraphics` to replay the preserved KSP settings from this report. These commands temporarily substitute the full-size config in the shared development instance, use a timestamped isolated save, launch the stock Aeris 4A on the runway, deploy gear, and perform 12 m and 60 m descents at Slow ring quality. Crash damage is enabled and artificial gravity remains 9.72 m/s?. The runner restores the original installed configuration pack and settings, and reinstalls the normal base build afterward.

- RingworldSmoke-20261006-234511.log passed with development graphics. The 12 m case landed intact with 40 parts and settled below 0.04 m/s. The 60 m case impacted at approximately 31 m/s, produced six surviving loaded vessels/debris objects, and continued for 378 rendered frames during the observation interval.
- RingworldSmoke-20261006-235302.log passed with the CKAN instance's KSP settings. The harder impact continued for 402 frames, leaving the active cockpit with two parts and six loaded vessels/debris objects. Startup zero-radius exceptions did not recur.

The development instance contains additional mods and updated Ringworld binaries; this is not a binary-identical rerun of the reported CKAN installation. These initial cases are unpowered descents, not a complete powered approach. Exact manual controls and engine conditions remain relevant if the freeze persists. A live hang stack/dump would be needed to attribute an unreproduced blocked function confidently.

Core verification: 110,581 checks passed; normal and smoke base builds compile. No version, ZIP, remote repository or published CKAN metadata was changed.
