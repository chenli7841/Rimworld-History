# HR-014 terrain data: Henan prototype

## Current boundary

The mod now samples an offline elevation cache onto RimWorld's existing `WorldGrid` surface tiles. It uses the HR-013 inverse projection before querying the cache. The cache importer accepts GeoTIFF and Cloud Optimized GeoTIFF inputs through rasterio/GDAL and makes a compact, averaged geographic grid for the game.

No Copernicus or other DEM source raster is included in this repository. Consequently, the checked-in map does not yet contain verified Henan elevation results. Until the cache is installed, world generation logs a warning and retains the existing editable XML terrain. The runtime does not connect to a GIS service.

## Prepare the DEM cache (Windows PowerShell)

1. Obtain the Copernicus DEM GLO-30 Public GeoTIFF/COG tiles that overlap 109–117°E, 31–37°N. Keep the download's licence/readme with your source data and note the exact tile release.
2. Install Python 3 and the GIS reader in a separate environment:

   ```powershell
   py -m venv .venv-terrain
   .\.venv-terrain\Scripts\Activate.ps1
   python -m pip install --upgrade pip rasterio numpy
   ```

3. Convert the source tiles into the mod cache. The importer's output resolution defaults to 0.01 degrees (about one kilometre); it uses average resampling, and records source tile names and processing details in a JSON sidecar.

   ```powershell
   python .\Tools\TerrainImport\prepare_historical_dem.py `
     'D:\GIS\Copernicus\*.tif' `
     --output .\Data\Terrain\Henan_GLO30.hrdem `
     --west 109 --south 31 --east 117 --north 37 `
     --resolution 0.01 `
     --source 'Copernicus DEM GLO-30 Public' `
     --source-version 'record the exact release/tile version here'
   ```

4. Retain `Data\Terrain\Henan_GLO30.json` with the cache. The map already references `HR_Terrain_Henan_GLO30`; the definition points at `Data/Terrain/Henan_GLO30.hrdem`. `required` is currently false so a missing cache is explicit in the log but does not prevent the prototype scenario from loading. Set it to true only after the imported cache is present and reviewed.

The public GLO-30 COG mirror lists Copernicus DEM 2021 release data, while the official product handbook describes the GLO-30 grid and accuracy characteristics. Copernicus DEM is a digital surface model (DSM); buildings and vegetation may contribute to measured height. Check the exact licence distributed with the data before redistributing source tiles or derived caches.

## Runtime sampling

- The source raster is resampled once offline; game startup only reads the compact cache.
- Each existing RimWorld tile center is inverse-projected through its scenario's HR-013 scale. No second grid or generated tile IDs are introduced.
- Since RimWorld exposes tile centers but not a geographic polygon footprint through this integration, a deterministic equal-area disk and configured sample points approximate the tile footprint. This is logged and is not claimed to be exact polygon-area integration.
- The tile stores mean/minimum/maximum elevation in metres, mean slope, relief and classified `Hilliness` in `HistoricalTerrainWorldComponent`, using Scribe. Its native RimWorld `Tile.elevation` gets a separate normalized, positive value because it is a planet-height offset, not metres above sea level.
- Slope and relief classify Flat/SmallHills/LargeHills/Mountainous; absolute elevation alone never makes a mountain. Thresholds and normalization are configured on `HistoricalTerrainDatasetDef`.
- Invalid, out-of-bounds and no-data pixels are skipped. If no existing tile intersects the cache after inverse projection, world generation reports an error rather than silently presenting the data as applied.
- With Dev Mode enabled, selecting an imported tile in the world map panel shows tile ID, historical coordinates, elevation statistics, slope, terrain and environment evidence/confidence.

## Historical environment limits

The current importer handles terrain elevation only. It does not derive Eastern Zhou forests, grasslands, wetlands, soils, ancient river courses, crossings or roads from modern elevation data. Such environment state is separately stored with `Unknown` evidence and zero confidence until period-specific historical/geological datasets are added. The scenario's RimWorld biome remains its existing configured biome; it is not mislabeled as a reconstructed 770 BCE ecosystem.

The tile-footprint disk is a scale-aware approximation. A future upgrade can replace it with sampling over actual native tile polygons or an offline cache pre-aggregated against the exact RimWorld grid. Current cache values remain independent of world seed and map zoom; only the sampling footprint and native tile assignment vary with the existing map.

## Data source references

- Copernicus DEM public COG registry and access notes: <https://registry.opendata.aws/copernicus-dem/>
- Copernicus DEM public COG readme: <https://copernicus-dem-30m.s3.amazonaws.com/readme.html>
- Copernicus DEM product handbook: <https://dataspace.copernicus.eu/sites/default/files/media/files/2024-06/geo1988-copernicusdem-spe-002_producthandbook_i5.0.pdf>
