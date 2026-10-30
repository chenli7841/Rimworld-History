# HR-013 Implementation Status

## Current status

HR-013 is **not complete**. The Eastern Zhou world is not yet backed by real Chinese geography, and the mod does not contain a generated terrain dataset. The current `HistoricalEarthGeoMapper` intentionally reports itself unavailable. No source GIS files were found in the project or the usual local download folders.

The Eastern Zhou scenario now uses a **scenario-bounded spherical WorldGrid** centered on the configured China-region bounds, then applies `Defs/HistoricalWorldMapDefs/EasternZhou_770BCE.xml`. The XML defines default biome, elevation, and hilliness, plus optional longitude/latitude overrides resolved to the nearest generated surface tile center. Out-of-range offsets or two overrides resolving to one tile fail generation instead of silently assigning misleading terrain. The template clears generated rivers and roads and removes generated landmarks. Its terrain values are editable prototypes, not real elevation, climate, coastline, or hydrography. The UI seed input is ignored for this scenario in favor of its configured fixed seed. The starting-site screen blocks continuation if bounds or terrain validation fails.

## Data-source review

- **Land mask candidate:** Natural Earth 1:10m physical land vectors, version 5.1.1. The publisher says its vector and raster data are public domain and may be modified and redistributed. Recommended credit: “Made with Natural Earth.” See <https://www.naturalearthdata.com/about/terms-of-use/> and <https://www.naturalearthdata.com/downloads/10m-physical-vectors/>.
- **Rivers/lakes candidate:** Natural Earth 1:10m rivers/lake centerlines and lakes. Its catalog lists the layers and versions, but the source is generalized cartography; river coverage, topology, and suitability for local-map river generation still need validation. See <https://www.naturalearthdata.com/downloads/10m-physical-vectors/>.
- **Elevation candidate:** NOAA ETOPO 2022 is an official global relief model containing topography, bathymetry, and shoreline data. The accessible NCEI FAQ specifically describes ETOPO1 usage and citation, not the full redistribution lineage for every ETOPO 2022 input. Treat ETOPO 2022 licensing and attribution as **unverified for this mod** until its product metadata and user guide are reviewed. See <https://www.ncei.noaa.gov/products/etopo-global-relief-model>.
- Do not package a screenshot, rendered map, or data with unclear redistribution rights. Ancient river courses and circa-770-BCE climate remain separate historical uncertainties even after modern geography data is available.

## Blocker

The build environment cannot resolve `naturalearth.s3.amazonaws.com` (`No such host is known`), and there are no local GIS datasets to preprocess. Therefore this work cannot produce or verify the required China coastline, elevation, river, lake, climate, or biome data. The repository also does not yet include the HR-013 GIS preprocessing tool or a runtime dataset loader.

## Chosen integration direction

Retain RimWorld's native spherical `WorldGrid` and tile adjacency. The current flat template changes terrain fields only; it does not map geographic coordinates or constrain the globe to an East Asian region. A future real map should use each tile center's geographic coordinates to sample a packaged regional dataset, keep the surrounding world outside the historical playable mask, and preserve vanilla neighbor topology and pathfinding.

## Required next inputs

1. Provide or make available the licensed regional source data: high-resolution land polygons, elevation/relief with redistribution terms, and river/lake vectors. The intended crop is 73–135°E, 15–55°N.
2. Confirm the source versions, attribution texts, and allowed redistribution for every input layer.
3. Build and validate the preprocessed dataset before enabling `HistoricalEarth` generation.
4. Implement and test world-tile mapping, land/water and terrain assignment, rivers/lakes, climate/biomes, settlement placement, and save/version behavior against the local RimWorld 1.6 assemblies and an actual generated game world.

## Verification

The existing `net472` mod project compiles against the local RimWorld 1.6 and Harmony assemblies. This only verifies the current code project; it does not verify HR-013 geography or runtime world generation.
