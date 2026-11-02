using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldFeatures
{
    /// <summary>Small RimWorld 1.6 compatibility boundary around native WorldFeature APIs.</summary>
    public static class WorldFeatureAdapter
    {
        private static RimWorldWorld cachedWorld;
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
            world.features.UpdateFeatures();
        }
    }
}
