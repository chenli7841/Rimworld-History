# Architecture

## Characters and genealogy (HR-007)

`HistoricalPersonRecord` owns persistent historical identity while RimWorld `Pawn`/Biotech continue to own biological age, reproduction, health, and ordinary death. `HistoricalGenealogyGameComponent` stores people, families, kinship, lifespan records, and Scribe Pawn references for the active save. The component only registers humanlike player-faction Pawns when a Historical RimWorld scenario is active. Lifespan policies are XML Defs; assigned deadlines and genealogy are Scribe data. See [CharactersAndGenealogy.md](CharactersAndGenealogy.md) for API choices and limits.

Historical RimWorld is an independent RimWorld 1.6 mod. Its foundation provides calendar time, data-driven scenarios, and a separate political-state model without changing vanilla time or Factions.

## Layers

- **Historical Core** owns shared runtime concepts and services such as calendar calculations, political state, validation, persistence, and queries. It should not depend on a particular scenario's facts.
- **Historical scenario data** describes dates, polities, regions, cultures, settlements, geography, reveal setups, and scenario-specific starting conditions. It is data-driven and consumed by the core rather than embedded in general-purpose systems.
- **Historical Geography** owns coordinates, boundary polygons, region-to-tile mappings, overlap queries, and deterministic primary-region selection. `HistoricalEarth` is unavailable until verified one-to-one Earth tile data exists. `PrototypeWorld` explicitly marks save-specific assignments on generated worlds as prototype data.
- **World Revelation** is separate from geography. It stores Locked, Unexplored, and Revealed access states, exploration history, and prototype tile assignments in a `WorldComponent`. It owns world-map display and access checks; region identity and politics remain present when a region is locked.
- Where regions overlap, access uses a stable primary-region order: explicit priority, then smaller polygon area, then DefName. Rendering and informational queries may retain the full overlap list.
- **Historical Events** are XML Defs registered to a scenario. A date resolver preserves historical precision while scheduling year-only events at the first day of that historical year. A save component tracks pending and terminal outcomes and executes due events in scheduled-date, ascending-priority, then DefName order.
- **Historical Chronicle** is a separate save component. Attested history, local experience, and inferred legacy are distinct entry types with stable string references; entries are not deleted when a polity is dissolved.
- Historical polities are separate from RimWorld `Faction` and never change `Faction.OfPlayer`. Optional faction mappings are references scoped to the current save.
- Historical political runtime state is initialized from a scenario's `HistoricalPoliticalSetupDef` only when that scenario is explicitly selected. Initialization is idempotent and does not overwrite saved state. Centralized APIs validate world/local mutation sources and record accepted changes with the current game tick and historical date.
- Region ownership, effective control, military occupation, domains, local communities, political relations, succession, and change history are separate concepts. The example domain is explicitly identified as a fictional/speculative template.

## Runtime state and persistence

- `HistoricalCalendarGameComponent`, `HistoricalScenarioGameComponent`, and `HistoricalPoliticsGameComponent` store save-specific runtime state. Historical world reveal and tile mapping state use `HistoricalWorldRevealWorldComponent`.
- Historical event outcomes and their status-change history use `HistoricalEventGameComponent`; visible and deferred chronicle entries use `HistoricalChronicleGameComponent`.
- Historical definitions and authored static content use XML Defs, including scenario data, geography, political setup, and initial reveal setup.
- State in a RimWorld save uses the game's Scribe persistence system.
- Save-specific community technology, events, genealogy, and local scenario state belong in `GameComponent`; world-wide revelation or future world-owned technology state belongs in a `WorldComponent` where its lifecycle matches the data.
- HR-003 static scenario, state, region, culture, and settlement definitions are separate Def types; save state stores only the active scenario DefName and initialization metadata.
- Cross-scenario inheritance will use a separate, versioned archive format with explicit migration handling; it will not be conflated with the current save's Scribe state.

## Historical technology (HR-008)

- **Historical Core** owns reusable technology categories, prerequisite validation, availability/adoption state, policy checks, and acquisition APIs. Scenario XML Defs provide the starting technology state and history-specific evidence without embedding scenario facts in the service layer.
- Static historical facts and technology trees use XML `HistoricalTechnologyDef`, `HistoricalTechnologySetupDef`, and ordinary RimWorld `ResearchProjectDef`/`RecipeDef`/`ThingDef` definitions. Saved community progress, access, acquisition source, and game tick use `HistoricalTechnologyGameComponent` with Scribe.
- Technology availability (`NotYetHistoricallyAvailable`, `ExistsElsewhere`, `LocallyAccessible`) is separate from adoption (`Unknown`, `Known`, `UnderStudy`, `Adopted`). Research checks historical availability, community access, and adopted prerequisites; gaining knowledge does not adopt a technology.
- Runtime ownership is keyed by Historical `CommunityId`, not `Faction.OfPlayer`. The first scenario setup seeds the player community once; re-entry to an already initialized save does not replace progress.
- HR-006 events can use `Action_MakeTechnologyAccessible`, `Action_GrantTechnologyKnowledge`, and `Action_AdoptTechnology`. HR-007 can grant community knowledge via the same public manager API; per-character craft-specialty records are not implemented here.
- Production reuses RimWorld worktables, Bills, skills, and recipe pipelines. The first data slice includes bronze stock and an adze, pottery, and hemp cloth. The input materials are abstract resources; this slice does not add geological deposits or a dedicated hemp crop.
- Biotech remains the required DLC and continues to own pawn biology, reproduction, and child growth. The historical filter does not patch those systems.
- Harmony patches are centralized through the existing mod bootstrap. Historical research projects, player buildability, recipe availability/Bill execution, and the vanilla trader-stock maker have verified gates. Every new Harmony patch must remain in the technology patch folder and document its target API.
- The current policy deliberately does not modify or delete vanilla Defs. It does not yet cover all quest-reward implementations, raid pawn equipment generators, map scatter/world generation, or direct third-party code paths that bypass the patched UI, Bills, or trader-stock maker. Therefore it is a partial historical content filter, not complete removal of modern content.
- Cross-scenario legacy is planned as a separate, explicitly versioned archive with migrations; current technology state is only part of the active save's Scribe state.
- The initial chronology deliberately uses broad uncertainty bounds. The Chinese bronze horizon is informed by the Smithsonian National Museum of Asian Art's summary that the earliest Chinese bronze artifacts come from Erlitou and that the fabrication capability was in place by about 1700 BCE ([Bronze Age Casting](https://asia-archive.si.edu/learn/ancient-chinese-bronzes/bronze-age-casting/)). Early iron timing is region-dependent; see the archaeological survey [Invention of cast iron smelting in early China](https://www.sciencedirect.com/science/article/pii/S2667136021000017). These are broad data bounds, not claimed invention dates.

