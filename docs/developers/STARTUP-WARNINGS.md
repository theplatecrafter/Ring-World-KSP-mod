# Parallax / Kopernicus startup warnings — 15 October 2026

## Cause and scope

The shared `template_instance/settings.cfg` selected `TERRAIN { preset = Low }`. Parallax Continued's `ConfigLoader.CheckSettings()` requires the final PQS preset (High). It displays its installation-error popup for Low/Default regardless of whether Ringworld's bridge is enabled. This is distinct from Ringworld's own quality preset and KSP's Terrain Shader Quality setting.

Kopernicus release 251's `RuntimeUtility.Update()` compares the current PQS preset with `KopernicusConfig.SelectedPQSQuality` every 121 menu/settings updates. The installed remembered/default value was High. The user confirmed the popup says **Terrain Detail changed**. A Low/High mismatch explains this warning; it is not a distant-star creation failure. The initial short startup capture reproduced the Parallax error, but ended before enough menu updates to capture this second popup.

Source evidence is the separately downloaded Parallax Continued source under `artifacts/integration-research/ParallaxContinued/Mod Source/Parallax/ConfigLoader.cs` and the retained Kopernicus release-251 source ZIP under the sibling Modpacks research directory. Nothing from either source is patched or redistributed. No upstream safety check is bypassed.

## Fix

Set KSP's **main-menu Settings > Graphics > Terrain Detail to High**, save and restart. Keep Ringworld's own quality preset at the desired lower setting. High satisfies Parallax's requirement and agrees with Kopernicus's remembered High value. Reflection resolution was already 128 (index 0), so its independent performance warning did not apply.

The development instance backup is `artifacts/startup-settings-cb0c1f7c25ca432c8131a7514929c5c1/settings.cfg` (the Kopernicus user override was also backed up). Only the PQS preset line changed, from Low to High. No player save, ring definition, terrain generator, plugin identity or mod dependency changes. This setting affects stock/planet-pack PQS elevation, so users with landed planetary vessels should retain a save backup before changing it. Ringworld's cylindrical geography does not use PQS terrain detail. The bridge does not force a global graphics change in players' installations.

Do not apply this explanation to a **missing textures**, **failed planetary system**, or **unsupported KSP** warning. Those require the actual message/log. The inspected Kopernicus log successfully loaded `NivenRingworldHost`; the initial startup probe reported zero missing-texture entries.

## Verification

`tools/VerifyStartupWarnings.ps1` temporarily installs a smoke-only, read-only observer and starts an owned KSP process without opening a save. It observes at least 150 menu frames (120-second limit), records active popup text and Kopernicus missing-texture entries, exits and restores the normal base binary. It does not dismiss popups or edit graphics settings. The normal DLL contains no observer.

Before: `template_instance/StartupWarnings-before.log` records Terrain=Low, reflection=0, Parallax's Terrain Detail error and zero Kopernicus missing textures. The initial observer used a 12-second wait; only the later observer guarantees enough frames for Kopernicus's periodic check. **After:** `template_instance/StartupWarnings-high.log` records 150 observed menu frames, Terrain=High, reflection=0, zero active popups and zero Kopernicus missing textures. The verification script completed successfully and restored the normal base binary. The core suite passed 143,203 checks; both C# builds had zero warnings/errors. The user confirmed the Kopernicus text was the terrain-detail-change warning. Startup checks opened no saves. Unrelated upstream shutdown exceptions remain in the logs and are not described as fixed by this graphics-setting correction.

No release archives were rebuilt. Dependency metadata and minimum versions remain unchanged.
