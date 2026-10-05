# Ringworld configuration packs

**Development feature for Niven Ringworld 1.1.7 or newer.** These packs do not work with 1.1.6 or earlier. Configurations initialize rings once per save, in any game mode. Existing saved ring layouts take precedence.

## Definition format

Place a .cfg file inside a distinct GameData folder. KSP loads it through GameDatabase; ModuleManager patches can modify it before Ringworld reads it. ModuleManager is not needed for a plain ring-only definition, but is required by the supplied distant-star packs.

```cfg
NIVEN_RINGWORLD_SYSTEM
{
    name = MyRingSystem
    replaceDefault = true
    RING
    {
        ringId = primary
        ringName = My Ringworld
        anchorId = body:Sun
        radius = 15300000000
        width = 160500000
        wallHeight = 160000
        gravity = 9.72
        panelsEnabled = true
        seed = 1970
    }
}
```

Use unique system names and ringIds. A pack can contain multiple RING nodes. Only one installed system may set replaceDefault=true. Without a replacement, the built-in ring is retained and ringId=primary is reserved for it. Invalid or conflicting packs report an error and fall back to the default ring; fix the installation and create a new save.

## World options

Values are invariant numbers with a decimal point. Boolean values accept true/false regardless of case. Distances below are **metres**, not the kilometres displayed by the editor.

| Option | Meaning |
| --- | --- |
| ringId / ringName | Persistent identifier and display name |
| anchorId | body:<internal body name>; follows that body's orbit. Save-specific asteroid IDs are not supported by instance packs. |
| referenceBody | Legacy fallback if anchorId is omitted; the loader resolves the orbital reference automatically. |
| designatedStar | Associate illumination with the anchor hierarchy's star |
| centerX / centerY / centerZ | Offset from the anchor in non-rotating reference axes, default zero |
| radius / width | Radius >= 1,000,000 m; width >= 10,000 m and <= radius |
| wallHeight | At least 60,000 m, below radius/10; atmosphere must fit under it |
| gravity | Artificial surface acceleration, > 0 and <= 100 m/s²; spin derives from sqrt(gravity/radius) |
| tiltX / tiltY / tiltZ | Inclination angles in degrees, matching the editor |
| spinDirection | +1 or -1 |
| panelsEnabled / daySeconds | Day/night panels and illumination cycle (seconds, at least 60) |
| seed | Signed 32-bit integer; blank or omitted chooses a random seed per save |
| heightMultiplier | Terrain height multiplier, 0.25 to 3 |
| forestDensity / pondAmount | Multipliers, 0 to 2 |
| atmosphere / atmosphereHeight / scaleHeight | Atmosphere enabled, thickness >= 100 m up to wall height, positive density scale height |
| structuralThickness | Hull thickness below deepest terrain, 1 to 10,000 m |
| tileSize / tileResolution / tileRadius | Advanced local terrain budget: 256–4096 m, 16–64 subdivisions, 2–5 tile radius |

The loader also accepts serialized Ringworld OPTIONS settings such as lodRange, cloudRange, weatherPeriod, stormChance and cloudAmount. Graphics start from Slow and are overridden only by explicitly supplied settings. Physical size does not select a more expensive preset. Unknown keys, duplicate keys, non-finite numbers and nested RING subnodes are rejected. Existing editor limits/clamping still apply to graphics and terrain-budget options. Use supported values rather than relying on clamping. Large offsets/dimensions remain subject to numeric precision limits; give interstellar rings their own star anchor and zero local offset.

## Distant stars

The base mod contains Compatibility/DistantStar.cfg. It activates only with Kopernicus and exactly one supplied interstellar pack folder. Both variants use the persistent internal name NivenRingworldHost. Do not change variants in a save that already uses that body.

The full-size star uses solar radius and gravitational parameter with G2V-inspired light; the standard variant scales radius to 1/10 and gravitational parameter to 1/100 for KSP gameplay. Flux is normalized to 1,360 W/m² at the ring in each variant. This is not a stellar-evolution simulation. The circular Sun-referenced orbit and explicitly bounded sphere of influence are KSP approximations, not a self-consistent binary-star model. The full-size Kerbol pack does not brighten or resize stock Kerbol.

Kopernicus registers the star; Ringworld retains its cylindrical terrain/physics. This does not turn the ring into a PQS planet or automatically enable planet-only visual configs. Interstellar in-game lifecycle and planet-pack compatibility still need validation before public release.

## Updates and removal

Restart KSP after editing installed configs. Existing saves retain their own ring snapshots; the Sandbox Rings tab can modify unoccupied rings. Removing a ring-only config does not delete rings already saved. Removing the distant-star pack, Kopernicus or its dependencies removes a celestial body on the next launch and may break saves; keep those installed until no save uses that star. Back up saves before changing the installed celestial system.

## Sources

- [Kopernicus](https://github.com/Kopernicus/Kopernicus): body registration and dependencies.
- [Kopernicus Light configuration](https://kopernicuswiki.github.io/main/ScaledVersion/Light.html): light colors, distance curves and flux coefficient conventions.
- [CKAN: adding a mod](https://github.com/KSP-CKAN/CKAN/wiki/Adding-a-mod-to-the-CKAN): config-pack distribution.
