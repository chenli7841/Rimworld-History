# HR-015 World Map Editor status

## Available now

- On the world map interface, use the `World Map Editor` button for a generated Historical RimWorld scenario. The entry is visible even outside Dev Mode; editing itself requires Dev Mode and the window explains that requirement.
- During new-game setup, the same editor can be opened on the starting-site selection page after the historical world has been generated. It reads the selected tile from `WorldInterface`, so terrain can be edited before choosing the landing tile.
- The editor reuses the selected native WorldGrid tile; click elsewhere on the existing world map to select another tile while the editor stays open.
- Inspect tile ID, game-sphere coordinate, HR-013 inverse-projected historical coordinate, native terrain/biome/elevation, and imported DEM statistics when HR-014 data exists.
- RimWorld's selected-tile inspect pane now shows both its native game-sphere coordinates and the inverse-projected historical coordinates at the active scenario scale.
- Edit the selected tile's description in the editor. Descriptions are stored in the world save and included in the scenario map-edit XML with geographic anchors so they can be remapped if world tile density changes.
- Paint native RimWorld `Hilliness` with a hex-neighbor brush at radius 0–4. Radius membership uses `WorldGrid.GetTileNeighbors`, not screen pixels.
- Terrain paint, restore, undo, and redo immediately update tile data and dirty the world renderer layers so the map redraws the edited terrain on the next UI frame.
- Draw a river by entering historical `longitude,latitude` waypoints, selecting a vanilla `RiverDef`, previewing the snapped tile route, and applying it. Each waypoint is inverse/forward projected with the scenario's map scale; intervening tiles are connected through WorldGrid neighbors. River paths support undo/redo, Scribe persistence, and XML export/remapping by the original historical coordinates.
- Undo and redo each paint/restore action as one command. Restore uses the generated/base terrain captured when a tile first receives a manual edit.
- The sparse manual terrain layer is Scribe-persisted in the world save.
- `Save scenario map edits` atomically writes a scenario-specific XML file under `GenFilePaths.ConfigFolderPath\HistoricalRimWorld\MapEdits\`. A new world of the same scenario reloads it by historical coordinates and reprojects to the current grid. Tile IDs are retained as export diagnostics, not used as stable keys. Invalid entries and coordinate collisions are reported and skipped.

Example output entry:

```xml
<TerrainOverride tileIdAtExport="119452"
                 longitude="112.3"
                 latitude="34.1"
                 baseTerrainAtExport="Flat"
                 terrain="LargeHills" />
<RiverPath id="generated-id" riverDefName="River">
  <Point longitude="113.2" latitude="34.7" />
  <Point longitude="113.8" latitude="34.4" />
</RiverPath>
```

## Not implemented yet

This is the HR-015 minimum editor foundation, not the full eight-phase editor. Biome and elevation painting, drag interpolation, rectangle/polygon/fill selection, image reference layers and georeferencing, river erasing/branch-flow editing, road editing, settlement editing/search, region drawing, colored map overlays, asynchronous large-area jobs, crash recovery, and automated test projects remain open. No original DEM or Mod XML is modified.

The save format is currently schema version 3 and stores manual `Hilliness` overrides, tile descriptions, and river routes. It continues to load schema versions 1 and 2. Other property types should extend the versioned format without changing the base DEM cache. Changing map density remaps edits by saved historical coordinates; entries that fall outside the new map's configured tile-center error are preserved in the file and logged as unapplied.

## Validation

The C# project compiles against the installed RimWorld 1.6 assemblies. UI interaction, Scribe round-trips, new-world remapping and map rendering still require in-game verification. The repository currently has no automated test project or test framework.
