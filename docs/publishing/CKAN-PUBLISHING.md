# CKAN publishing

## Development and release ownership

The [workspace workflow](../../../AGENTS.md) governs local development. The development assistant edits ordinary files, builds and tests locally, and packages ZIPs only when requested. All Git operations, GitHub/SpaceDock publication, and NetKAN pull requests are handled by the project owner. Publishing steps below are instructions for the owner, not authorization for automated publication.

Each component's `RELEASE-NOTES.md` must report required dependencies and version constraints, optional/suggested integrations, configuration-provider conflicts, installation layout, and validation limits for the version being prepared. Configuration release notes must distinguish the requirements of all four packs.

Authoritative metadata lives in the sibling **NetKAN/NetKAN** checkout on the Ringworld branch. Do not generate distribution copies, .ckan assets or metadata.json files in the base, extensions or config packs. Submit changes from that checkout to KSP-CKAN/NetKAN.

The base entry has GitHub and SpaceDock (listing 4573) download sources. Harmony2 >= 2.2.1.0 is required. Instance config packs are optional; the built-in default ring remains available without one. Config providers require base >= 1.1.7 and each provides/conflicts with NivenRingworldConfig to select one replacement system. Interstellar providers additionally require Kopernicus and ModuleManager. Ringworld Scattering 1.0.1 requires base >= 1.1.7; Clouds 1.0.1 retains base >= 1.1.5. Cyla and both extensions are optional to the base runtime.

Manual installations can omit a config pack to retain the built-in default ring. All archives extract into the KSP root, merging GameData. Dependencies are downloaded separately, never bundled. Each config has its own SpaceDock listing and ZIP. GitHub publication does not upload files to SpaceDock. For listings with both download sources, keep versions synchronized on both hosts; submit/merge metadata and allow NetKAN to index them.

Update RELEASE-NOTES.md, version files and project versions before packaging. Validate ZIP/source equality and dependency constraints. Keep limitations explicit: interstellar lifecycle is not yet certified, and graphics testing does not cover every GPU/API. Do not rewrite historical release assets to change their dependencies.