## DLC and integration boundaries

- Biotech is required and will provide vanilla reproduction and child growth systems. This mod will build historical age and genealogy features around those systems rather than replacing them.
- Royalty, Ideology, Anomaly, and Odyssey are optional and must not be required for the mod to load.
- Harmony is used for world-map rendering and vanilla world access hooks that do not expose a suitable mod API. All patches are centralized under `Source/Patches/World`; future Harmony patches must remain in that patch-management area with clear ownership and compatibility checks. Harmony is a mod dependency, while other DLC beyond Biotech remains optional.

## Event execution boundary

- Due events are checked on throttled game-component ticks and processed when the historical absolute day advances. Events whose scheduled date is less than or equal to the current historical date are eligible, so skipped game days are caught up.
- Priority sorts ascending: smaller values run first. DefName breaks ties deterministically.
- Each event may commit at most one politics or region-access mutation in this release. All action definitions validate before execution; the mutation is followed by chronicle writes and notifications. HR-004 and HR-005 do not expose a shared cross-component transaction, so multiple mutations are rejected rather than described as atomic.
- Conditions that are valid but false stay Pending and retry once per historical day. Missing definitions and invalid actions become Failed with a persisted reason; terminal states do not retry.
- InitialBackground events become chronicle entries and Skipped runtime states; their actions are never replayed. Events dated before a scenario's start are also not replayed.
- `RegionKnowledgeRequired` entries remain hidden until HR-005 reports that region Revealed. The chronicle UI filters future-dated entries and displays historical precision without inventing a month or day.

## Culture, customs, rituals, and relics (HR-009)

- `HistoricalCultureDef` remains the single culture data model. Its related-culture, custom, and ritual references extend the HR-003 catalog; HR-009 does not create a parallel culture database. Historical scenario data and evidence/source notes live in XML Defs, while custom state and individual/community familiarity are save data.
- Cultural identity is independent of political allegiance and RimWorld faction membership. Person state records birth culture, primary culture, and familiarity with other cultures; family state preserves primary culture/traditions; community state records dominant culture, familiarity/presence, and active customs. Birth registration, family inheritance, and marriage exchange use explicit APIs rather than per-tick polling.
- `HistoricalCultureGameComponent`, `HistoricalRitualGameComponent`, and `HistoricalRelicGameComponent` persist current-save state with Scribe. A future scenario-to-scenario legacy feature must use its own versioned archive and migrations; it must not reuse the active save's Scribe records as a portable archive.
- Ritual definitions are XML data and do not require Ideology. The initial ancestor commemoration, funeral/memorial, and hospitality actions use persistent planned/in-progress/completed/cancelled/failed records. Completion checks the host, gathering location, present participants, category-specific visitor, required offering, and configured duration before consuming an offering and granting a small vanilla memory. They are menu-driven observances using gathered Pawns; this version does not assign jobs, form a Lord, animate a ceremony, or guarantee attendance. A funeral needs a deceased member of the host's recorded family.
- `HistoricalRelicDef` describes a recordable object; the Scribe relic record and event log are separate from the physical `Thing`. `CompHistoricalRelic` links the physical item to a permanent RelicId and records destruction without deleting its history. The initial interface records designation and manual owner/lost events. It does not yet intercept vanilla inventory, caravan, trade, theft, or inheritance transactions, so those transfers require an explicit UI action. Cross-scenario relic legacy belongs to the future versioned archive.
- The first custom/relic entries use broad, qualified descriptions and identify gameplay abstractions in their source notes; they do not claim that customs are exclusive to a region or uniformly practiced. Biotech continues to own vanilla reproduction, pregnancy, biological age, and child growth. No Ideology ritual API is required.
- Harmony remains centralized in the existing patch bootstrap. HR-009 adds no Harmony patch; any future patch needed to observe vanilla ownership transactions must be placed in the central patch area, validated against RimWorld 1.6, and limited to cases without a suitable public Mod API.

## Bootstrap boundary

The Mod entry point loads settings, applies the centralized world patches, and writes an initialization log message. The historical calendar and scenario remain inactive in every save until explicitly initialized. The calendar tab supports a direct calendar opt-in; developer mode also offers explicit scenario initialization from loaded Defs. Scenario initialization validates its scenario, political setup, reveal setup, and event definitions before binding the clock and state. It does not create a world, pawns, or factions; background history is recorded and scheduled events wait for their historical dates. The politics view is read-only outside developer mode; its developer controls still use the centralized validation and recording API. New systems should be added in focused modules as roadmap stages require them.
