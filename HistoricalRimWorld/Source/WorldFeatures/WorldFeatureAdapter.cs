using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using HistoricalRimWorld.WorldGeneration;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldFeatures
{
    /// <summary>Small RimWorld 1.6 compatibility boundary around native WorldFeature APIs.</summary>
    public static class WorldFeatureAdapter
    {
        private static RimWorldWorld cachedWorld;
        private static RimWorldWorld pendingLabelRefreshWorld;
        private static Dictionary<WorldFeature, IReadOnlyList<int>> tileCache =
            new Dictionary<WorldFeature, IReadOnlyList<int>>();
        public static IReadOnlyList<WorldFeature> GetAllFeatures(RimWorldWorld world)
        {
            if (world?.features?.features == null) return new List<WorldFeature>().AsReadOnly();
            return world.features.features.Where(feature => feature != null).ToList().AsReadOnly();
        }

        public static string GetName(WorldFeature feature) => feature?.name ?? string.Empty;

        public static bool TrySetName(WorldFeature feature, string name)
        {
            if (feature == null || string.IsNullOrWhiteSpace(name)) return false;
            feature.name = name.Trim();
            return true;
        }

        public static string GetFeatureType(WorldFeature feature) => feature?.def?.defName ?? string.Empty;

        public static IReadOnlyList<int> GetFeatureTiles(RimWorldWorld world, WorldFeature feature)
        {
            if (world?.grid?.Surface?.Tiles == null || feature == null) return new List<int>().AsReadOnly();
            EnsureTileCache(world);
            return tileCache.TryGetValue(feature, out IReadOnlyList<int> tileIds)
                ? tileIds : new List<int>().AsReadOnly();
        }

        private static void EnsureTileCache(RimWorldWorld world)
        {
            if (ReferenceEquals(world, cachedWorld)) return;
            cachedWorld = world;
            tileCache = new Dictionary<WorldFeature, IReadOnlyList<int>>();
            Dictionary<WorldFeature, List<int>> mutable = new Dictionary<WorldFeature, List<int>>();
            for (int tileId = 0; tileId < world.grid.Surface.TilesCount; tileId++)
            {
                WorldFeature feature = world.grid.Surface.Tiles[tileId]?.feature;
                if (feature == null) continue;
                if (!mutable.TryGetValue(feature, out List<int> ids)) mutable[feature] = ids = new List<int>();
                ids.Add(tileId);
            }
            foreach (KeyValuePair<WorldFeature, List<int>> pair in mutable)
                tileCache[pair.Key] = pair.Value.AsReadOnly();
        }

        public static void RefreshFeatureLabels(RimWorldWorld world)
        {
            if (world?.features == null) return;

            // UpdateFeatures() only rebuilds its static text-mesh list when that
            // WorldFeatures instance has textsCreated == false. Editing the existing
            // feature list leaves that flag true and can make its per-frame update
            // index stale text meshes. Replace the lightweight native container to
            // reset that lifecycle state, preserving the actual feature objects.
            RimWorld.Planet.WorldFeatures refreshed = new RimWorld.Planet.WorldFeatures();
            if (world.features.features != null)
                refreshed.features.AddRange(world.features.features);
            world.features = refreshed;
            refreshed.UpdateFeatures();
        }

        public static void ScheduleLabelRefresh(RimWorldWorld world)
        {
            if (world == null) return;
            // World generation and its Harmony postfix can run inside RimWorld's
            // asynchronous long event. Find.World may already point at that world,
            // but Unity objects (legacy feature text meshes) must only be rebuilt on
            // the main thread. The world-interface GUI patch drains this queue later.
            pendingLabelRefreshWorld = world;
        }

        public static void InvalidateTileCache() { cachedWorld = null; tileCache.Clear(); }

        public static bool TrySetTileFeature(RimWorldWorld world, int tileId, WorldFeature feature)
        {
            if (world?.grid?.Surface == null || tileId < 0 || tileId >= world.grid.Surface.TilesCount) return false;
            if (feature != null && feature.uniqueID >= ushort.MaxValue) return false;
            EnsureRawFeatureMap(world.grid.Surface);
            Tile tile = world.grid.Surface.Tiles[tileId];
            if (tile == null) return false;
            tile.feature = feature;
            byte[] raw = world.grid.Surface.tileFeature;
            int offset = tileId * 2;
            ushort value = feature == null ? ushort.MaxValue : (ushort)feature.uniqueID;
            raw[offset] = (byte)(value & 0xFF);
            raw[offset + 1] = (byte)(value >> 8);
            InvalidateTileCache();
            return true;
        }

        private static void EnsureRawFeatureMap(SurfaceLayer surface)
        {
            int requiredLength = surface.TilesCount * 2;
            if (surface.tileFeature != null && surface.tileFeature.Length == requiredLength) return;
            surface.tileFeature = new byte[requiredLength];
            for (int i = 0; i < requiredLength; i++) surface.tileFeature[i] = byte.MaxValue;
            for (int tileId = 0; tileId < surface.TilesCount; tileId++)
            {
                WorldFeature existing = surface.Tiles[tileId]?.feature;
                if (existing == null || existing.uniqueID >= ushort.MaxValue) continue;
                int offset = tileId * 2;
                ushort id = (ushort)existing.uniqueID;
                surface.tileFeature[offset] = (byte)(id & 0xFF);
                surface.tileFeature[offset + 1] = (byte)(id >> 8);
            }
        }

        public static void RecalculateFeatureGeometry(RimWorldWorld world, WorldFeature feature)
        {
            if (world?.grid?.Surface == null || feature == null) return;
            Vector3 sum = Vector3.zero;
            int count = 0;
            int centerTile = -1;
            List<int> tileIds = new List<int>();
            for (int tileId = 0; tileId < world.grid.Surface.TilesCount; tileId++)
            {
                if (!ReferenceEquals(world.grid.Surface.Tiles[tileId]?.feature, feature)) continue;
                tileIds.Add(tileId);
                Vector2 coordinate = world.grid.Surface.LongLatOf(tileId);
                sum += HistoricalCoverageCalculator.ToUnitVector(coordinate.x, coordinate.y);
                count++;
            }
            if (count == 0) return;
            Vector3 center = sum.sqrMagnitude < 1e-8f
                ? HistoricalCoverageCalculator.ToUnitVector(
                    world.grid.Surface.LongLatOf(tileIds[0]).x, world.grid.Surface.LongLatOf(tileIds[0]).y)
                : sum.normalized;
            feature.drawCenter = center * WorldGrid.PlanetRadius;
            feature.drawAngle = 0f;
            centerTile = tileIds[0];
            float radius = 0f;
            PlanetTile centerPlanetTile = new PlanetTile(centerTile, world.grid.Surface);
            foreach (int tileId in tileIds)
                radius = Mathf.Max(radius, world.grid.ApproxDistanceInTiles(
                    centerPlanetTile, new PlanetTile(tileId, world.grid.Surface)));
            feature.maxDrawSizeInTiles = Mathf.Max(1f, radius * 2f + 1f);
        }

        /// <summary>Removes all generated native feature objects and their tile/serialized references.</summary>
        public static int ClearAllFeatures(RimWorldWorld world)
        {
            if (world?.grid == null) return 0;
            int featureCount = world.features?.features?.Count ?? 0;
            int tileReferenceCount = 0;
            foreach (PlanetLayer layer in world.grid.PlanetLayers.Values)
            {
                if (layer?.Tiles == null) continue;
                foreach (Tile tile in layer.Tiles)
                {
                    if (tile?.feature == null) continue;
                    tile.feature = null;
                    tileReferenceCount++;
                }
            }

            SurfaceLayer surface = world.grid.Surface;
            if (surface?.tileFeature != null)
            {
                // SurfaceLayer stores each feature ID as a ushort in its raw byte buffer;
                // 0xFFFF is RimWorld's null feature sentinel in WorldFeatures.ExposeData.
                if (surface.tileFeature.Length != surface.TilesCount * 2)
                    surface.tileFeature = new byte[surface.TilesCount * 2];
                for (int index = 0; index < surface.tileFeature.Length; index++) surface.tileFeature[index] = byte.MaxValue;
            }
            world.features?.features?.Clear();
            InvalidateTileCache();

            ScheduleLabelRefresh(world);
            Log.Message("[Historical RimWorld] Cleared " + featureCount + " native World Feature(s) and "
                + tileReferenceCount + " tile reference(s) from the generated world.");
            return featureCount;
        }

        public static void RefreshPendingLabels(RimWorldWorld world)
        {
            if (world == null || !ReferenceEquals(world, pendingLabelRefreshWorld)) return;
            try
            {
                RefreshFeatureLabels(world);
                pendingLabelRefreshWorld = null;
            }
            catch (Exception exception)
            { Log.Warning("[Historical RimWorld] Could not refresh native World Feature labels: " + exception.Message); }
        }
    }
}
