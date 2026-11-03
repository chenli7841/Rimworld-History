# HR-016 RimWorld 1.6 World Feature API Investigation

Inspected the local `Assembly-CSharp.dll` from the configured RimWorld 1.6 Managed directory before implementing the adapter.

## Confirmed API

- Runtime types are `RimWorld.Planet.WorldFeature` and `RimWorld.Planet.WorldFeatures`; feature definitions are `RimWorld.FeatureDef`.
- A world exposes the native collection through the public `RimWorld.Planet.World.features` field; the collection is `WorldFeatures.features`.
- `WorldFeature` exposes writable public fields `uniqueID`, `def`, `layer`, `name`, `drawCenter`, `drawAngle`, `maxDrawSizeInTiles`, and `alpha`.
- `WorldFeature.def.defName` identifies its native type. Core examples include `MountainRange`, `Lake`, `Island`, `Bay`, `Peninsula`, `Sea`, and biome feature DefNames. Core has no river `FeatureDef`; rivers use another native data model, so HR-016 does not rename river paths as WorldFeatures.
- Each `RimWorld.Planet.Tile` has a public `feature` field. HR-016 gathers feature coverage from the generated world's own surface tiles, avoiding `Find.WorldGrid` during world generation.
- `RimWorld.SurfaceLayer.tileFeature` is a public raw byte array. `WorldFeatures.ExposeData()` loads per-tile `ushort` feature IDs from it and uses `65535` as the no-feature sentinel. Clearing therefore nulls each loaded layer Tile's `feature`, fills this raw surface mapping with `0xFF`, and empties `World.features.features`.
- `WorldFeature.Tiles` also exists, but the adapter avoids relying on its global-world lookup while the generated `World` may not yet be `Find.World`.
- `WorldFeatures.UpdateFeatures()` is public. Local IL inspection confirms it marks text as not created and calls `CreateTextsAndSetPosition()`, which reads each `WorldFeature.name`; this refreshes vanilla labels without rebuilding the map. `WorldFeature.ExposeData()` calls `Scribe_Values.Look(ref name, "name", ...)`, and `WorldFeatures.ExposeData()` Scribes its feature list, so the native feature name is saved by RimWorld.
- `WorldGenStep_Features.GenerateFresh(string, PlanetLayer)` creates features. Historical scenario integration runs after `WorldGenerator.GenerateWorld` returns, so all generated Features are available. If the new world is not yet active as `Find.World`, label refresh is deferred until its world interface is drawn.

## Compatibility notes

Renaming mutates only the existing feature's `name`; it does not change `Tile.feature`, terrain, rivers, roads, or political data. Native feature IDs are treated as current-world hints only. XML matching is based on projected historical coordinates and native feature type, and collisions are rejected rather than silently overwriting a name. The automatic point matcher is conservative and logs candidates requiring review. Polygon overlap scoring is not yet enabled for point-only definitions.

