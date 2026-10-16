# Shared development workspace

## Development and release ownership

The [workspace workflow](../../../AGENTS.md) governs local development. The development assistant edits ordinary files, maintains the authoritative local `NetKAN/NetKAN` metadata, builds and tests locally, and packages ZIPs only when requested. Dependency ranges must reflect supported APIs/assets rather than automatically requiring each latest release. All Git operations, GitHub/SpaceDock publication, and NetKAN pull requests/submissions remain with the project owner. Local metadata maintenance does not authorize commits or remote operations.

Each component's `RELEASE-NOTES.md` must report required dependencies and version constraints, optional/suggested integrations, configuration-provider conflicts, installation layout, and validation limits for the version being prepared. Configuration release notes must distinguish the requirements of all four packs.

The Windows development layout is:

```
Ring World KSP mods/
  Ring World KSP/        base Git repository
  Ringworld Clouds/     optional cloud repository
  Ringworld Scattering/ optional water/atmosphere repository
  Ringworld Configs/    instance configuration packs
  NetKAN/               assistant-edited metadata; owner manages Git/submission
  template_instance/    shared KSP test installation (never distribute)
  Ring World KSP mods.code-workspace
```

Open `Ring World KSP mods.code-workspace` with **File → Open Workspace from File** in VS Code, or open the parent directory as a folder. Each child retains its own Git history and remote. The KSP installation is deliberately outside every repository.

For Codex, add/open the parent as the project folder. Where the app supports multi-folder projects, use the project's menu → Edit project → Add folder, and set the parent as primary for new tasks. An existing task can retain its original working directory; start a new task in the parent when necessary. [Official project guidance](https://learn.chatgpt.com/docs/projects).

Build the base first with `./build.ps1 -Install`, then run the same command in either extension repository. Rebuild shaders only when shader sources change using `./build-visuals.ps1`. The base smoke harness uses the sibling `template_instance`; all installations target that shared instance. Close KSP before replacing DLLs. Release ZIPs continue to extract into the KSP root with top-level GameData.

The old base directory was held open by an editor during relocation. Its contents were moved; the remaining empty directory can be removed after closing old editor windows.

## Parallax bridge development (7 October 2026)

The owner explicitly requested the local base branch `parallax-continued-bridge` from the current commit and a new local sibling repository `Ringworld Parallax`. Those two repository operations are a task-specific exception to the ordinary no-Git workflow. No remote repository, push, release or PR is authorized by that exception. The base remains on that branch and the extension is a separate local working directory. Parallax-only preview disables native surface scatters while preserving buildings; a saved fallback toggle restores them for comparisons.
