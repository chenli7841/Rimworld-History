using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.Geography.Terrain;
using HistoricalRimWorld.WorldGeneration;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    /// <summary>Scenario-specific edit files, stored outside the mod and anchored by historical coordinates.</summary>
    public static class HistoricalMapEditFileStore
    {
        private const int SchemaVersion = 5;
        private const int MaximumEntries = 200000;
        private const int MaximumDescriptionLength = 8192;
        private const long MaximumFileBytes = 64L * 1024L * 1024L;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string GetPath(string scenarioId)
        {
            string safeId = new string((scenarioId ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character == '_' || character == '-').ToArray());
            if (string.IsNullOrWhiteSpace(safeId)) throw new ArgumentException("Scenario ID is invalid.", nameof(scenarioId));
            return Path.Combine(GenFilePaths.ConfigFolderPath, "HistoricalRimWorld", "MapEdits", safeId + ".xml");
        }

        public static bool TrySave(HistoricalMapEditWorldComponent component,
            HistoricalMapTileDescriptionWorldComponent descriptionComponent, out string path, out string error)
        {
            path = null;
            error = null;
            if (component == null || string.IsNullOrWhiteSpace(component.ScenarioDefName))
            { error = "There is no active historical scenario edit layer to save."; return false; }
            List<HistoricalMapTerrainOverride> values = component.TerrainOverrides
                .Where(item => item != null).OrderBy(item => item.HistoricalLatitude)
                .ThenBy(item => item.HistoricalLongitude).ToList();
            List<HistoricalMapBiomeOverride> biomes = component.BiomeOverrides
                .Where(item => item != null).OrderBy(item => item.HistoricalLatitude)
                .ThenBy(item => item.HistoricalLongitude).ToList();
            List<HistoricalMapTileColorOverride> tileColors = component.TileColorOverrides
                .Where(item => item != null).OrderBy(item => item.HistoricalLatitude)
                .ThenBy(item => item.HistoricalLongitude).ToList();
            List<HistoricalMapTileDescription> descriptions = (descriptionComponent?.Descriptions
                    ?? Enumerable.Empty<HistoricalMapTileDescription>())
                .Where(item => item != null).OrderBy(item => item.TileId).ToList();
            List<HistoricalMapRiverPath> riverPaths = component.RiverPaths
                .Where(item => item != null).ToList();
            int riverPointCount = riverPaths.Sum(item => item.Points?.Count ?? 0);
            if (values.Count + biomes.Count + tileColors.Count + descriptions.Count + riverPaths.Count + riverPointCount > MaximumEntries)
            { error = "Map edit file exceeds the entry limit."; return false; }

            RimWorldWorld world = Find.World;
            HistoricalMapScaleDef scale = DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(component.ScaleDefName);
            if (descriptions.Count > 0 && (world?.grid?.Surface == null || scale == null))
            { error = "World grid or historical projection is unavailable for saving tile descriptions."; return false; }
            HistoricalMapScaleTransformer transformer = descriptions.Count == 0 ? null : new HistoricalMapScaleTransformer(scale);

            string target;
            string temp = null;
            try
            {
                target = GetPath(component.ScenarioDefName);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                XmlWriterSettings settings = new XmlWriterSettings
                { Indent = true, Encoding = new System.Text.UTF8Encoding(false), CloseOutput = false };
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (XmlWriter writer = XmlWriter.Create(stream, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("HistoricalMapEdits");
                    writer.WriteAttributeString("schemaVersion", SchemaVersion.ToString(Invariant));
                    writer.WriteAttributeString("scenarioId", component.ScenarioDefName);
                    writer.WriteAttributeString("scaleDefName", component.ScaleDefName ?? string.Empty);
                    writer.WriteAttributeString("gridTileCountAtExport", component.GridTileCount.ToString(Invariant));
                    writer.WriteAttributeString("projectionVersion", component.ProjectionVersion.ToString(Invariant));
                    writer.WriteAttributeString("scenarioMapVersion", component.ScenarioMapVersion.ToString(Invariant));
                    writer.WriteAttributeString("terrainDatasetDefName", component.TerrainDatasetDefName ?? string.Empty);
                    writer.WriteAttributeString("terrainDatasetVersion", component.TerrainDatasetVersion.ToString(Invariant));
                    foreach (HistoricalMapTerrainOverride item in values)
                    {
                        writer.WriteStartElement("TerrainOverride");
                        writer.WriteAttributeString("tileIdAtExport", item.TileId.ToString(Invariant));
                        writer.WriteAttributeString("longitude", item.HistoricalLongitude.ToString("R", Invariant));
                        writer.WriteAttributeString("latitude", item.HistoricalLatitude.ToString("R", Invariant));
                        writer.WriteAttributeString("baseTerrainAtExport", item.BaseTerrain.ToString());
                        writer.WriteAttributeString("terrain", item.Terrain.ToString());
                        writer.WriteEndElement();
                    }
                    foreach (HistoricalMapBiomeOverride item in biomes)
                    {
                        if (string.IsNullOrWhiteSpace(item.BiomeDefName)
                            || DefDatabase<BiomeDef>.GetNamedSilentFail(item.BiomeDefName) == null)
                            throw new InvalidDataException("A biome override references a missing BiomeDef: " + item.BiomeDefName + ".");
                        writer.WriteStartElement("BiomeOverride");
                        writer.WriteAttributeString("tileIdAtExport", item.TileId.ToString(Invariant));
                        writer.WriteAttributeString("longitude", item.HistoricalLongitude.ToString("R", Invariant));
                        writer.WriteAttributeString("latitude", item.HistoricalLatitude.ToString("R", Invariant));
                        writer.WriteAttributeString("baseBiomeAtExport", item.BaseBiomeDefName ?? string.Empty);
                        writer.WriteAttributeString("biome", item.BiomeDefName);
                        writer.WriteEndElement();
                    }
                    foreach (HistoricalMapTileColorOverride item in tileColors)
                    {
                        if (!HistoricalWorldTerrainColorManager.TryParseHex(item.ColorHex, out _, out string normalized))
                            throw new InvalidDataException("A tile color override contains an invalid RGB hex value.");
                        writer.WriteStartElement("TileColorOverride");
                        writer.WriteAttributeString("tileIdAtExport", item.TileId.ToString(Invariant));
                        writer.WriteAttributeString("longitude", item.HistoricalLongitude.ToString("R", Invariant));
                        writer.WriteAttributeString("latitude", item.HistoricalLatitude.ToString("R", Invariant));
                        writer.WriteAttributeString("color", normalized);
                        writer.WriteEndElement();
                    }
                    foreach (HistoricalMapTileDescription item in descriptions)
                    {
                        if (item.TileId < 0 || item.TileId >= world.grid.TilesCount)
                            throw new InvalidDataException("A tile description references an invalid WorldGrid tile ID: " + item.TileId + ".");
                        string description = item.Description ?? string.Empty;
                        if (description.Length > MaximumDescriptionLength)
                            throw new InvalidDataException("A tile description exceeds the 8192-character limit for tile " + item.TileId + ".");
                        Vector2 coordinate = world.grid.Surface.LongLatOf(item.TileId);
                        HistoricalGeoCoordinate historical = transformer.ToHistoricalCoordinate(
                            new HistoricalGameGeoCoordinate(coordinate.y, coordinate.x));
                        writer.WriteStartElement("TileDescription");
                        writer.WriteAttributeString("tileIdAtExport", item.TileId.ToString(Invariant));
                        writer.WriteAttributeString("longitude", historical.Longitude.ToString("R", Invariant));
                        writer.WriteAttributeString("latitude", historical.Latitude.ToString("R", Invariant));
                        writer.WriteString(description);
                        writer.WriteEndElement();
                    }
                    foreach (HistoricalMapRiverPath riverPath in riverPaths)
                    {
                        if (string.IsNullOrWhiteSpace(riverPath.Id)
                            || string.IsNullOrWhiteSpace(riverPath.RiverDefName)
                            || riverPath.Points == null || riverPath.Points.Count < 2)
                            throw new InvalidDataException("A saved river path is incomplete.");
                        writer.WriteStartElement("RiverPath");
                        writer.WriteAttributeString("id", riverPath.Id);
                        writer.WriteAttributeString("riverDefName", riverPath.RiverDefName);
                        foreach (HistoricalMapRiverPoint point in riverPath.Points)
                        {
                            if (point == null || point.Longitude < -180d || point.Longitude > 180d
                                || point.Latitude < -90d || point.Latitude > 90d)
                                throw new InvalidDataException("A river path contains an invalid historical coordinate.");
                            writer.WriteStartElement("Point");
                            writer.WriteAttributeString("longitude", point.Longitude.ToString("R", Invariant));
                            writer.WriteAttributeString("latitude", point.Latitude.ToString("R", Invariant));
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Flush();
                    stream.Flush(true);
                }
                ReplaceAtomically(temp, target);
                path = target;
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not save the scenario map edits: " + exception.Message;
                return false;
            }
            finally
            {
                if (temp != null && File.Exists(temp))
                {
                    try { File.Delete(temp); }
                    catch (Exception exception) { Log.Warning("[Historical RimWorld] Could not remove temporary map edit file: " + exception.Message); }
                }
            }
        }

        public static bool TryLoadAndApply(RimWorldWorld world, string scenarioId,
            HistoricalMapScaleDef scale, float maximumTileErrorDegrees, out int appliedCount, out string error)
        {
            appliedCount = 0;
            error = null;
            if (world?.grid?.Surface == null || scale == null)
            { error = "World surface or historical projection is unavailable."; return false; }

            string path;
            try { path = GetPath(scenarioId); }
            catch (Exception exception) { error = exception.Message; return false; }
            if (!File.Exists(path)) return true;

            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists) return true;
                if (info.Length <= 0 || info.Length > MaximumFileBytes)
                    throw new InvalidDataException("Map edit file is empty or exceeds the 64 MB safety limit.");
                XmlDocument document = new XmlDocument { XmlResolver = null };
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    document.Load(stream);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "HistoricalMapEdits")
                    throw new InvalidDataException("Root element must be HistoricalMapEdits.");
                if (!int.TryParse(root.GetAttribute("schemaVersion"), NumberStyles.Integer, Invariant, out int version)
                    || version < 1 || version > SchemaVersion) throw new InvalidDataException("Unsupported map edit schema version.");
                if (!string.Equals(root.GetAttribute("scenarioId"), scenarioId, StringComparison.Ordinal))
                    throw new InvalidDataException("Map edit file belongs to a different scenario.");
                XmlNodeList entries = root.SelectNodes("TerrainOverride");
                XmlNodeList biomeEntries = root.SelectNodes("BiomeOverride");
                XmlNodeList tileColorEntries = root.SelectNodes("TileColorOverride");
                XmlNodeList descriptionEntries = root.SelectNodes("TileDescription");
                XmlNodeList riverPathEntries = root.SelectNodes("RiverPath");
                if (entries == null || biomeEntries == null || tileColorEntries == null || descriptionEntries == null || riverPathEntries == null
                    || entries.Count + biomeEntries.Count + tileColorEntries.Count + descriptionEntries.Count + riverPathEntries.Count > MaximumEntries)
                    throw new InvalidDataException("Map edit entry count is invalid.");
                int totalRiverPoints = 0;
                foreach (XmlNode riverPathNode in riverPathEntries)
                    totalRiverPoints += riverPathNode.SelectNodes("Point")?.Count ?? 0;
                if (entries.Count + biomeEntries.Count + tileColorEntries.Count + descriptionEntries.Count + riverPathEntries.Count + totalRiverPoints > MaximumEntries)
                    throw new InvalidDataException("Map edit file exceeds the entry limit.");

                if (int.TryParse(root.GetAttribute("gridTileCountAtExport"), NumberStyles.Integer, Invariant, out int oldTileCount)
                    && oldTileCount > 0 && oldTileCount != world.grid.TilesCount)
                    Log.Warning("[Historical RimWorld] Scenario map edits were exported for " + oldTileCount
                        + " tiles; remapping by historical coordinates onto the current " + world.grid.TilesCount + "-tile grid.");
                if (!string.IsNullOrEmpty(root.GetAttribute("scaleDefName"))
                    && root.GetAttribute("scaleDefName") != scale.defName)
                    Log.Warning("[Historical RimWorld] Scenario map edits were exported with scale "
                        + root.GetAttribute("scaleDefName") + " and are being reprojected with " + scale.defName + ".");
                if (int.TryParse(root.GetAttribute("projectionVersion"), NumberStyles.Integer, Invariant, out int projectionVersion)
                    && projectionVersion != HistoricalMapScaleTransformer.CurrentProjectionVersion)
                    Log.Warning("[Historical RimWorld] Scenario map edits use projection version " + projectionVersion
                        + "; current version is " + HistoricalMapScaleTransformer.CurrentProjectionVersion + ". Geographic anchors will be reprojected.");
                if (int.TryParse(root.GetAttribute("scenarioMapVersion"), NumberStyles.Integer, Invariant, out int oldMapVersion))
                {
                    int currentMapVersion = world.GetComponent<HistoricalWorldRevealWorldComponent>()?.MapVersion ?? 0;
                    if (oldMapVersion > 0 && currentMapVersion > 0 && oldMapVersion != currentMapVersion)
                        Log.Warning("[Historical RimWorld] Scenario map edits came from map version " + oldMapVersion
                            + "; current version is " + currentMapVersion + ". Geographic coordinates are used for remapping.");
                }
                HistoricalTerrainWorldComponent currentTerrain = world.GetComponent<HistoricalTerrainWorldComponent>();
                string savedDataset = root.GetAttribute("terrainDatasetDefName");
                if (!string.IsNullOrEmpty(savedDataset) && currentTerrain != null
                    && (savedDataset != currentTerrain.DatasetDefName
                        || int.TryParse(root.GetAttribute("terrainDatasetVersion"), NumberStyles.Integer, Invariant, out int oldDatasetVersion)
                            && oldDatasetVersion != currentTerrain.DatasetVersion))
                    Log.Warning("[Historical RimWorld] Scenario terrain overrides were saved against DEM dataset "
                        + savedDataset + " but will be applied to the current terrain generation.");

                HistoricalMapScaleTransformer transformer = new HistoricalMapScaleTransformer(scale);
                HistoricalMapEditWorldComponent component = world.GetComponent<HistoricalMapEditWorldComponent>();
                if (component == null) throw new InvalidOperationException("HistoricalMapEditWorldComponent is unavailable.");
                component.Initialize(scenarioId, scale.defName);
                HistoricalMapTileDescriptionWorldComponent descriptionComponent =
                    world.GetComponent<HistoricalMapTileDescriptionWorldComponent>();
                if (descriptionEntries.Count > 0 && descriptionComponent == null)
                    throw new InvalidOperationException("HistoricalMapTileDescriptionWorldComponent is unavailable.");
                TileSpatialIndex tileIndex = new TileSpatialIndex(world);
                HashSet<int> occupiedTiles = new HashSet<int>();
                foreach (XmlNode node in entries)
                {
                    XmlElement entry = node as XmlElement;
                    if (entry == null) continue;
                    if (!double.TryParse(entry.GetAttribute("longitude"), NumberStyles.Float, Invariant, out double longitude)
                        || !double.TryParse(entry.GetAttribute("latitude"), NumberStyles.Float, Invariant, out double latitude)
                        || longitude < -180d || longitude > 180d || latitude < -90d || latitude > 90d)
                    { Log.Warning("[Historical RimWorld] Ignored a map edit with invalid geographic coordinates."); continue; }
                    if (!Enum.TryParse(entry.GetAttribute("terrain"), false, out Hilliness terrain)
                        || !Enum.IsDefined(typeof(Hilliness), terrain))
                    { Log.Warning("[Historical RimWorld] Ignored a map edit with an unsupported terrain value."); continue; }

                    int tileId;
                    double distance;
                    try
                    {
                        HistoricalGameGeoCoordinate projected = transformer.ToGameCoordinate(
                            new HistoricalGeoCoordinate(latitude, longitude));
                        tileId = tileIndex.FindNearest(projected.Longitude, projected.Latitude, out distance);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning("[Historical RimWorld] Ignored a scenario map edit that could not be projected: " + exception.Message);
                        continue;
                    }
                    if (tileId < 0 || distance > maximumTileErrorDegrees)
                    {
                        Log.Warning("[Historical RimWorld] Preserved but did not apply a terrain override at "
                            + FormatCoordinate(longitude, latitude) + "; nearest tile offset "
                            + distance.ToString("0.##", Invariant) + "° exceeds the current map limit.");
                        continue;
                    }
                    if (!occupiedTiles.Add(tileId))
                    {
                        Log.Warning("[Historical RimWorld] Ignored a saved terrain override at " + FormatCoordinate(longitude, latitude)
                            + " because it maps to an already assigned tile " + tileId + " on this grid.");
                        continue;
                    }

                    Tile tile = world.grid.Surface.Tiles[tileId];
                    Hilliness baseTerrain = tile.hilliness;
                    tile.hilliness = terrain;
                    component.SetTerrain(tileId, longitude, latitude, baseTerrain, terrain);
                    appliedCount++;
                }
                HashSet<int> biomeOverrideTiles = new HashSet<int>();
                foreach (XmlNode node in biomeEntries)
                {
                    XmlElement entry = node as XmlElement;
                    if (entry == null) continue;
                    if (!double.TryParse(entry.GetAttribute("longitude"), NumberStyles.Float, Invariant, out double longitude)
                        || !double.TryParse(entry.GetAttribute("latitude"), NumberStyles.Float, Invariant, out double latitude)
                        || longitude < -180d || longitude > 180d || latitude < -90d || latitude > 90d)
                    { Log.Warning("[Historical RimWorld] Ignored a biome override with invalid geographic coordinates."); continue; }
                    string biomeDefName = entry.GetAttribute("biome");
                    BiomeDef biome = DefDatabase<BiomeDef>.GetNamedSilentFail(biomeDefName);
                    if (biome == null)
                    { Log.Warning("[Historical RimWorld] Ignored a biome override with missing BiomeDef " + biomeDefName + "."); continue; }
                    int tileId;
                    double distance;
                    try
                    {
                        HistoricalGameGeoCoordinate projected = transformer.ToGameCoordinate(
                            new HistoricalGeoCoordinate(latitude, longitude));
                        tileId = tileIndex.FindNearest(projected.Longitude, projected.Latitude, out distance);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning("[Historical RimWorld] Ignored a biome override that could not be projected: " + exception.Message);
                        continue;
                    }
                    if (tileId < 0 || distance > maximumTileErrorDegrees)
                    {
                        Log.Warning("[Historical RimWorld] Preserved but did not apply a biome override at "
                            + FormatCoordinate(longitude, latitude) + "; nearest tile offset "
                            + distance.ToString("0.##", Invariant) + "° exceeds the current map limit.");
                        continue;
                    }
                    if (!biomeOverrideTiles.Add(tileId))
                    {
                        Log.Warning("[Historical RimWorld] Ignored a saved biome override at " + FormatCoordinate(longitude, latitude)
                            + " because it maps to an already assigned tile " + tileId + " on this grid.");
                        continue;
                    }
                    Tile tile = world.grid.Surface.Tiles[tileId];
                    if (tile?.PrimaryBiome == null) continue;
                    string baseBiome = tile.PrimaryBiome.defName;
                    tile.PrimaryBiome = biome;
                    component.SetBiome(tileId, longitude, latitude, baseBiome, biome.defName);
                    appliedCount++;
                }
                HashSet<int> colorOverrideTiles = new HashSet<int>();
                foreach (XmlNode node in tileColorEntries)
                {
                    XmlElement entry = node as XmlElement;
                    if (entry == null) continue;
                    if (!double.TryParse(entry.GetAttribute("longitude"), NumberStyles.Float, Invariant, out double longitude)
                        || !double.TryParse(entry.GetAttribute("latitude"), NumberStyles.Float, Invariant, out double latitude)
                        || longitude < -180d || longitude > 180d || latitude < -90d || latitude > 90d)
                    { Log.Warning("[Historical RimWorld] Ignored a tile color override with invalid geographic coordinates."); continue; }
                    if (!HistoricalWorldTerrainColorManager.TryParseHex(entry.GetAttribute("color"), out _, out string normalized))
                    { Log.Warning("[Historical RimWorld] Ignored a tile color override with invalid RGB hex color."); continue; }
                    int tileId;
                    double distance;
                    try
                    {
                        HistoricalGameGeoCoordinate projected = transformer.ToGameCoordinate(
                            new HistoricalGeoCoordinate(latitude, longitude));
                        tileId = tileIndex.FindNearest(projected.Longitude, projected.Latitude, out distance);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning("[Historical RimWorld] Ignored a tile color override that could not be projected: " + exception.Message);
                        continue;
                    }
                    if (tileId < 0 || distance > maximumTileErrorDegrees)
                    {
                        Log.Warning("[Historical RimWorld] Preserved but did not apply a tile color override at "
                            + FormatCoordinate(longitude, latitude) + "; nearest tile offset "
                            + distance.ToString("0.##", Invariant) + "° exceeds the current map limit.");
                        continue;
                    }
                    if (!colorOverrideTiles.Add(tileId))
                    {
                        Log.Warning("[Historical RimWorld] Ignored a duplicate tile color override at "
                            + FormatCoordinate(longitude, latitude) + " for current tile " + tileId + ".");
                        continue;
                    }
                    component.SetTileColor(tileId, longitude, latitude, normalized);
                    appliedCount++;
                }
                HashSet<int> describedTiles = new HashSet<int>();
                foreach (XmlNode node in descriptionEntries)
                {
                    XmlElement entry = node as XmlElement;
                    if (entry == null) continue;
                    if (!double.TryParse(entry.GetAttribute("longitude"), NumberStyles.Float, Invariant, out double longitude)
                        || !double.TryParse(entry.GetAttribute("latitude"), NumberStyles.Float, Invariant, out double latitude)
                        || longitude < -180d || longitude > 180d || latitude < -90d || latitude > 90d)
                    { Log.Warning("[Historical RimWorld] Ignored a tile description with invalid geographic coordinates."); continue; }
                    string description = entry.InnerText ?? string.Empty;
                    if (description.Length > MaximumDescriptionLength)
                    { Log.Warning("[Historical RimWorld] Ignored an overlong tile description at " + FormatCoordinate(longitude, latitude) + "."); continue; }

                    int tileId;
                    double distance;
                    try
                    {
                        HistoricalGameGeoCoordinate projected = transformer.ToGameCoordinate(
                            new HistoricalGeoCoordinate(latitude, longitude));
                        tileId = tileIndex.FindNearest(projected.Longitude, projected.Latitude, out distance);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning("[Historical RimWorld] Ignored a tile description that could not be projected: " + exception.Message);
                        continue;
                    }
                    if (tileId < 0 || distance > maximumTileErrorDegrees)
                    {
                        Log.Warning("[Historical RimWorld] Preserved but did not apply a tile description at "
                            + FormatCoordinate(longitude, latitude) + "; nearest tile offset "
                            + distance.ToString("0.##", Invariant) + "° exceeds the current map limit.");
                        continue;
                    }
                    if (!describedTiles.Add(tileId))
                    {
                        Log.Warning("[Historical RimWorld] Ignored a tile description at " + FormatCoordinate(longitude, latitude)
                            + " because it maps to an already assigned tile " + tileId + " on this grid.");
                        continue;
                    }
                    descriptionComponent.SetDescription(tileId, description);
                }
                foreach (XmlNode node in riverPathEntries)
                {
                    XmlElement entry = node as XmlElement;
                    if (entry == null) continue;
                    string id = entry.GetAttribute("id");
                    string riverDefName = entry.GetAttribute("riverDefName");
                    RiverDef river = DefDatabase<RiverDef>.GetNamedSilentFail(riverDefName);
                    if (string.IsNullOrWhiteSpace(id) || river == null)
                    {
                        Log.Warning("[Historical RimWorld] Ignored a saved river path with an invalid ID or missing RiverDef "
                            + riverDefName + ".");
                        continue;
                    }
                    List<HistoricalGeoCoordinate> points = new List<HistoricalGeoCoordinate>();
                    bool pointsValid = true;
                    foreach (XmlNode pointNode in entry.SelectNodes("Point"))
                    {
                        XmlElement point = pointNode as XmlElement;
                        if (point == null
                            || !double.TryParse(point.GetAttribute("longitude"), NumberStyles.Float, Invariant, out double longitude)
                            || !double.TryParse(point.GetAttribute("latitude"), NumberStyles.Float, Invariant, out double latitude))
                        { pointsValid = false; break; }
                        try { points.Add(new HistoricalGeoCoordinate(latitude, longitude)); }
                        catch (ArgumentOutOfRangeException) { pointsValid = false; break; }
                    }
                    if (!pointsValid || points.Count < 2)
                    {
                        Log.Warning("[Historical RimWorld] Ignored an invalid saved river path " + id + ".");
                        continue;
                    }
                    if (!HistoricalRiverPathManager.TryBuildPlan(world, transformer, maximumTileErrorDegrees,
                        river, points, out HistoricalRiverPathPlan plan, out string routeError))
                    {
                        Log.Warning("[Historical RimWorld] Skipped saved river path " + id + ": " + routeError);
                        continue;
                    }
                    try
                    {
                        HistoricalRiverPathManager.ApplyPlan(world, plan);
                        component.AddRiverPath(new HistoricalMapRiverPath
                        {
                            Id = id,
                            RiverDefName = riverDefName,
                            Points = points.Select(point => new HistoricalMapRiverPoint(
                                point.Longitude, point.Latitude)).ToList()
                        });
                    }
                    catch (Exception exception)
                    { Log.Warning("[Historical RimWorld] Could not apply saved river path " + id + ": " + exception.Message); }
                }
                HistoricalWorldTerrainColorManager.Invalidate(world, true);
                world.pathGrid?.RecalculateAllLayersPathCosts();
                return true;
            }
            catch (Exception exception)
            {
                error = "Map edit file could not be loaded; no saved edit was applied: " + exception.Message;
                return false;
            }
        }

        private sealed class TileSpatialIndex
        {
            private readonly Node root;

            public TileSpatialIndex(RimWorldWorld world)
            {
                TilePoint[] points = new TilePoint[world.grid.TilesCount];
                for (int tileId = 0; tileId < points.Length; tileId++)
                {
                    Vector2 center = world.grid.Surface.LongLatOf(tileId);
                    points[tileId] = new TilePoint(tileId,
                        HistoricalCoverageCalculator.ToUnitVector(center.x, center.y));
                }
                root = Build(points, 0, points.Length, 0);
            }

            public int FindNearest(double longitude, double latitude, out double angularDistance)
            {
                Vector3 target = HistoricalCoverageCalculator.ToUnitVector(longitude, latitude);
                TilePoint best = default(TilePoint);
                double bestSquaredDistance = double.PositiveInfinity;
                Search(root, target, ref best, ref bestSquaredDistance);
                angularDistance = Math.Acos(Math.Max(-1d, Math.Min(1d,
                    1d - bestSquaredDistance / 2d))) * 180d / Math.PI;
                return root == null ? -1 : best.TileId;
            }

            private static Node Build(TilePoint[] points, int start, int end, int axis)
            {
                if (start >= end) return null;
                Array.Sort(points, start, end - start, new AxisComparer(axis));
                int middle = start + (end - start) / 2;
                TilePoint point = points[middle];
                return new Node(point, axis,
                    Build(points, start, middle, (axis + 1) % 3),
                    Build(points, middle + 1, end, (axis + 1) % 3));
            }

            private static void Search(Node node, Vector3 target, ref TilePoint best, ref double bestDistanceSquared)
            {
                if (node == null) return;
                double dx = node.Point.Position.x - target.x;
                double dy = node.Point.Position.y - target.y;
                double dz = node.Point.Position.z - target.z;
                double distanceSquared = dx * dx + dy * dy + dz * dz;
                if (distanceSquared < bestDistanceSquared)
                { bestDistanceSquared = distanceSquared; best = node.Point; }

                double difference = AxisValue(target, node.Axis) - AxisValue(node.Point.Position, node.Axis);
                Node first = difference < 0d ? node.Left : node.Right;
                Node second = difference < 0d ? node.Right : node.Left;
                Search(first, target, ref best, ref bestDistanceSquared);
                if (difference * difference < bestDistanceSquared)
                    Search(second, target, ref best, ref bestDistanceSquared);
            }

            private static double AxisValue(Vector3 value, int axis) => axis == 0 ? value.x : axis == 1 ? value.y : value.z;

            private sealed class Node
            {
                public readonly TilePoint Point;
                public readonly int Axis;
                public readonly Node Left;
                public readonly Node Right;
                public Node(TilePoint point, int axis, Node left, Node right)
                { Point = point; Axis = axis; Left = left; Right = right; }
            }

            private struct TilePoint
            {
                public readonly int TileId;
                public readonly Vector3 Position;
                public TilePoint(int tileId, Vector3 position) { TileId = tileId; Position = position; }
            }

            private sealed class AxisComparer : IComparer<TilePoint>
            {
                private readonly int axis;
                public AxisComparer(int axis) { this.axis = axis; }
                public int Compare(TilePoint left, TilePoint right) =>
                    AxisValue(left.Position, axis).CompareTo(AxisValue(right.Position, axis));
            }
        }

        private static string FormatCoordinate(double longitude, double latitude) =>
            longitude.ToString("0.####", Invariant) + "°, " + latitude.ToString("0.####", Invariant) + "°";

        private static void ReplaceAtomically(string temp, string target)
        {
            if (!File.Exists(target)) { File.Move(temp, target); return; }
            string backup = target + "." + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", Invariant) + ".bak";
            try { File.Replace(temp, target, backup, true); }
            catch (PlatformNotSupportedException) { ReplaceByMoves(temp, target, backup); }
            catch (NotSupportedException) { ReplaceByMoves(temp, target, backup); }
        }

        private static void ReplaceByMoves(string temp, string target, string backup)
        {
            File.Move(target, backup);
            try { File.Move(temp, target); }
            catch
            {
                if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                throw;
            }
        }
    }
}
