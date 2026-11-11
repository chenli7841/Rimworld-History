using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Maps TerrainGrid callbacks to their owning map without reflecting into RimWorld internals.</summary>
    internal static class ForestSoilErosionRegistry
    {
        private static readonly Dictionary<TerrainGrid, Map> MapsByGrid = new Dictionary<TerrainGrid, Map>();

        public static void Register(Map map)
        {
            if (map?.terrainGrid != null) MapsByGrid[map.terrainGrid] = map;
        }

        public static void Unregister(Map map)
        {
            if (map?.terrainGrid != null) MapsByGrid.Remove(map.terrainGrid);
        }

        public static void NotifyTerrainChanged(TerrainGrid grid, IntVec3 cell)
        {
            if (grid != null && MapsByGrid.TryGetValue(grid, out Map map))
            {
                map.GetComponent<MapComponent_ForestSoilErosion>()?.NotifyTerrainChanged(cell);
                map.GetComponent<MapComponent_ForestEcology>()?.MarkRegionDirty(cell);
            }
        }
    }
}
