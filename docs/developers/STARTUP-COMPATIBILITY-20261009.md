# EVA patch startup failure on an existing save

## Report and cause

The reporter upgraded an existing 1.1.4 save to 1.1.7, installed Kerbol Standard, and retained WildBlue parts. Craft still reported Ringworld residence, but the ring and launcher were absent. The supplied exception identifies `EvaTransitionSpeedPatch`: its transpiler throws if a target EVA method no longer contains a direct `Vessel.horizontalSrfSpeed` field read.

`StockIntegration.Install` runs before `ScaledRing.Start` creates its root and before `RingworldFlight.Start` creates the launcher. The exception aborts both initializers. Reinstalling the same DLL cannot repair this. This evidence does not identify KFS, another mod, or a particular KSP build as the source of the changed instruction stream. The reporter's exact game assembly, full mod list and save were not available for this local test.

[Harmony chains transpilers](https://harmony.pardeike.net/articles/patching-transpiler.html), so an earlier patch can move a field read into a helper and invalidate a subsequent literal instruction search without making the game assembly incompatible.

## Local fix

The four EVA transition methods now refresh shared ring-relative surface caches in a late-priority prefix. Original method bodies and other mods' transpilers remain intact. There is no required speed-read instruction, no speed cap, no global host-body modification, and no catch-all suppression of patch failures. Other essential stock patches retain their installation checks. `HorizontalSpeed` remains available for harvesting initialization.

Ordinary non-ring vessels, unloaded/packed vessels and unrelated frames are excluded by the existing shared cache scope. This composes with another mod that reads the same cache through a helper; it cannot force an arbitrary controller to use ring-local conditions if that controller calculates its own planetary speed.

## Save and installation behavior

Legacy `OPTIONS` and residence records without ring IDs migrate to `primary`. Saved dimensions, seed and host override newly installed instance configuration packs. Packs initialize new layouts rather than replacing occupied existing ones. Back up the save; do not delete its Ringworld scenario to troubleshoot missing rendering.

The independent base, extensions and config packages intentionally use their own GameData folders. KSP's GameDatabase loads configs throughout GameData; they need not reside under NivenRingworld. Preserve the shipped paths, particularly because optional-extension assets and distant-star activation depend on them. Standalone ZIPs merge GameData at the KSP root. A separately requested manual modpack already bundles selected components and required dependencies. Directory layout is independent of the reported patch exception.

Current metadata requires Harmony2 >= 2.2.1.0, ModuleManager >= 4.0.0 and one NivenRingworldConfig provider. Interstellar packs also require Kopernicus and its dependency stack. A working 1.1.4 installation does not by itself prove all 1.1.7 dependencies are installed. No dependency or NetKAN change accompanies this fix.

## Reproduction and validation

`tools/VerifyStartupCompatibility.ps1` runs in the private required-only modpack validation instance, not the user's save or installation. It uses Slow, saves/restores DLLs and settings, and restores a normal source build. The smoke-only fixture moves speed reads into a pass-through helper in all four real KSP EVA methods, reproduces the released patch's exact exception, then installs the corrected patches with the competing transpilers retained. It loads a synthetic legacy single-ring scenario while a replacement interstellar pack is installed, checks rendering/launcher/residence migration and resaving, and exercises native EVA contact/recovery/walking.

The synthetic save tests legacy format handling with a current stock Mk1-3 single pod, retaining the training vessel's identity/crew; it is not the reporter's actual 1.1.4 save or WildBlue craft. The competing patch is a controlled reproduction, not proof that a named upstream mod made the same change.

`StartupCompatibility-4.log` passed on KSP 1.12.5 / Harmony 2.2.1.0 at Slow:

- The controlled competing transpiler reproduced the exact released exception. All stock patches then installed successfully and a second Install call left one Ringworld prefix per target; all four competing transpilers remained installed.
- Legacy OPTIONS/residence records without ring IDs loaded as primary. Sun host, 15.3-billion-metre radius and saved seed survived the installed full-size interstellar replacement pack. The scaled ribbon and launcher existed. The resident restored to its saved chart location and resaving retained its ring identity.
- Ordinary hatch release, contact, recovery through movement input and walking passed with damage enabled. Walk distance was 2.678934 m.
- A real patched heading transition consumed local speed 0.6889297 m/s after its cache was deliberately set to 385,637 m/s. The other mod's pass-through helper remained in that method. An unregistered vessel's cache was unchanged.
- All 110,644 core checks passed. The normal build and original private-instance DLLs/settings were restored; the corrected normal build was installed into the shared template instance separately.

Runs 1-3 exposed test-fixture assumptions (an orbital-only baseline assertion, an assumed second vessel, and the deprecated tutorial pod's obstructed hatch). These were corrected in the smoke harness, not worked around in production hatch/contact code. Run 4 is the complete passing regression. The reporter's game build, save and full WildBlue combination still need confirmation; this is not a blanket compatibility certification.

This is unpublished development work. Release ZIPs and the previously built manual modpack have not been updated.
