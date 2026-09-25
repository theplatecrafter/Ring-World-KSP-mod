# Resources, gravity and buoyancy integration

## Availability

These interfaces and adapters are unpublished v1.1.5 development work. Public CKAN v1.1.3 does not contain them. The older Sun / LANDED scanner report comes from retaining the host star as KSP's reference body; ring science subjects and resource scanning are separate systems.

## Resource definitions

Ringworld reads stock `ResourceCache` definitions after configuration loading, including ModuleManager changes. For crustal resources (`ResourceType = 0`), selection proceeds from `GLOBAL_RESOURCE` to matching `PLANETARY_RESOURCE` to matching `BIOME_RESOURCE`; the last matching entry within a level wins. Prefer `PlanetName = Ringworld:<persistent ringId>` (for example `Ringworld:primary`). Ring ID and display-name aliases are also accepted. `BiomeName` uses the terrain biome enum: Ocean, Lake, River, Wetland, Grassland, Forest, Desert, Mountain, Snow, Scrith, Ruins, Rimwall or Road. These are terrain biomes, not research landmark names.

`Resources.cfg` supplies default Ore for the primary ring and an explicit zero-presence Scrith override. Other resources can inherit global distributions or receive ring-specific nodes. Sun-specific nodes never become ring definitions. Ring IDs and terrain seeds keep distributions independent and stable across restarts.

Supported distribution fields are PresenceChance, MinAbundance, MaxAbundance, Variance and Dispersal. Abundance inputs are percentages; `RingworldResourceApi.TryGetAbundance` returns a 0..1 fraction. Presence/min-max selection uses deterministic hashes; variance uses continuous ring terrain noise, with dispersal controlling feature size. This is a cylindrical distribution model, not a bit-for-bit reproduction of stock spherical abundance noise. Altitude/range distribution fields and resource types 1..3 are not implemented. Unknown definitions return unavailable, never host-star abundance.

Stock local scanner display and the base ModuleResourceHarvester abundance call use this shared provider. The drill retains stock activation, recipe, resource consumption, heat and impact checks. Its impact test is a layer-15 raycast; the ring's terrain colliders already use that layer. No artificial contact is granted. A scanner identity correction does not unlock a Sun biome.

Limitations: orbital survey/discovery persistence, depletion, packed/background extraction and independently implemented third-party harvesters remain unsupported. Reading a definition does not automatically redirect every mod's ResourceMap calls. Providers should call the ring API with the actual vessel rather than infer ring identity from mainBody.

## Environment API v5

`RingworldSurfaceApi.TryGetEnvironmentAtPosition(contextVessel, worldPosition, out state)` samples the currently loaded rotating frame. It returns false without a restored, unpacked ring vessel context or outside the ring arrival region. Use Unity world coordinates on the main thread and refresh each physics tick. It supplies effective stationary-frame gravity, local up, terrain clearance/biome, air density, kPa pressure, kelvin temperature, and depth below mean water level. Gravity includes the frame acceleration used by Ringworld; it is not inverse-square central-body gravity. Velocity-dependent acceleration remains available in the vessel surface-state query.

Buoyancy-per-cubic-metre helpers return newtons: `-EffectiveGravity * density`. The airship must still account for gas/payload mass, envelope volume and force-unit conversion (KSP uses kilonewtons). Water uses a 1000 kg/m³ reference density. These values expose a hydrostatic model; they do not replace the existing simplified craft flotation solver or add accurate hull-volume displacement. Visual waves do not change mean water depth.

Do not globally alter the Sun's mass, atmosphere or body ID. Stock orbital integration also consumes those properties. A virtual spherical planet would still have the wrong gravity direction, altitude and surface coordinates for this cylindrical habitat and would require substantial save/orbit migration.

## Experimental optional adapters

- **Hooligan Labs Airships:** detected `HLAirships.HLEnvelopePartModule` methods have their stock gravity, pressure, temperature and density query call sites redirected to the ring environment only for owned ring vessels. Outside the ring the original queries run. This addresses the stock density routine returning zero for an airless host body. The tester subsequently identified Heisenberg Airships; its CKAN metadata depends on Hooligan Labs.
- **Heisenberg static lift:** `WildBlueIndustries.WBIModuleStaticLift` receives the same air queries plus ring gravity and local lift directions for its per-part/per-vessel force paths. The HL-10L part configuration uses this module. Its independent airship-controller altitude tables/autopilot are not adapted.
- **KFS:** detected `WildBlueIndustries.WBIGraviticEngine.UpdateHoverState` reads ring effective gravity and a local lift reference instead of star-radial up. Fuel handling and hover control remain in KFS. The synthetic lift reference exists only inside that direction calculation; no CelestialBody or orbit is modified. A changed method signature skips adaptation. VTOL without hover, warp/translation, gravity overrides and terrain helpers are not covered.

Source audits: [KFS hover engine](https://github.com/Angel-125/FlyingSaucers/blob/master/Source/FlyingSaucers/PartModules/WBIGraviticEngine.cs), [Hooligan Labs envelope](https://github.com/net-lisias-ksp/HLAirshipsCore/blob/master/Source/HLAirships/HLEnvelope.cs). Inspected 2026-09-25; the player's installed versions remain unknown. No third-party implementation was copied. Full craft tests with these mods are required before advertising support.

## Adapter organization

Public APIs are the shared source of data. Optional third-party bridges live in `src/Ringworld.KSP/Compatibility`, separate from stock integration. `GameData/NivenRingworld/Compatibility.cfg` exposes `hooliganLabsAirships`, `heisenbergLift` and `kfsHover`; each defaults true but is installed only when its target plugin exists. Restart KSP after changing these switches. ModuleManager can edit them, for example:

```cfg
@RINGWORLD_COMPATIBILITY:FINAL
{
    @heisenbergLift = false
}
```

ModuleManager changes data; Harmony changes runtime methods. Neither infers how spherical-body calculations should map to a cylinder. Prefer upstream mods consuming the public API; otherwise keep a narrow adapter that changes only their query sites. Adapters currently compile into the base DLL, but independently released bridges can consume the same public APIs and later use separate optional CKAN packages. There is no need for one resource implementation per mod.

Additional inspected sources: [Heisenberg CKAN metadata](https://github.com/KSP-CKAN/NetKAN/blob/master/NetKAN/Heisenberg.netkan), [HL-10L config](https://github.com/Angel-125/Airships/blob/master/GameData/WildBlueIndustries/Heisenberg/Parts/HL10/hl10Large.cfg), [static-lift implementation](https://github.com/Angel-125/Airships/blob/master/AirshipUtils/WBIModuleStaticLift.cs).

## Validation

`RingworldSmoke-20260925-172713.log` passed the live Slow-preset compatibility fixtures: stock-cache global/planet/biome precedence, absent deposits, ring identity isolation, scanner/harvester query agreement, point gravity/air sampling, buoyancy direction and terrain mesh availability. It also passed the existing visual/photo regressions. Core checks: 110,323. The later optional-adapter organization and Heisenberg source-path extension compile successfully; actual Heisenberg/KFS installations and craft remain untested. The resource fixture validates the shared abundance path, not a full drill deployment/production cycle.
