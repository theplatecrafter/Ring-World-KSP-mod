# NetKAN compatibility review — 15 October 2026

## Workflow

The assistant maintains ordinary local files under `NetKAN/NetKAN` alongside the code and release notes. The owner handles all Git operations, commits, publication and NetKAN submissions. Do not generate component-local metadata copies. Already prepared ZIPs are immutable for this task; no package/version bump is needed for this metadata correction.

## Base-version ranges

| Extension release | Base minimum retained | Evidence |
| --- | --- | --- |
| Clouds 1.0.2 | 1.1.5 | Current normal source compiles against released 1.1.5 and 1.1.9 SDK DLLs |
| Scattering 1.0.3 | 1.1.8 | Current normal source compiles against released 1.1.8 and 1.1.9 SDK DLLs |
| Parallax 1.0.1 | 1.1.8 | Current normal source compiles against released 1.1.8 and 1.1.9 SDK DLLs |

All six builds succeeded in isolated output directories; normal source build outputs, installed binaries and release ZIPs were not overwritten. SDKs were extracted from retained release ZIPs, with hashes recorded in `artifacts/netkan-compatibility-20261015/compile-results.json` at the workspace root. Test-only profiler references are absent from these normal builds.

The Scattering visibility hook uses a reflected optional method. Older hosts, including 1.1.9, do not supply it and retain the existing scene-copy behaviour. New performance benefits supplied by base 1.1.10 do not turn into an unconditional dependency. Clouds' coverage/march shader changes are extension-owned. Parallax's new culling/allocations use the existing scatter/photo/terrain API.

The new base is recommended for its own lag/streaming fixes. Keep these as minimum ranges, not exact-version pins. Existing base `suggests` lower bounds remain compatible; CKAN can select newer releases within them. No change is needed to the base, Clouds, Scattering or config recipes for these release version numbers alone. Configs remain 1.0.0, minimum base 1.1.7; each replacement provider keeps provides + conflicts NivenRingworldConfig.

## Actual metadata correction

`RingworldParallax.netkan` now explicitly requires:

- `ParallaxContinued-Scatter-Textures >=1.0.0`, the stock scatter sources used by the shipped profiles;
- `KSPTextureLoader >=1.0.36`, the bridge's documented/tested texture-loading API.

The previous recipe obtained these packages transitively through Continued/Kopernicus, but Kopernicus's >=1.0.26 loader bound did not enforce the bridge's declared >=1.0.36 contract. The bridge calls TextureLoader.LoadTexture/TextureHandle directly, so it should declare that requirement itself. These modules were already in the manual dependency graph; this adds no new downloaded module to the prepared pack. It does not establish that every earlier loader build is broken; it avoids allowing an unvalidated lower API floor.

Keep `NivenRingworld >=1.1.8` and `ParallaxContinued >=1.0.3`. Rendering/asset tests use Continued 1.0.4, stock packs 1.0.0 and KSPTextureLoader 1.0.36; exact upstream 1.0.3 rendering and arbitrary future layouts are not certified. Upstream Parallax/plugins/assets remain separate downloads under their redistribution terms.

## Validation

Eight local recipes parse and retain valid relationships, install roots, source/version extraction and config exclusivity. There are no exact base-version pins. The updated resolver selects exactly the same modules/versions as prepared modpack 1.0.2. All nine resolver/archive tests pass, including a fixture where Kopernicus-compatible loader 1.0.26 is rejected by the bridge's explicit requirement. Full remote NetKAN inflation/CI is not run; the owner submits and checks it.

Future manual builds use the authoritative NetKAN relationship definitions directly instead of the temporary Parallax/Scattering overrides. The reviewed download lock still pins exact upstream versions/hashes and controls redistribution. Metadata minimum ranges and reproducible manual download pins have different purposes.

A separate private KSP 1.12.5 / D3D11 fixture loads the **actual released base 1.1.9 DLLs/assets** with the three already packaged current extension DLLs. The read-only observer exercises discovery, Clouds material/controller/weather updates, Scattering's absent-visibility-API fallback and scene-copy lifecycle, and Parallax layer/photo lifecycle. **Result: PASS.** All four checks completed with the actual released host 1.1.9 and current released extension payloads. The probe build completed with zero warnings/errors. The runtime log is `Ringworld Modpacks/artifacts/validation-game-80817f8666cc44918cf6d0767d0a4f97/MetadataCompatibility.log`; the machine-readable receipt is workspace `artifacts/netkan-compatibility-20261015/review.json`. This is an API/runtime contract probe, not a full Flight/underwater test or upstream Parallax scatter draw. Complete renderer and photo checks against base 1.1.10 are recorded in the rendering/release evidence. Exact minimum-host runtime coverage is not inferred solely from compilation.
