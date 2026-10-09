# Terrain textures and the exterior hull

These changes belong to the unpublished development build. Install the matching base DLL and base visual bundle together and restart KSP.

## Ground appearance

Every terrain biome has subtle procedural surface detail without requiring Parallax or another texture pack. Existing biome colours remain the base colour. Soil, sand/seabeds, rock/ruins, snow, scrith/walls and roads have different detail strengths. Full modulation is bounded to 0.8–4% according to material; soil uses 3.5%. Typical variation is smaller. Geometry supplies the normals, so there is no embossed grain or bump pattern.

Detail is evaluated directly by a texture-free 3-D gradient simplex shader, similar in purpose to Perlin noise. Smooth domain warping gives broad patches less regular outlines. There is no ground texture tile, placement grid, rectangular tile crossfade or 64 km periodic reset. The discarded texture-array generator and asset are removed. The [Ashima Arts / stegu simplex reference](https://github.com/ashima/webgl-noise/blob/master/src/noise3D.glsl) supplies the corner-ordering reference; its MIT notice is retained in THIRD-PARTY-NOTICES.md. Ringworld supplies the world-coordinate hash and filtering. Squad assets are not copied.

The field uses the canonical three-dimensional cylindrical surface, independent of floating origin, ring inclination and current spin. Adjacent chunks query the same world field, and the physical cylinder closes at the ring seam. Integer lattice origins are packed as 64-bit values; only relative vertex offsets become floats. The 65,536 m origin block is a precision partition, not a repeat interval: its full signed index participates in the hash. The field remains deterministic for the ring's seed.

Fine 0.25–1 m grain fades over 80–400 m; projected pixel size can soften it sooner. Broad noise spans 64–512 m, blending toward a 4,096 m field over 2–16 km. Filtering removes small features instead of sharpening them at a mesh transition. Camera distance is measured in physical metres in both local and scaled space. Terrain elevation, colliders, resources and biome classification are unchanged.

Near meshes and terrain LOD skirts expose scrith/road weights in UV channel 3, soil/sand/rock/snow weights in UV channel 4 and relative skewed noise positions in UV channel 5. Per-renderer property blocks carry the packed origin and seed. Original colour buffers and UV channels remain intact for the Parallax bridge. See `TerrainSurface`, `TerrainSurfaceDetail` and `TerrainDetail.cginc` for the coordinate and shader implementation.

## Viewing the ring from outside

The underside uses a separate, dark scrith material. Interior terrain colour overlays, distant clouds and the Scattering extension's full-ring atmosphere reject cameras below the hull within its width. The classification runs in double-precision physical ring coordinates before shader values are converted to floats. This avoids losing the small separation between the hull and a camera when the ring radius is enormous.

The existing per-ray hull tests remain for views through the open rim. A camera inside the habitat, including below a water surface but above the underside, still sees its interior layers. Shadow panels remain physical structures; the exterior hull does not receive the habitat's alternating day/night colour overlay.

The atmosphere correction requires the matching Ringworld Scattering shader bundle. Older installed extension shaders cannot consume the new exterior-camera flag. Ringworld Clouds uses the base global cloud shell; no Clouds binary change is needed for that shell's exterior rejection.

## Requirements and validation

Required dependencies remain KSP 1.12.5 and separately installed Harmony2 >= 2.2.1.0. Cyla, Ringworld Clouds, Ringworld Scattering and Ringworld Parallax remain optional. Texture detail itself requires no new external dependency. Terrain API version and the Parallax mesh contract are unchanged. No release archive or remote publication is included in this work.

Validation results are recorded in the base release notes. Diagnostic captures and logs are stored locally in `artifacts/diagnostics/terrain-textures-20261008`. Full flight verification uses Slow on the development laptop; other hardware and higher gameplay presets are not certified by these checks.
