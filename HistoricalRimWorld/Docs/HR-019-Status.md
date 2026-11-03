# HR-019 Mountain Peak and Mountain Pass Map Markers

## Implemented

- Added `HR_MapMarker_MountainPeak` and `HR_MapMarker_MountainPass`, both using the existing `HistoricalMapLocationWorldObject` class and the same non-settlement WorldObject behavior as Town/City markers.
- Added transparent 128 × 128 world-map textures at `Textures/World/HR/MountainPeak.png` and `Textures/World/HR/MountainPass.png`. The mountain icon uses a three-peak silhouette; the pass icon uses two mountain shoulders and a central gate.
- Integrated marker selection and create/edit/delete controls into the existing World Map Editor. The selector includes the existing Town/City markers and the new Mountain Peak/Mountain Pass markers. Marker create/delete and metadata edits use Undo/Redo.
- New markers use the selected WorldGrid tile center and active historical projection. Name, description, coordinates, Def and tile are persisted by RimWorld's native world-object save system.
- Markers remain neutral information objects. They do not create factions, settlements, maps, pawns, quests, movement barriers, or terrain changes.

## Validation and limits

- The C# project builds against the configured local RimWorld 1.6 assemblies; the marker XML parses as well-formed XML.
- In-game Def loading, texture rendering at different world-map zoom levels, and save/reload behavior still need runtime verification.
- User-created markers are part of the active world's save. This change does not export manually created markers into the scenario's separate new-world XML template.
