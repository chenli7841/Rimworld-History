using System.Collections.Generic;

namespace HistoricalRimWorld.Geography
{
    /// <summary>Disabled until a verified Earth-backed RimWorld world and tile index are available.</summary>
    public sealed class HistoricalEarthGeoMapper : IHistoricalWorldGeoMapper
    {
        private static readonly IReadOnlyList<int> NoTiles = new List<int>().AsReadOnly();

        public bool IsAvailable => false;

        public bool TryGetCoordinate(int tileId, out HistoricalGeoCoordinate coordinate)
        {
            coordinate = default(HistoricalGeoCoordinate);
            return false;
        }

        public bool TryGetTile(HistoricalGeoCoordinate coordinate, out int tileId)
        {
            tileId = -1;
            return false;
        }

        public IReadOnlyList<int> GetTilesForRegion(string regionId) => NoTiles;
    }
}
