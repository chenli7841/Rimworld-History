# HE-001-02 Historical Landmark Maps

This module adds two enterable world-map landmarks for the Eastern Zhou scenario. Coordinates stay in `EasternZhou_770BCE.xml`; the landmark Defs cross-reference those location IDs, and the generated world resolves each coordinate to the current world's tile grid. No tile ID is stored in XML.

## Landmarks and templates

- `HR_Landmark_Lishan` → `HR_Map_LishanBattlefield`: deterministic foothill route, irregular southern rock ridge with multiple passes, normalized battlefield/approach/retreat/observer/civilian zones, and map entry points. The death-scene zone is explicitly a gameplay reconstruction.
- `HR_Landmark_Haojing` → `HR_Map_HaojingCapital`: representative Western Zhou settlement districts and broad gravel streets. Districts are gameplay abstractions; no unsupported palace plan or later-period buildings are spawned.

Both locations use `HistoricalMapLocationWorldObject`, now a `MapParent`. Only the two dedicated landmark WorldObjectDefs set `canHaveMap=true`; ordinary editor markers remain non-enterable. If both locations map to one tile, their WorldObjects can coexist and each map-parent identity selects its own template when generating a local map.

## Generation and persistence

`GenStep_HistoricalLandmarkMap` runs on local map generation and returns immediately unless the tile contains a landmark for the selected historical scenario. It runs after the existing ancient-forest step. It changes only natural dry terrain for streets and adds natural rock to eligible non-road ridge cells; it skips active ancient-forest soil and never clears the map. If a map is too small or a rock Def is unavailable, it falls back to metadata/zone registration and keeps the map playable.

`HistoricalMapSceneComponent` stores scenario, template and version, all colocated landmark IDs, deterministic generation seed, normalized zones resolved to cells, approach cells and active scene-layer IDs. Existing maps are never regenerated on load; their native terrain and spawned Things remain under RimWorld's normal save system.

The runtime query surface is `HistoricalEventLocationService`: `TryGetLandmarkTile`, `IsEventLocation`, `GetMapTemplateId`, and `TryGetScene`. Scene components expose `TryGetZone` and `TryGetApproachCell`. HE-001 event dates and stages remain owned by `HistoricalEventManager`; loading or entering a map does not advance the event. Scene layers compare stage indices in the event Def's declared order, not stage-name strings.

## Scope limits

This phase creates scene space and query data only. It does not spawn King You, armies, civilians, buildings, battle encounters, quests or rewards. Haojing uses terrain and district markers, not a complete city. Current in-game verification still requires launching RimWorld 1.6 and entering both world-map landmarks.
