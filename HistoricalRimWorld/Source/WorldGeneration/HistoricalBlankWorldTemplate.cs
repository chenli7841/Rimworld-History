using RimWorld;
using RimWorld.Planet;
using HistoricalRimWorld.World;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.WorldFeatures;
using Verse;
using System.Collections.Generic;
using System;
using UnityEngine;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldGeneration
{
    /// <summary>
    /// Replaces generated surface terrain with a uniform, editable land template.
    /// This is a staging template, not a representation of real-world geography.
    /// </summary>
    public static class HistoricalBlankWorldTemplate
    {
        public static bool TryApply(RimWorldWorld world, HistoricalWorldMapDef map,
            HistoricalMapScaleDef scale, float maximumCoordinateErrorDegrees, out string error)
        {
            error = null;
            Log.Message("[Historical RimWorld] 正在创建指定的地图（读取独立 XML 地图数据）。");
            if (world?.grid == null || world.grid.Surface == null)
            {
                error = "The surface WorldGrid is unavailable.";
                return false;
            }

            if (map == null)
            {
                error = "The configured HistoricalWorldMapDef is missing.";
                return false;
            }

            HistoricalMapScaleTransformer transformer;
            try { transformer = new HistoricalMapScaleTransformer(scale); }
            catch (Exception exception)
            {
                error = "The historical map scale could not be initialized: " + exception.Message;
                return false;
            }

            BiomeDef baseBiome = map.defaultBiome;
            if (baseBiome == null || baseBiome.impassable || !baseBiome.canBuildBase)
            {
                error = "The map XML defaultBiome is unavailable or not suitable for settlement.";
                return false;
            }
            if (map.defaultElevation <= 0f)
            {
                error = "The map XML defaultElevation must be above zero; use tile overrides to add water.";
                return false;
            }

            Dictionary<int, HistoricalWorldTileOverride> overrides = new Dictionary<int, HistoricalWorldTileOverride>();
            Dictionary<int, HistoricalMapTileDescription> descriptions = new Dictionary<int, HistoricalMapTileDescription>();
            int skippedDuplicateOverrides = 0;
            foreach (HistoricalWorldTileOverride tileOverride in map.tileOverrides ?? new List<HistoricalWorldTileOverride>())
            {
                if (tileOverride == null
                    || tileOverride.longitude < -180f || tileOverride.longitude > 180f
                    || tileOverride.latitude < -90f || tileOverride.latitude > 90f)
                {
                    error = "The map XML contains an invalid tile coordinate. Longitude must be -180..180 and latitude -90..90.";
                    return false;
                }
                if (tileOverride.biome == null)
                {
                    error = "The map XML coordinate " + FormatCoordinate(tileOverride.longitude, tileOverride.latitude)
                        + " has no biome.";
                    return false;
                }
                string coordinate = FormatCoordinate(tileOverride.longitude, tileOverride.latitude);
                HistoricalGameGeoCoordinate gameCoordinate;
                try
                {
                    gameCoordinate = transformer.ToGameCoordinate(
                        new HistoricalGeoCoordinate(tileOverride.latitude, tileOverride.longitude));
                }
                catch (Exception exception)
                {
                    error = "Map coordinate " + coordinate + " cannot be projected: " + exception.Message;
                    return false;
                }
                int tileId = FindNearestTile(world, (float)gameCoordinate.Longitude, (float)gameCoordinate.Latitude);
                if (tileId < 0)
                {
                    error = "No WorldGrid tile could be resolved for coordinate "
                        + FormatCoordinate(tileOverride.longitude, tileOverride.latitude) + ".";
                    return false;
                }
                if (overrides.ContainsKey(tileId))
                {
                    HistoricalWorldTileOverride previous = overrides[tileId];
                    skippedDuplicateOverrides++;
                    Log.Warning("[Historical RimWorld] Ignored duplicate map coordinate " + coordinate
                        + ": it resolves to WorldGrid tile " + tileId + ", already assigned by earlier coordinate "
                        + FormatCoordinate(previous.longitude, previous.latitude) + ". The first XML entry is kept.");
                    continue;
                }
                Vector2 resolvedCenter = world.grid.Surface.LongLatOf(tileId);
                double offset = HistoricalCoverageCalculator.AngularDistance(gameCoordinate.Longitude,
                    gameCoordinate.Latitude, resolvedCenter.x, resolvedCenter.y);
                if (offset > maximumCoordinateErrorDegrees)
                {
                    error = "Map coordinate " + coordinate + " projected to "
                        + FormatCoordinate((float)gameCoordinate.Longitude, (float)gameCoordinate.Latitude)
                        + " resolves to WorldGrid tile " + tileId
                        + " centered at " + FormatCoordinate(resolvedCenter.x, resolvedCenter.y)
                        + " (angular offset " + offset.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                        + "°), exceeding the configured maximum of " + maximumCoordinateErrorDegrees + "°.";
                    return false;
                }
                overrides.Add(tileId, tileOverride);
                if (!string.IsNullOrWhiteSpace(tileOverride.description))
                    descriptions.Add(tileId, new HistoricalMapTileDescription(tileId, tileOverride.description.Trim()));
                string resolutionMessage = "[Historical RimWorld] Map coordinate " + coordinate + " projected to "
                    + FormatCoordinate((float)gameCoordinate.Longitude, (float)gameCoordinate.Latitude)
                    + " resolves to WorldGrid tile "
                    + tileId + " (tile center " + FormatCoordinate(resolvedCenter.x, resolvedCenter.y) + ", angular offset "
                    + offset.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°).";
                Log.Message(resolutionMessage);
            }

            HistoricalMapTileDescriptionWorldComponent descriptionComponent =
                world.GetComponent<HistoricalMapTileDescriptionWorldComponent>();
            if (descriptionComponent != null)
                descriptionComponent.SetDescriptions(descriptions.Values);
            else if (descriptions.Count > 0)
                Log.Warning("[Historical RimWorld] Tile descriptions were parsed but could not be saved because "
                    + "HistoricalMapTileDescriptionWorldComponent is unavailable.");

            // World generation has already placed vanilla landmarks on the generated
            // surface. Their terrain renderer expects nearby tiles with a different
            // biome so the landmark can rotate away from its background. A uniform
            // biome template cannot satisfy that constraint, so remove landmarks as
            // part of replacing the generated geography.
            int removedLandmarks = 0;
            if (world.landmarks != null)
            {
                for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
                {
                    PlanetTile planetTile = new PlanetTile(tileId, world.grid.Surface);
                    if (world.landmarks[planetTile] == null) continue;
                    world.landmarks.RemoveLandmark(planetTile);
                    removedLandmarks++;
                }
            }

            // This editable template starts as neutral land; keep no random vanilla
            // mountain/biome/world-feature names or hidden tile-to-feature links.
            WorldFeatureAdapter.ClearAllFeatures(world);

            for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
            {
                Tile tile = world.grid.Surface.Tiles[tileId];
                SurfaceTile surfaceTile = world.grid.Surface[tileId];
                if (tile == null || surfaceTile == null)
                {
                    error = "WorldGrid tile " + tileId + " is unavailable.";
                    return false;
                }

                HistoricalWorldTileOverride tileOverride;
                bool hasOverride = overrides.TryGetValue(tileId, out tileOverride);
                tile.PrimaryBiome = hasOverride ? tileOverride.biome : baseBiome;
                // RimWorld 1.6 marks a surface tile as water-covered when its elevation is <= 0.
                tile.elevation = hasOverride ? tileOverride.elevation : map.defaultElevation;
                tile.hilliness = hasOverride ? tileOverride.hilliness : map.defaultHilliness;
                tile.mutatorsNullable = new List<TileMutatorDef>();

                // WorldInspectPane assumes Roads is either null or non-empty before calling MaxBy.
                // Leaving a cleared-but-non-null list makes inspection of that tile throw.
                surfaceTile.potentialRivers = null;
                surfaceTile.potentialRoads = null;
                surfaceTile.riverDist = 0;

            }

            world.pathGrid?.RecalculateAllLayersPathCosts();
            Log.Message("[Historical RimWorld] Applied XML map " + map.defName + " at scale "
                + transformer.ScaleFactor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x to " + world.grid.TilesCount
                + " world tiles (" + overrides.Count + " coordinate-based tile overrides; "
                + skippedDuplicateOverrides + " duplicate coordinates ignored). Rivers, roads, and "
                + removedLandmarks + " generated landmarks were removed.");
            return true;
        }

        private static int FindNearestTile(RimWorldWorld world, float longitude, float latitude)
        {
            int nearestTileId = -1;
            double nearestDistance = double.PositiveInfinity;
            for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
            {
                Vector2 center = world.grid.Surface.LongLatOf(tileId);
                double distance = HistoricalCoverageCalculator.AngularDistance(longitude, latitude, center.x, center.y);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestTileId = tileId;
            }
            return nearestTileId;
        }

        private static string FormatCoordinate(float longitude, float latitude) =>
            longitude.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "°, "
            + latitude.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "°";
    }
}
