# Architecture

Historical RimWorld is an independent RimWorld 1.6 mod. Its bootstrap project deliberately contains no historical gameplay systems.

## Layers

- **Historical Core** will own shared runtime concepts and services such as calendar calculations, validation, and interfaces. It should not depend on a particular scenario's data.
- **Historical scenario data** will describe dates, polities, events, and scenario-specific starting conditions. It will be data-driven and consumed by the core rather than embedded in general-purpose systems.

## Runtime state and persistence

- `GameComponent` and/or `WorldComponent` are planned for runtime state, selected according to whether a datum belongs to a save game or a world.
- Historical definitions and authored static content will use XML Defs.
- State in a RimWorld save will use the game's Scribe persistence system.
- Cross-scenario inheritance will use a separate, versioned archive format with explicit migration handling; it will not be conflated with the current save's Scribe state.

## DLC and integration boundaries

- Biotech is required and will provide vanilla reproduction and child growth systems. This mod will build historical age and genealogy features around those systems rather than replacing them.
- Royalty, Ideology, Anomaly, and Odyssey are optional and must not be required for the mod to load.
- Harmony is not part of the bootstrap. If future work requires Harmony patches, they must be registered and maintained in one central patch-management area with clear ownership and compatibility checks.

## Bootstrap boundary

The Mod entry point loads settings and writes one initialization log message. It does not create a world, pawns, or historical events. New systems should be added in focused modules as roadmap stages require them.
