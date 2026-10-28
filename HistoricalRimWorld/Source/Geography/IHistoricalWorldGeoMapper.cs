using System.Collections.Generic;

namespace HistoricalRimWorld.Geography
{
    public interface IHistoricalWorldGeoMapper
    {
        bool IsAvailable { get; }
        bool TryGetCoordinate(int tileId, out HistoricalGeoCoordinate coordinate);
        bool TryGetTile(HistoricalGeoCoordinate coordinate, out int tileId);
        IReadOnlyList<int> GetTilesForRegion(string regionId);
    }
}
