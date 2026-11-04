using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.World;
using HistoricalRimWorld.WorldGeneration;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldFeatures
{
    [XmlRoot("HistoricalWorldFeatureLayouts")]
    public sealed class HistoricalWorldFeatureLayoutFile
    {
        [XmlAttribute("schemaVersion")] public int schemaVersion = 2;
        [XmlAttribute("scenarioId")] public string scenarioId;
        [XmlAttribute("scaleDefName")] public string scaleDefName;
        [XmlElement("Feature")] public List<HistoricalWorldFeatureLayoutRecord> features = new List<HistoricalWorldFeatureLayoutRecord>();
    }

    public sealed class HistoricalWorldFeatureLayoutRecord
    {
        [XmlAttribute("id")] public string id;
        [XmlAttribute("name")] public string name;
        [XmlAttribute("featureDefName")] public string featureDefName;
        [XmlAttribute("drawAngle")] public float drawAngle;
        [XmlAttribute("terrainColor")] public string terrainColor;
        [XmlAttribute("terrainColorPriority")] public int terrainColorPriority;
        [XmlElement("Tile")] public List<HistoricalWorldFeatureTileCoordinate> tiles = new List<HistoricalWorldFeatureTileCoordinate>();
    }

    public sealed class HistoricalWorldFeatureTileCoordinate
    {
        [XmlAttribute("longitude")] public double longitude;
        [XmlAttribute("latitude")] public double latitude;
    }

    /// <summary>Exports native feature membership as historical coordinates so it can be remapped to a regenerated grid.</summary>
    public static class HistoricalWorldFeatureLayoutFileStore
    {
        private const int MaximumFeatures = 10000;
        private const int MaximumTileReferences = 200000;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(HistoricalWorldFeatureLayoutFile));

        public static string GetPath(string scenarioId)
        {
            string safe = new string((scenarioId ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character == '_' || character == '-').ToArray());
            if (string.IsNullOrWhiteSpace(safe)) throw new ArgumentException("Scenario ID is invalid.", nameof(scenarioId));
            return Path.Combine(GenFilePaths.ConfigFolderPath, "HistoricalRimWorld", "WorldFeatures", "Layouts", safe + ".xml");
        }

        public static bool TrySave(RimWorldWorld world, string scenarioId, HistoricalMapScaleDef scale,
            out string path, out string error)
        {
            path = null; error = null;
            if (world?.grid?.Surface == null || world.features?.features == null || scale == null
                || string.IsNullOrWhiteSpace(scenarioId))
            { error = "World, native features, scenario, or historical scale is unavailable."; return false; }
            string temporary = null;
            try
            {
                HistoricalMapScaleTransformer transformer = new HistoricalMapScaleTransformer(scale);
                List<HistoricalWorldFeatureLayoutRecord> records = new List<HistoricalWorldFeatureLayoutRecord>();
                int totalTiles = 0;
                foreach (WorldFeature feature in WorldFeatureAdapter.GetAllFeatures(world)
                    .Where(item => ReferenceEquals(item.layer, world.grid.Surface)).OrderBy(item => item.uniqueID))
                {
                    List<int> members = WorldFeatureAdapter.GetFeatureTiles(world, feature).ToList();
                    if (members.Count == 0) continue;
                    HistoricalWorldFeatureColorOverride color = world.GetComponent<HistoricalWorldTerrainColorWorldComponent>()
                        ?.GetFeatureOverride(feature.uniqueID);
                    HistoricalWorldFeatureLayoutRecord record = new HistoricalWorldFeatureLayoutRecord
                    {
                        id = "Feature_" + feature.uniqueID.ToString(Invariant), name = feature.name,
                        featureDefName = feature.def?.defName, drawAngle = feature.drawAngle,
                        terrainColor = color?.ColorHex, terrainColorPriority = color?.Priority ?? 0
                    };
                    foreach (int tileId in members)
                    {
                        Vector2 game = world.grid.Surface.LongLatOf(tileId);
                        HistoricalGeoCoordinate historical = transformer.ToHistoricalCoordinate(
                            new HistoricalGameGeoCoordinate(game.y, game.x));
                        record.tiles.Add(new HistoricalWorldFeatureTileCoordinate
                        { longitude = historical.Longitude, latitude = historical.Latitude });
                    }
                    totalTiles += record.tiles.Count;
                    records.Add(record);
                }
                if (records.Count > MaximumFeatures || totalTiles > MaximumTileReferences)
                { error = "Feature layout exceeds the configured safety limit."; return false; }

                path = GetPath(scenarioId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                HistoricalWorldFeatureLayoutFile file = new HistoricalWorldFeatureLayoutFile
                { scenarioId = scenarioId, scaleDefName = scale.defName, features = records };
                XmlWriterSettings settings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (XmlWriter writer = XmlWriter.Create(stream, settings)) Serializer.Serialize(writer, file);
                if (File.Exists(path))
                {
                    string backup = path + "." + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", Invariant) + ".bak";
                    try { File.Replace(temporary, path, backup, true); }
                    catch (PlatformNotSupportedException) { ReplaceByMoves(temporary, path, backup); }
                    catch (NotSupportedException) { ReplaceByMoves(temporary, path, backup); }
                }
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception exception) { error = "Could not save native feature layouts: " + exception.Message; return false; }
            finally { if (temporary != null && File.Exists(temporary)) try { File.Delete(temporary); } catch { } }
        }

        public static bool TryLoadAndApply(RimWorldWorld world, string scenarioId, HistoricalMapScaleDef scale,
            float maximumTileErrorDegrees, out bool fileFound, out int createdCount, out string error)
        {
            fileFound = false; createdCount = 0; error = null;
            if (world?.grid?.Surface == null || world.features?.features == null || scale == null)
            { error = "World surface, native features, or historical projection is unavailable."; return false; }
            string path;
            try { path = GetPath(scenarioId); }
            catch (Exception exception) { error = exception.Message; return false; }
            if (!File.Exists(path)) return true;
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > 64L * 1024L * 1024L)
                    throw new InvalidDataException("Feature layout XML is empty or exceeds the 64 MB limit.");
                HistoricalWorldFeatureLayoutFile file;
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (FileStream stream = File.OpenRead(path))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                    file = (HistoricalWorldFeatureLayoutFile)Serializer.Deserialize(reader);
                if (file == null || file.schemaVersion < 1 || file.schemaVersion > 2
                    || file.scenarioId != scenarioId)
                    throw new InvalidDataException("Feature layout schema or scenario ID does not match.");
                if (file.features == null || file.features.Count > MaximumFeatures
                    || file.features.Sum(item => item?.tiles?.Count ?? 0) > MaximumTileReferences)
                    throw new InvalidDataException("Feature layout count exceeds the safety limit.");

                fileFound = true;
                int restoredColorOverrides = 0;
                // A saved layout represents the complete feature layer for this scenario.
                // Clear generated labels only after the file has passed structural validation.
                WorldFeatureAdapter.ClearAllFeatures(world);
                HistoricalMapScaleTransformer transformer = new HistoricalMapScaleTransformer(scale);
                TileCoordinateResolver resolver = new TileCoordinateResolver(world, transformer);
                HashSet<int> claimedTiles = new HashSet<int>();
                foreach (HistoricalWorldFeatureLayoutRecord record in file.features)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.name)
                        || string.IsNullOrWhiteSpace(record.featureDefName) || record.tiles == null || record.tiles.Count == 0)
                    { Log.Warning("[Historical RimWorld] Skipped an incomplete saved native feature layout."); continue; }
                    FeatureDef featureDef = DefDatabase<FeatureDef>.GetNamedSilentFail(record.featureDefName);
                    if (featureDef == null)
                    { Log.Warning("[Historical RimWorld] Skipped feature layout " + record.id
                        + ": native FeatureDef is unavailable: " + record.featureDefName + "."); continue; }

                    List<int> members = new List<int>();
                    foreach (HistoricalWorldFeatureTileCoordinate coordinate in record.tiles)
                    {
                        if (coordinate == null || coordinate.longitude < -180d || coordinate.longitude > 180d
                            || coordinate.latitude < -90d || coordinate.latitude > 90d)
                        { Log.Warning("[Historical RimWorld] Skipped an invalid coordinate in feature layout " + record.id + "."); continue; }
                        int tileId = resolver.Find(coordinate.longitude, coordinate.latitude, out double errorDegrees);
                        if (tileId < 0 || errorDegrees > maximumTileErrorDegrees)
                        { Log.Warning("[Historical RimWorld] Could not map a saved feature tile at "
                            + coordinate.longitude.ToString("0.####", Invariant) + "," + coordinate.latitude.ToString("0.####", Invariant)
                            + " (offset " + errorDegrees.ToString("0.##", Invariant) + "°); skipped."); continue; }
                        if (!claimedTiles.Add(tileId))
                        { Log.Warning("[Historical RimWorld] Feature layout tile " + tileId + " was already assigned to another saved feature; skipped duplicate."); continue; }
                        members.Add(tileId);
                    }
                    if (members.Count == 0) continue;
                    WorldFeature feature = new WorldFeature(featureDef, world.grid.Surface) { name = record.name };
                    if (feature.uniqueID < 0 || feature.uniqueID >= ushort.MaxValue)
                    { Log.Warning("[Historical RimWorld] Native feature ID exceeded the tile mapping limit; stopped applying layouts."); break; }
                    world.features.features.Add(feature);
                    foreach (int tileId in members) WorldFeatureAdapter.TrySetTileFeature(world, tileId, feature);
                    WorldFeatureAdapter.RecalculateFeatureGeometry(world, feature);
                    feature.drawAngle = record.drawAngle;
                    if (!string.IsNullOrWhiteSpace(record.terrainColor))
                    {
                        if (!HistoricalWorldTerrainColorManager.TryParseHex(record.terrainColor, out _, out string normalized))
                            Log.Warning("[Historical RimWorld] Ignored invalid saved terrain color for feature layout " + record.id + ".");
                        else
                        {
                            HistoricalWorldTerrainColorWorldComponent colors =
                                world.GetComponent<HistoricalWorldTerrainColorWorldComponent>();
                            if (colors != null)
                            {
                                Vector2 game = world.grid.Surface.LongLatOf(members[0]);
                                HistoricalGeoCoordinate historical = transformer.ToHistoricalCoordinate(
                                    new HistoricalGameGeoCoordinate(game.y, game.x));
                                colors.SetFeatureOverride(feature.uniqueID, feature.def.defName, feature.name,
                                    historical.Longitude, historical.Latitude,
                                    normalized, record.terrainColorPriority);
                                restoredColorOverrides++;
                            }
                        }
                    }
                    createdCount++;
                }
                if (createdCount > 0)
                {
                    WorldFeatureAdapter.ScheduleLabelRefresh(world);
                    HistoricalWorldTerrainColorManager.Invalidate(world, true);
                    Log.Message("[Historical RimWorld] Restored " + createdCount
                        + " native World Feature layout(s), including " + restoredColorOverrides
                        + " terrain color override(s), from " + path + ".");
                }
                return true;
            }
            catch (Exception exception)
            { error = "Saved World Feature layouts could not be loaded: " + exception.Message; return false; }
        }

        private static void ReplaceByMoves(string temporary, string target, string backup)
        {
            File.Move(target, backup);
            try { File.Move(temporary, target); }
            catch
            {
                if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                throw;
            }
        }

        private sealed class TileCoordinateResolver
        {
            private readonly Vector3[] centers;
            private readonly HistoricalMapScaleTransformer transformer;
            public TileCoordinateResolver(RimWorldWorld world, HistoricalMapScaleTransformer transformer)
            {
                this.transformer = transformer;
                centers = new Vector3[world.grid.Surface.TilesCount];
                for (int tileId = 0; tileId < centers.Length; tileId++)
                {
                    Vector2 coordinate = world.grid.Surface.LongLatOf(tileId);
                    centers[tileId] = HistoricalCoverageCalculator.ToUnitVector(coordinate.x, coordinate.y);
                }
            }
            public int Find(double longitude, double latitude, out double errorDegrees)
            {
                HistoricalGameGeoCoordinate game = transformer.ToGameCoordinate(new HistoricalGeoCoordinate(latitude, longitude));
                Vector3 target = HistoricalCoverageCalculator.ToUnitVector(game.Longitude, game.Latitude);
                int nearest = -1; double bestDot = double.NegativeInfinity;
                for (int index = 0; index < centers.Length; index++)
                {
                    double dot = Vector3.Dot(target, centers[index]);
                    if (dot <= bestDot) continue;
                    bestDot = dot; nearest = index;
                }
                errorDegrees = nearest < 0 ? double.PositiveInfinity
                    : Math.Acos(Math.Max(-1d, Math.Min(1d, bestDot))) * 180d / Math.PI;
                return nearest;
            }
        }
    }
}
