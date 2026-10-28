using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Geography;
using Verse;

namespace HistoricalRimWorld.World
{
    public static class HistoricalWorldTileAccessCache
    {
        private static readonly Dictionary<int, string[]> regionsByTile = new Dictionary<int, string[]>();
        private static readonly HashSet<int> lockedTileIds = new HashSet<int>();
        private static bool mappingAvailable;
        private static bool coverageComplete;

        public static bool MappingAvailable => mappingAvailable;
        public static IReadOnlyCollection<int> LockedTileIds => lockedTileIds;

        public static bool TryGetRegions(int tileId, out IReadOnlyList<string> regionIds)
        {
            string[] regions;
            if (regionsByTile.TryGetValue(tileId, out regions))
            {
                regionIds = regions;
                return true;
            }

            regionIds = new string[0];
            return false;
        }

        public static bool IsAccessible(int tileId)
        {
            if (!mappingAvailable) return true;
            if (tileId < 0 || Find.WorldGrid == null || tileId >= Find.WorldGrid.TilesCount) return false;
            string[] regionIds;
            if (!regionsByTile.TryGetValue(tileId, out regionIds)) return !coverageComplete;
            return !regionIds.Any(regionId =>
            {
                HistoricalRegionRevealState state = HistoricalWorldRevealManager.GetRegionState(regionId);
                return state == null || state.Access == HistoricalRegionAccess.Locked;
            });
        }

        public static bool IsVisible(int tileId)
        {
            if (!mappingAvailable) return true;
            if (tileId < 0 || Find.WorldGrid == null || tileId >= Find.WorldGrid.TilesCount) return false;
            string[] regionIds;
            if (!regionsByTile.TryGetValue(tileId, out regionIds)) return !coverageComplete;
            return !regionIds.Any(regionId =>
            {
                HistoricalRegionRevealState state = HistoricalWorldRevealManager.GetRegionState(regionId);
                return state == null || state.Access == HistoricalRegionAccess.Locked;
            });
        }

        public static void Rebuild(HistoricalWorldRevealWorldComponent component)
        {
            regionsByTile.Clear();
            lockedTileIds.Clear();
            mappingAvailable = false;
            coverageComplete = false;

            if (component == null || !component.Initialized) return;
            coverageComplete = component.PrototypeMappingCoverageComplete;
            IHistoricalWorldGeoMapper mapper = HistoricalGeographyManager.CurrentMapper;
            if (mapper == null || !mapper.IsAvailable) return;

            foreach (HistoricalPrototypeTileMapping mapping in component.PrototypeMappings.Where(item => item != null))
            {
                if (Find.WorldGrid == null || mapping.TileId < 0 || mapping.TileId >= Find.WorldGrid.TilesCount) continue;
                string[] regionIds = mapping.RegionIds.Distinct().OrderBy(id => id, System.StringComparer.Ordinal).ToArray();
                if (regionIds.Length == 0) continue;
                regionsByTile[mapping.TileId] = regionIds;
                mappingAvailable = true;
                string primaryRegionId = HistoricalGeographyManager.GetPrimaryRegionForTile(mapping.TileId);
                HistoricalRegionRevealState primaryState = component.FindRegion(primaryRegionId);
                if (primaryState == null || primaryState.Access == HistoricalRegionAccess.Locked)
                    lockedTileIds.Add(mapping.TileId);
            }
        }
    }
}
