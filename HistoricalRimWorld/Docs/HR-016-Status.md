# HR-016 implementation status

## Implemented

- `HistoricalGeographicRegionDef` stores scenario-scoped geographic features as historical longitude/latitude geometry. Supported types are Point, LineString, Polygon, and MultiPolygon; region types include the HR-016 list.
- `HistoricalGeographicRegionManager` resolves scenario and period names, and caches the two-way mapping between features and the active WorldGrid. Geometry remains independent of political regions and native tile terrain.
- Labels render on the world map with zoom thresholds, priority ordering, overlap suppression, click-through details, explicit or automatic anchors, and line/polygon major-axis rotation.
- Tile inspection and the Historical World Map panel list geographic features covering the selected tile. A feature name opens its detail window.
- The World Map Editor can create, edit, rename, and delete features; edit coordinate vertices and label anchor/rotation; choose geometry and feature type; append the selected tile's projected historical coordinate; and undo/redo region changes.
- Editor overrides are saved as a separate schema-versioned XML file under `Config\HistoricalRimWorld\GeographicRegions\<scenarioId>.xml`. Save data is also included in the RimWorld world save.
- The Eastern Zhou scenario includes 19 starter features: 8 mountain ranges, 7 rivers, and 4 basin/plain regions.

## Coordinate and data caveats

Starter feature footprints are coarse editor examples and are explicitly tagged `Approximate`; their source notes instruct replacement with verified geometry. They should not be treated as authoritative ancient boundaries or ancient-period names. Historical names can have BCE date ranges; scenario display names take precedence, then period names, then default Chinese/English names.

The editor currently edits vertices in a longitude/latitude text area and has add-selected-tile/remove-last-vertex actions. Direct mouse drawing, moving an existing vertex by dragging, moving/rotating labels on the map canvas, boundary overlay handles, a robust spatial index, and broad automated tests remain future work. The current cache avoids remapping each frame and rebuilds after edits or when the map/scale changes.

## File precedence

Base XML Defs seed a new scenario's region collection. The scenario's user override XML replaces entries with matching `defName` and can add new entries. The active world save then persists the resulting editable collection for continued play.

