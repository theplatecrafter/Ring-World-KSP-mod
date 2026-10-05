# Multiple habitats

These placement features are in the development build after v1.1.6. Published v1.1.6 supports star-relative offsets, not planet/asteroid attachment or physical eclipses.

## Create or edit a ring

In Sandbox, open **Ringworld > Rings**. Select a habitat, or enter settings and choose **Spawn a new ring using these fields**. The editor groups controls into collapsible placement, terrain, and rotation sections. Save the game after editing.

Enable **Follow an existing body or asteroid/comet** and select an existing star, planet, moon, or orbiting asteroid/comet. The ring follows that object's motion. Asteroids use their persistent vessel ID, so switching ships or crossing a stock sphere of influence does not change the anchor.

Turn following off for a location offset from the stock Sun. No option creates or clones celestial bodies. A ring remains a custom habitat, not a new stock celestial SOI.

Center X/Y/Z values are offsets in kilometers in KSP's non-rotating reference axes. They travel with the anchor but do not turn with its surface. **Inclination X/Y/Z** rotates the ring about its center, in X, then Y, then Z order; it does not rotate the offset.

Set diameter, width, rim wall height, terrain height multiplier, seed and artificial gravity independently for each ring. Wall height is entered in kilometers (minimum 60 km, below one tenth of the radius; development builds for 1.1.7+ remove the earlier 1,000 km cap). Terrain height multiplier ranges from 0.25 to 3; 1 preserves normal terrain heights. Both fields apply when creating a ring or editing an unoccupied ring. Blank seeds randomize on creation. **Reverse rotation direction** changes spin direction; speed is calculated from radius and artificial gravity. Real celestial gravity still acts on vessels, so effective gravity near a planet can differ from the selected centrifugal acceleration.

Diameter can be as small as 2,000 km and width as small as 10 km, subject to the other geometry checks. For a small ring, reduce wall height in the same editor before applying; the atmosphere must still fit beneath the wall. **Visit selected ring** performs a Sandbox transfer to the arrival landmark.

## Daylight and eclipses

**Day/night shadow panels** enables the twenty orbiting panels. Turning them off removes their shadows, but does not prevent a planet or another ring from eclipsing the star.

Lighting uses the star above the anchor in the orbital hierarchy. Celestial spheres, ring floors, rim walls and enabled panels can block its light. Ring holes remain open to light. Eclipses depend on the observer's position, inclination, current orbit and other rings' positions.

Local and distant terrain/cloud lighting, the stock Sun flare and ring solar-panel illumination use this geometry. The matching development **Ringworld Scattering** build applies it to enhanced water and distant atmosphere. CPU and GPU penumbrae use different approximations, so soft edges are not pixel-identical.

This does not add ring-shadow shaders to stock planetary terrain. Illumination uses one associated star; simultaneous illumination by multiple stars is not simulated.

## Moving, saving and recovery

Move/delete is blocked while a ring has resident vessels. Recover or move those vessels first. At least one habitat must remain. Overlap checks are conservative, especially for tilted rings, but permit a small planet ring inside another ring's empty central region. They check the current layout; they do not guarantee that independently moving rings will never intersect later.

Grounded vessels, surface time warp, save/reload and trajectory encounters use the moving anchor. Only the current nearby habitat generates local terrain and physics; distant habitats use coarse rendering. Multiple independent rotating physics frames cannot run simultaneously in one Unity scene. Unloaded atmospheric flight and collision simulation are not added.

If an asteroid is removed, its ring holds its last recorded center relative to its saved orbital reference and displays an editor warning. Select another anchor when the habitat can be edited. Back up before removing an anchor or planet pack; a removed orbital reference body itself cannot be reconstructed.

Science and landed residents retain each ring's persistent ID. Additional habitats have distinct science subject IDs; expedition milestones remain save-wide. Quality settings are inherited when spawning and can be changed after visiting. Extra rings cost rendering time and memory. Trajectory prediction includes celestial gravity and the selected ring's ribbon gravity, not the combined gravity of every ring.
