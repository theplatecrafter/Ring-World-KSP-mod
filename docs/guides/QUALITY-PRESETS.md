# Graphics, performance and photo mode

Presets are selected from the Settings dropdown and apply immediately to this save. Save the game to persist them. Manual edits still use Apply settings. The dropdown reads Custom when current settings do not match a preset. Presets do not change KSP graphics preferences, seed, biome/physical tree distribution, ring dimensions, gravity, weather timing, science or flight controls.

All names are relative cost tiers, not hardware guarantees. Simple atmosphere rendering does not use volumetric ray steps, so some lower tiers differ chiefly in ground details and weather particles. Stock texture quality, antialiasing and scatter remain respected. The horizon has no artificial upper cap when entered manually.

| Preset | Horizon (km) | Terrain subdivisions | Blocks/frame | Forest | Atmosphere | Water | Ground detail (m) |
|---|---:|---:|---:|---|---|---|---:|
| Absolute Cow | Whole ring¹ | 32 | 4 | Ultra | Full-resolution | Ultra | 250 |
| Extra Beefy | 1,000,000 | 32 | 3 | Ultra | Full-resolution | Ultra | 225 |
| Beefy | 500,000 | 32 | 2 | Ultra | Full-resolution | Detailed | 200 |
| Strong | 320,000 | 16 | 2 | High | Half-resolution | Detailed | 175 |
| Good | 240,000 | 16 | 2 | High | Half-resolution | Waves | 150 |
| Mid | 160,000 | 16 | 1 | Low | Half-resolution | Waves | 125 |
| Slow | 160,000 | 8 | 1 | Economy | Simple | Ripples | 100 |
| Better Potato | 160,000 | 8 | 1 | Economy | Simple | Ripples | 75 |
| Potato | 160,000 | 8 | 1 | Economy | Simple | Ripples | 50 |
| Aged Potato | 160,000 | 8 | 1 | Economy | Simple | Flat | 35 |
| Rotten Potato | 200 | 8 | 1 | Economy | Simple | Flat | 25 |

## Biome features: forests

Forest LOD quality is independent of atmosphere and can be changed after an expedition begins. Economy uses one low-polygon near canopy mesh per patch with approximately one-quarter of the visual crowns, enlarged to represent tree groups. The complete physical tree list is retained for nearby pooled trunk contacts. Far woodland remains recognizable through canopy colour and relief, without intermediate crown meshes. Low/High/Ultra use the former Laptop/High/Ultra crown budgets (8/16/24 candidates per side on larger blocks; ranges bounded by 8/16/32 km block sizes). Photo mode uses the forest quality of its selected temporary preset.

Economy significantly reduces forest mesh construction, memory and drawing; it does not skip all tree sampling or alter ground collision. It is deliberately coarse. Quality changes rebuild at most one existing scenery tile per frame. Terrain and distant canopy queues use their existing incremental budgets. This is not GPU instancing or an FPS guarantee.

Near trunks remain limited to the existing contact radius. World forest density is a separate generation parameter, locked after visiting the ring; visual quality remains editable. 

## Atmosphere backend and photo selection

Mid and above select Cyla; Slow and below select the original lightweight atmosphere. Reselect a preset to update older saved settings. Backend selection remains manually editable. Reducing Cyla resolution alone does not disable the GPU cloud pass; the low presets now bypass both expensive paths.

Both photo buttons offer the same eleven preset choices (default Beefy). The temporary selection controls terrain horizon and mesh resolution, forest quality, water, atmosphere backend and samples. Low presets capture the simple scene; high presets use tiled volumetric refinement. Resume or Cancel restores the prior gameplay settings without saving the photo choices as gameplay options. Photo mode freezes time and offers independent output resolution through 16K, retaining screen aspect ratio and rejecting outputs beyond GPU texture/memory limits.

With Ringworld Parallax installed and enabled, the photo preset also selects scatter distance, density, visible-object capacity and generation budget. Photo mode generates detail around the framed camera and waits for that population before capturing, so newly loaded grass, rocks and trees appear in the saved image. Higher presets can take longer to prepare. Per-profile distance limits and stock KSP scatter density still apply. Rotten Potato has zero Parallax density. A disabled extension stays disabled; Resume and Cancel restore your custom gameplay values.

¹ Absolute Cow sets horizon to half the circumference plus ring width, the renderer's meaningful whole-ring extent. Custom input remains uncapped. It uses 32 terrain subdivisions, four blocks/frame, Ultra forests, full-resolution atmosphere, 256 cloud steps, 96 Original air steps, 500 Cyla view steps, 50 Cyla light steps, 20,000 km cloud distance (v1.1.5 development; see extension guide), full cloud shadows, waves, 250 m close details and 64 photo samples. Brightness, weather, world generation and stock KSP settings are not quality sliders and are not maximized.

Rotten Potato uses the 200 km minimum horizon, eight subdivisions, one block/frame, Economy forests, original/simple atmosphere, simple water, minimum sample budgets, 25 m details and one photo sample. Full-ring detail, particles, rain/lightning effects, cloud shadows and waves are disabled. Other low presets preserve their 160,000 km terrain horizon. Changes affect visual cost, not physical tree distribution or weather simulation.
