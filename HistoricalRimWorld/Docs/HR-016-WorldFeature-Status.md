# HR-016 Native World Feature Names — Status

## Implemented

- Uses RimWorld's existing `WorldFeature` instances and native label renderer. It does not create replacement map labels or change feature coverage.
- `WorldFeatureAdapter` reads `World.features.features`, native `FeatureDef.defName`, `Tile.feature`, and covered tiles. It changes only `WorldFeature.name` and calls native `WorldFeatures.UpdateFeatures()` to refresh labels.
- At the end of historical world generation, point targets are projected through the active HR-013 scale and matched by native feature type and historical great-circle distance. Candidates under 0.60 score are rejected; scores from 0.60 to 0.85 are logged for review; 0.85 and above can be renamed. If multiple targets claim one native feature, the automatic rename is skipped. One manual claim takes priority over an automatic target.
- The World Map Editor shows the native feature for the selected tile, its original/current name, `FeatureDef`, representative historical coordinate, and covered-tile count. Users can rename and restore its original name; these operations enter the existing Undo/Redo stack.
- Native `WorldFeature.name` is serialized by RimWorld's own `WorldFeature.ExposeData()`. A separate manual-override XML can preserve editor choices across new worlds.
- Six Eastern Zhou mountain names are configured against native `MountainRange` features. Rivers are not treated as WorldFeatures: RimWorld Core uses `RiverDef`/tile river links instead.

## Current limits

- The first automatic matcher uses point distance and exact native type only. Polygon overlap scoring, terrain suitability scoring, and weighted multi-factor scoring are not implemented; point-only Defs set `minimumOverlapRatio` to zero. Nonzero overlap requirements are safely skipped with a diagnostic.
- Initial coordinates are approximate candidates. Automatic matches may be absent or require review for a given generated map and scale. The matcher will leave uncertain Features unchanged; use the editor to select a tile covered by the intended native feature and rename it manually.
- The label refresh call is queued when generation happens before the new World is globally active, then applied on the first world-interface draw. UI/runtime behavior has not yet been tested in RimWorld.

