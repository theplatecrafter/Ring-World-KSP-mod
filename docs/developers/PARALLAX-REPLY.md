# Terrain integration discussion

Copy-ready reply to the Parallax developer:

> Thanks! It uses a custom chunk-based terrain system, not PQS. There is no spherical Ringworld CelestialBody or Kopernicus/PQS terrain controller; the vessel retains the host star as its stock reference body.
>
> Terrain is sampled deterministically from a seed in double-precision cylindrical coordinates: distance along the ring, distance across its width, and height above the inner floor. Near the vessel, SurfaceStreamer generates Unity MeshFilter/MeshRenderer chunks with MeshColliders. TerrainLod supplies progressively coarser distant meshes; a separate scaled-space representation shows the whole ring. The nearby terrain and vessel run in a rotating, surface-relative frame to avoid the enormous absolute coordinates and tangential velocities. Chunk transforms therefore need to follow frame/origin changes.
>
> We currently place our own biome-dependent trees/ground clutter and use a cheaper distant forest representation. We can expose chunk creation/removal notifications, meshes, local-to-world transforms, sampled biome/density masks and the local surface-up direction, but we do not currently provide a Parallax/PQS adapter.
>
> Is there an entry point into Parallax Continued's scatter distribution/culling/instancing that can accept these custom meshes and masks without a PQSQuad? If not, which parts would be practical to reuse behind a custom terrain-provider adapter? I'd prefer to integrate with Parallax rather than duplicate its renderer. Please let me know what mesh/UV/mask data and lifecycle hooks you would need.

Repository: https://github.com/theplatecrafter/Ring-World-KSP-mod

The public CKAN version reported by players is **v1.1.3**. Unpublished extension work must not be described as already available to those players.

## Follow-up: mesh interface available in development

> Thanks, that is helpful! I have now exposed a read-only loaded-chunk interface, `RingworldTerrainApi.GetLoadedChunks()`. Each entry supplies a lifetime ID, ring ID, Unity mesh (vertices/normals/triangles/UVs), live transform, and double-precision along/across origin and size. Consumers can detect added/removed chunks by comparing IDs; meshes must not be modified and GPU buffers should be released when an ID disappears. Transforms track floating-origin and rotating-frame changes. `RingworldSurfaceApi.TryGetEnvironmentAtPosition` supplies cylindrical altitude, terrain clearance, biome and local up, given a loaded ring-vessel context.
>
> This is available in the development source, not public v1.1.3. There is still no fake PQ/PQS object. Would these inputs be sufficient for a custom terrain entry point into ScatterSystemQuadData, with the spherical direction/altitude and biome operations supplied by the adapter? I can adjust the interface to match what your investigation needs.

The [Continued implementation](https://github.com/Gameslinx/Parallax-Continued/blob/master/Mod%20Source/Parallax/PQS%20Mods/ScatterSystemQuadData.cs) exposes vertex/normal/index compute buffers but also PQ, planet normals/radius, subdivision and PQS map-decal state. Therefore supplying mesh arrays is useful groundwork, not a working Parallax integration. The current registry exposes close-range surface chunks only. It does not yet supply far-LOD meshes, biome masks, scatter exclusion masks, lifecycle events or automatic suppression of Ringworld's built-in scatters.
