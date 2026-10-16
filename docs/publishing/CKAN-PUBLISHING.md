# CKAN publishing

## Development and release ownership

The [workspace workflow](../../../AGENTS.md) governs local development. The development assistant maintains ordinary local mod files and the authoritative `NetKAN/NetKAN` metadata, validates dependency ranges/archive paths, builds and tests locally, and packages ZIPs only when requested. All Git operations, GitHub/SpaceDock publication, and NetKAN pull requests/submissions remain with the owner. File-edit authorization does not permit commits or remote operations.

Each component's `RELEASE-NOTES.md` must report required dependencies and version constraints, optional/suggested integrations, configuration-provider conflicts, installation layout, and validation limits for the version being prepared. Configuration release notes must distinguish the requirements of all four packs.

Authoritative metadata lives in the sibling **NetKAN/NetKAN** directory. Maintain those files directly and report any dependency changes with compatibility evidence. Do not generate distribution copies, .ckan assets or metadata.json files in component projects. The owner submits changes to KSP-CKAN/NetKAN.

The base entry has GitHub and SpaceDock (listing 4573) sources. CKAN requires Harmony2 >=2.2.1.0, ModuleManager >=4.0.0 and one NivenRingworldConfig provider. Config providers require base >=1.1.7 and provide/conflict with NivenRingworldConfig so CKAN selects one replacement layout. Interstellar providers additionally require Kopernicus and ModuleManager. Manual installations can retain the built-in default without a replacement config.

Clouds 1.0.2 retains base >=1.1.5. Scattering 1.0.3 and Parallax bridge 1.0.1 retain base >=1.1.8; base 1.1.10 is recommended, not a new hard minimum. The new Scattering water-visibility hook is optional and falls back to the previous copy behaviour on older hosts. Parallax additionally declares ParallaxContinued >=1.0.3, ParallaxContinued-Scatter-Textures >=1.0.0 and KSPTextureLoader >=1.0.36; its complete upstream dependency stack is transitive and remains a separate download for manual users. Actual upstream rendering tests used Continued 1.0.4. These are optional extensions to the base. Existing suggested-version lower bounds remain valid; CKAN can choose newer compatible releases without raising those bounds for every patch. See the [compatibility audit](../developers/NETKAN-COMPATIBILITY.md).

Manual installations can omit a config pack to retain the built-in default ring. All archives extract into the KSP root, merging GameData. Dependencies are downloaded separately, never bundled. Each config has its own SpaceDock listing and ZIP. GitHub publication does not upload files to SpaceDock. For listings with both download sources, keep versions synchronized on both hosts; submit/merge metadata and allow NetKAN to index them.

Update RELEASE-NOTES.md, version files and project versions before packaging. Validate ZIP/source equality and dependency constraints. Keep limitations explicit: interstellar startup/arrival and retained-save transitions have targeted coverage, while every craft lifecycle, graphics backend and GPU combination is not certified. Do not rewrite historical release assets to change their dependencies.
