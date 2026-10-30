# HR-013 Implementation Status

## Implemented in this pass

- Chose the local projection route (A). RimWorld's `WorldGrid` and `PlanetLayer` remain the native objects; the mod does not replace the grid or change its tile-indexing algorithm.
- Added `HistoricalMapScaleDef`, distinct historical/projected/game coordinate types, and an azimuthal equidistant projection with inverse conversion. The projection uses a spherical Earth radius of 6,371.0088 km, scales local projected X/Y in kilometers, then unprojects to the game sphere.
- Added an Eastern Zhou map scale of 3.0 centered at 34.5°N, 111.5°E. The scale is applied to sampled world-generation bounds, XML terrain overrides, and city map markers. HR-012's world coverage UI and `PlanetLayer.InitializeLayer` coverage patch therefore use the expanded game-sphere cap.
- Historical map markers display both the unchanged historical coordinates and the transformed game-sphere coordinates, tile ID, and scale. World generation logs real great-circle distance, projected game-sphere separation, and `WorldGrid.ApproxDistanceInTiles` for Luoyi-to-configured-city pairs. That tile value is a geometric grid estimate, not a pathfinder route.
- The chosen scale parameters, projection version, and world-generation mapping version are serialized with the historical faction world component. The existing fixed scenario seed remains in effect; ordinary RimWorld generation is outside the historical scenario Harmony context.
- XML terrain override coordinates remain historical coordinates. The uniform plains template transforms each coordinate before resolving the nearest surface tile. This is still prototype terrain, not real China geography.
- Added a reusable region-boundary projection utility that samples long great-circle edges before transforming them, a terrain-source interface, and a separate travel-scale policy model.

## RimWorld 1.6 integration inspected

The integration uses the local game assembly's `PlanetLayer.InitializeLayer`, `WorldGrid.LongLatOf`, `PlanetLayer.LongLatOf`, and `WorldGrid.ApproxDistanceInTiles` APIs. Existing Harmony patches set `PlanetLayer` view center and angle before surface mesh initialization; the scaled coverage is passed through that existing path. No original game files are modified.

## Current Eastern Zhou scale

`HR_MapScale_EasternZhou_770BCE` sets scale 3.0, center 34.5°N/111.5°E, projection `AzimuthalEquidistant`, and travel speed multiplier 1.0. For the current world-generation bounds (20–42°N, 100–132°E), the coverage calculator transforms the bounds and selects a spherical cap around the transformed bounds midpoint, plus the existing 3° tile safety margin. The resulting coverage is logged at world-generation time.

The scale enlarges the angular separation between historical locations on the game sphere, and the expanded coverage gives the native grid more area in which to place tiles. It does **not** subdivide or replace RimWorld's native grid. Actual generated tile counts and shortest traversable paths depend on the game build and world generation, and have not been measured in a running game in this pass.

## Not yet complete / runtime risks

- No verified historical region polygons are installed in the current project. The new projection utility is not yet used to assign rendered/revealed tiles from real region boundaries; existing region tiles still come from the prototype placement system and developer mappings.
- The project has no licensed, validated China terrain dataset. XML tile overrides are transformed, but the current uniform template still uses prototype biome/elevation and removes generated roads, rivers, and landmarks. It does not reproduce the historical coastline or Yellow River.
- Caravan pathfinder routes and travel times have not been runtime-tested. The configured default multiplier is 1.0, matching vanilla travel speed; changing that policy value is not currently applied to caravan speed.
- The game-map marker diagnostics report geometric tile distance, not `WorldPathFinder` path length. The requested actual path-distance acceptance measurements still require generating and inspecting a RimWorld world.
- Scale state is recorded for new historical worlds. Existing saves keep their already generated tile locations; this code does not reproject them. Old save migration and retaining multiple projection implementations across future projection-version changes need an explicit policy before changing the projection algorithm.
- No in-game generation, save/load, performance, fog, trade, or caravan QA was possible here. The local net472 project build and XML parsing are the only checks completed.

## Verification

The project builds against the local RimWorld 1.6 and Harmony assemblies with 0 warnings and 0 errors. The new scale Def, world-generation Def, and Eastern Zhou map XML parse successfully. Runtime acceptance remains open until a world is generated and its tile counts, city tile IDs, path distances, and save/load behavior are measured in RimWorld.
