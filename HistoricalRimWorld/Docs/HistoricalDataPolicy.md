# Historical Data Policy

## Stable identity

- Every project Def uses an `HR_`-prefixed `defName`. Treat published names as permanent IDs because saves and future archive formats may refer to them.
- Static historical facts belong in XML Defs. Save-specific choices and progress belong in `GameComponent` or `WorldComponent` state.
- Historical state, region, culture, and settlement Defs describe reference data. They do not create Pawns, factions, settlements, map tiles, borders, ownership, or events.

## Evidence and uncertainty

- Use `sourceNotes` and `evidenceLevel` to preserve provenance and uncertainty.
- Leave unknown dates and coordinates unset. Do not fill fields with invented precision.
- A region is a logical historical geography, not a permanent political territory or a RimWorld tile. Adjacency is a data relationship, not pathfinding.
- Culture definitions are historical and regional groupings. Do not equate them with modern ethnic identities or assume a region has one uniform culture.
- A settlement Def is a reference entry, not a claim that the settlement existed on every date in a scenario. Record known chronological limits when supported; otherwise explain uncertainty in `sourceNotes`.

## Validation

Def `ConfigErrors()` and `HistoricalDefValidator` check dates, scenario membership, repeated references, political cycles, settlement coordinates, and region adjacency. Asymmetric adjacency is logged as a warning. Optional historical prose is not required for a Def to load.

## Runtime and save compatibility

- `HistoricalScenarioGameComponent` stores the scenario `defName`, initialization flag, and initialization version. It never serializes an entire Def.
- On load, an unavailable saved Def is reported and its `defName` remains intact. The game does not substitute another scenario or reset the historical clock.
- Scenario initialization is explicit and one-way for a save in this phase. Selecting an already active scenario is idempotent; switching scenarios is rejected.
- HR-002 owns the date and Tick mapping. Scenario data supplies its start date through the public calendar initialization API.
