# Optional compatibility adapters

Keep third-party-specific code here, separate from the public surface/resource/terrain APIs and stock integration. Adapters consume those shared APIs rather than duplicate ring physics. Detection and Harmony targets belong to each adapter; no target plugin is a hard dependency.

`GameData/NivenRingworld/Compatibility.cfg` controls automatic installation. ModuleManager may override the booleans before flight startup. Configuration changes require a KSP restart. New adapters should add an explicit switch, use narrow call-site patches, preserve original behavior outside the ring, and document supported plugin versions and tests. Do not globally mutate a CelestialBody or patch stock gravity for every caller.

These files currently build into the base DLL for deployment simplicity. The public APIs also allow independently distributed bridge plugins; a separate optional CKAN package can be created when an adapter has its own validated release lifecycle. This directory is not a claim that every adapter is fully craft-tested.
