# Flight, landing and saving

The ring's interior is the ground. Apparent gravity points outward toward that floor because the habitat rotates. The star remains KSP's native reference body; the mod supplies a local rotating flight frame near the ring.

## Instruments and approach

Inside the ring frame, surface speed, altitude, vertical speed and stock directional SAS targets use ring-relative measurements. Target and manoeuvre modes retain their stock behaviour. Stock orbit statistics can still describe the central star rather than the ring.

The ring-aware map trajectory predicts a vacuum coast and marks entry/escape. It stops at atmosphere entry: it does not predict aerodynamic descent, future thrust or manoeuvre-node execution. Tracking Station guards prevent warp from skipping an imminent ring encounter. The ring is not a new spherical SOI.

Use stock engines, landing gear and parachutes normally. Parachutes still need suitable pressure, altitude and speed; their deployment rules are not bypassed. Avoid an unmatched atmospheric approach: the air rotates with the habitat.

In the Sandbox expedition tab, **Gentle placement near ground / water** overrides the arrival-height slider. It places the craft just above the surface with clearance for its colliders, then lets ordinary gravity settle it. Choose a clear, level location: this option does not clear buildings, repair a damaged craft or guarantee stability on a steep slope. Leave it off for the normal descent from your selected altitude. The training approach always uses its advertised high-altitude descent.

## Land and keep your progress

Let the craft settle on dry ground, with the engines off and little translation or rotation. Use **Save** and the normal **Space Center** controls when available. **Revert Flight** intentionally discards progress.

If saving or surface warp is blocked, check the reason shown in the Ringworld panel. A craft that is sliding, bouncing, rotating, airborne or floating in water is not a stable dry-ground resident. A small amount of landing-leg flex is tolerated, but sustained motion is not.

Back up saves before changing mods or world generation. Rings with resident craft are protected against relocation/deletion in the Sandbox editor. Existing craft cannot be repaired simply by updating the mod after a destructive event.

## Time warp

Use KSP's ordinary warp controls at the top left, or the comma/period shortcuts. High warp on the ring requires stable dry-ground contact. Airborne and physics warp in the local ring frame are unavailable. Weather and shadow-square phase advance with game time.

## EVA, structures and water

EVA uses the ring's local orientation and contact handling. Structures have simplified exterior collision meshes; a visible doorway does not guarantee an accessible interior. Detailed terrain and collision coverage remain local to the loaded vessel.

Water supports approximate buoyancy and damping. Visual waves do not move the mean buoyancy level. Water is not equivalent to a fully integrated stock planetary ocean, and underwater/EVA swimming and all third-party buoyancy systems are not supported as a complete feature set.

See [limitations and troubleshooting](KNOWN-LIMITATIONS.md) for compatibility boundaries and [research](SCIENCE-AND-EXPEDITIONS.md) for deployed equipment and experiments.
