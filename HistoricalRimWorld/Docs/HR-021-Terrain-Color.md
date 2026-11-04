# HR-021: Historical World Terrain Color

## Current implementation

- Tile color overrides are stored in `HistoricalMapEditWorldComponent` and exported as coordinate-anchored `<TileColorOverride>` entries in the scenario's existing `MapEdits` XML.
- Feature color overrides are stored in `HistoricalWorldTerrainColorWorldComponent` and included as `terrainColor` / `terrainColorPriority` attributes in the existing World Feature layout XML. During layout import, the values are attached to the newly created native `WorldFeature` after its historical tile membership is restored.
- `HistoricalWorldTerrainColorManager` resolves colors in the order Feature, Tile, then vanilla. Feature candidates are compared by descending priority, then ascending native feature unique ID for deterministic tie resolution.
- The existing `WorldRenderer.DrawWorldLayers` postfix draws the color mesh after all native map layers (including terrain and clouds). A cached mesh draws the exact WorldGrid tile polygons with vertex colors and partial alpha. The overlay includes only tiles with a configured effective color; the rest remain rendered by RimWorld's original biome materials.
- The tint hook runs in the native terrain mesh builder. It does not write biome, hilliness, elevation, temperature, rainfall, or other tile data.
- Hex edits are applied only after pressing Apply, then the native world layers are marked dirty. Effective-color lookup is cached until an edit or world-feature membership change invalidates it.

## RimWorld 1.6 API observations

The local `Assembly-CSharp.dll` shows the base Surface draw-layer order includes terrain followed by clouds. Runtime logs confirmed both the material tint path and the first tile-overlay path ran, but the requested color remained invisible, consistent with a later world layer covering the terrain-stage overlay. The color renderer now runs alongside the existing fog renderer in the `WorldRenderer.DrawWorldLayers` postfix, after all native layers, using the same `WorldMaterials.VertexColor` and tile-polygon mesh pattern. Partial alpha keeps earlier roads, rivers, and map details visible through the tint.

## Data and overlap limitation

The color records use the native feature unique ID during a saved world's lifetime. For regenerated worlds, the existing feature-layout XML associates the color with the same layout record that carries the feature type, name, and historical tile coordinates; it is not applied by display name alone.

RimWorld 1.6's `Tile` stores a single native `WorldFeature` reference. The manager still applies deterministic priority comparison to all feature color records associated with a tile, but the current native membership model cannot represent two simultaneous feature memberships for one tile. Supporting overlapping feature memberships would require a separate compatibility layer and is not silently simulated here.

## Visual interpretation

The configured RGB value is drawn as a partial-alpha per-tile tint. The native terrain remains visible beneath it, so rendered pixels blend with the original terrain. Unconfigured tiles use their original RimWorld biome material.
