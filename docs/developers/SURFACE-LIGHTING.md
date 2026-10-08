# Surface lighting, shadows and scatter colour

## Report and findings

The October 8 screenshots show changing illumination on the Aeris 4A, coarse projected shadows and white grass in Grassland. The reported shared-instance arrival used seed 408656951 at along 53,993,880,082.137 m / across 76,762,176.0651363 m. Logs and graphics settings are preserved under `artifacts/diagnostics/surface-lighting-20261008`.

The runtime shadow inventory reports a 50,000 m range, Low shadow resolution and two cascades. That spreads the available shadow texels across a much larger area than the nearby craft and ground require. Scatterer's installed configuration supplies the large range; this is a ring-camera precision issue, not evidence that Scatterer is broken on planets. The original comparison capture shows the coarse shadowing darkening broad areas of the plane; the localized range retains the craft's surface detail.

Parallax's installed Kerbin grass distribution sets `coloredByTerrain = True`. The bridge previously put white into every instance's colour channel, including this profile. Its gradient texture and bright material multiplier consequently produced white blades. Correct terrain tint is a separate data-contract fix, rather than lowering the exposure of the whole scene.

## Shared rendering context

`RingStellarLighting` uses the configured primary star's nominal intensity and Ringworld sunlight visibility consistently during local camera renders. The stock spherical-horizon fade is not a reliable source of ring brightness. Direction still comes from double-precision stellar ephemerides and the rotating ring frame. The native key light is shared by terrain, vessels and installed Parallax materials; the habitat light remains a fallback. Panels and physical eclipses continue to control visibility.

The same camera scope limits the shadow range to 600-6,000 m based on the rendering camera's height above ring terrain, never exceeding the existing range. It uses stable projection and concentrates existing cascades near the camera. Excessive primary-light bias is bounded locally. Resolution, cascade count and the user's shadow enable/disable selection remain unchanged. The scope restores distance, projection, splits, bias and lighting state after rendering. Map and planetary cameras retain their settings.

Low-resolution shadows can still show aliasing, especially at long camera distances. This treatment spends the existing budget more effectively; it is not unlimited shadow resolution or a new shadow renderer. Large terrain/body/panel eclipses remain part of the separate Ringworld visibility model rather than depending on this local shadow range.

## Scatter materials

Terrain-coloured profiles receive `TerrainTint.Color(sample)` in their existing instance buffer. That is the same base colour used by Ringworld's terrain, including older generator biome colours. Profiles that do not request terrain colouring keep white as the neutral multiplier, retaining their texture/material colour. No upstream models, textures or compiled shaders are altered or bundled.

Texture loading now checks the installed shader's declared texture dimension. Cube properties such as `_RefractionTexture` load a cubemap instead of guessing the type from the property name. The earlier 2D-to-Cube assignment errors no longer occur in the focused runtime check.

Reference interfaces: [Parallax terrain colouring](https://github.com/Gameslinx/Parallax-Continued/blob/master/Changelog), [scatter shader properties](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/GameData/ParallaxContinued/Config/ScatterShaderBank.cfg), [instanced shader](https://github.com/Gameslinx/Parallax-Continued/blob/master/Assets/Shaders/Scatters/InstancingShaders/InstancedSolid.shader). These are reference reads; upstream source files under local diagnostic artifacts are not part of the extension distribution.

## Verification

The focused runner is `-ringworld-smoketest -ringworld-moving-landing-only -ringworld-surface-lighting-only`. It uses a fresh isolated Aeris test save at the reported coordinates and Slow quality. It compares the native shadow range with the local range, checks one stable key light over repeated paused renders, checks restoration of shadow settings, verifies actual terrain-coloured grass output and material texture dimensions, then observes ordinary moving-camera renders at a 170 m/s approach speed. Runtime results are appended after the final run.

`SurfaceLighting-final.log` passed. The native 50 km range was reduced to 1,536.908 m for the above-craft view, retaining Low resolution and two cascades. Thirty repeated paused renders retained one consistent key light and restored shadow settings. The moving observation passed 120 camera views with all 40 Aeris parts intact. Grass produced 439 candidates and visible LOD counts 1/34/98; the close-up capture shows green blades and white daisy petals rather than white grass. Shader texture dimensions passed without the prior `_RefractionTexture` assignment errors. The final diagnostic images are `native-range.png`, `local-range.png` and `terrain-tinted-grass.png` in the diagnostic directory. This verifies the shared lighting state and the reported view; it does not certify every moving scene or eliminate low-resolution aliasing.

Base core suite: 110,600 checks passed. Bridge placement suite: 18,050 checks passed. Both plugin builds compile without warnings. No dependency or version changes, release archive, Git operation or publication are included. Other GPUs, presets above Slow and every possible visual-mod combination remain unverified.
