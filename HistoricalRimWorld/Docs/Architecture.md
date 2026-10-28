# Architecture

Historical RimWorld is an independent RimWorld 1.6 mod. Its foundation provides calendar time, data-driven scenarios, and a separate political-state model without changing vanilla time or Factions.

## Layers

- **Historical Core** owns shared runtime concepts and services such as calendar calculations, political state, validation, persistence, and queries. It should not depend on a particular scenario's facts.
- **Historical scenario data** describes dates, polities, regions, cultures, settlements, and scenario-specific starting conditions. It is data-driven and consumed by the core rather than embedded in general-purpose systems.
- Historical polities are separate from RimWorld `Faction` and never change `Faction.OfPlayer`. Optional faction mappings are references scoped to the current save.
- Historical political runtime state is initialized from a scenario's `HistoricalPoliticalSetupDef` only when that scenario is explicitly selected. Initialization is idempotent and does not overwrite saved state. Centralized APIs validate world/local mutation sources and record accepted changes with the current game tick and historical date.
- Region ownership, effective control, military occupation, domains, local communities, political relations, succession, and change history are separate concepts. The example domain is explicitly identified as a fictional/speculative template.

## Runtime state and persistence

- `HistoricalCalendarGameComponent`, `HistoricalScenarioGameComponent`, and `HistoricalPoliticsGameComponent` store save-specific runtime state. Future runtime state will use `GameComponent` and/or `WorldComponent` according to whether it belongs to a save game or a world.
- Historical definitions and authored static content use XML Defs. The Eastern Zhou scenario includes an initial political setup Def.
- State in a RimWorld save uses the game's Scribe persistence system.
- HR-003 static scenario, state, region, culture, and settlement definitions are separate Def types; save state stores only the active scenario DefName and initialization metadata.
- Cross-scenario inheritance will use a separate, versioned archive format with explicit migration handling; it will not be conflated with the current save's Scribe state.

## DLC and integration boundaries

- Biotech is required and will provide vanilla reproduction and child growth systems. This mod will build historical age and genealogy features around those systems rather than replacing them.
- Royalty, Ideology, Anomaly, and Odyssey are optional and must not be required for the mod to load.
- Harmony is not part of the bootstrap. If future work requires Harmony patches, they must be registered and maintained in one central patch-management area with clear ownership and compatibility checks.

## Bootstrap boundary

The Mod entry point loads settings and writes one initialization log message. The historical calendar and scenario remain inactive in every save until explicitly initialized. The calendar tab supports a direct calendar opt-in; developer mode also offers explicit scenario initialization from loaded Defs. Scenario initialization validates both the scenario and its political setup before binding the clock, scenario ID, and initial political state. It does not create a world, pawns, factions, or historical events. The politics view is read-only outside developer mode; its developer controls still use the centralized validation and recording API. New systems should be added in focused modules as roadmap stages require them.
