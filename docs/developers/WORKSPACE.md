# Shared development workspace

The Windows development layout is:

```
Ring World KSP mods/
  Ring World KSP/        base Git repository
  Ringworld Clouds/     optional cloud repository
  Ringworld Scattering/ optional water/atmosphere repository
  template_instance/    shared KSP test installation (never distribute)
  Ring World KSP mods.code-workspace
```

Open `Ring World KSP mods.code-workspace` with **File → Open Workspace from File** in VS Code, or open the parent directory as a folder. Each child retains its own Git history and remote. The KSP installation is deliberately outside every repository.

For Codex, add/open the parent as the project folder. Where the app supports multi-folder projects, use the project's menu → Edit project → Add folder, and set the parent as primary for new tasks. An existing task can retain its original working directory; start a new task in the parent when necessary. [Official project guidance](https://learn.chatgpt.com/docs/projects).

Build the base first with `./build.ps1 -Install`, then run the same command in either extension repository. Rebuild shaders only when shader sources change using `./build-visuals.ps1`. The base smoke harness uses the sibling `template_instance`; all installations target that shared instance. Close KSP before replacing DLLs. Release ZIPs continue to extract into the KSP root with top-level GameData.

The old base directory was held open by an editor during relocation. Its contents were moved; the remaining empty directory can be removed after closing old editor windows.
