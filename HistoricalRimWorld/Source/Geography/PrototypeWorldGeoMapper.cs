using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.World;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.Geography
{
    /// <summary>Uses explicitly assigned RimWorld tile IDs. Coordinates returned by this mapper are prototype-world coordinates only.</summary>
    public sealed class PrototypeWorldGeoMapper : IHistoricalWorldGeoMapper
    {
        private readonly HistoricalWorldRevealWorldComponent component;

        public PrototypeWorldGeoMapper(HistoricalWorldRevealWorldComponent revealComponent)
        {
            component = revealComponent;
        }

        public bool IsAvailable => component != null && component.MapMode == HistoricalWorldMapMode.PrototypeWorld
            && component.PrototypeMappings.Any();

        public bool TryGetCoordinate(int tileId, out HistoricalGeoCoordinate coordinate)
        {
            coordinate = default(HistoricalGeoCoordinate);
            if (!IsAvailable || Find.WorldGrid == null || tileId < 0 || tileId >= Find.WorldGrid.TilesCount
                || !component.PrototypeMappings.Any(item => item.TileId == tileId)) return false;

            // RimWorld's generated-planet coordinates are not Earth coordinates. This conversion is only useful for prototype tooling.
            UnityEngine.Vector2 worldCoordinate = Find.WorldGrid.LongLatOf(new PlanetTile(tileId));
            try
            {
                coordinate = new HistoricalGeoCoordinate(worldCoordinate.y, worldCoordinate.x);
                return true;
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public bool TryGetTile(HistoricalGeoCoordinate coordinate, out int tileId)
        {
            tileId = -1;
            if (!IsAvailable || Find.WorldGrid == null) return false;

            double bestDistance = double.PositiveInfinity;
            foreach (int candidate in component.PrototypeMappings.Select(item => item.TileId).Distinct())
            {
                HistoricalGeoCoordinate candidateCoordinate;
                if (!TryGetCoordinate(candidate, out candidateCoordinate)) continue;
                double latitudeDelta = candidateCoordinate.Latitude - coordinate.Latitude;
                double longitudeDelta = candidateCoordinate.Longitude - coordinate.Longitude;
                double distance = latitudeDelta * latitudeDelta + longitudeDelta * longitudeDelta;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                tileId = candidate;
            }
            return tileId >= 0;
        }

        public IReadOnlyList<int> GetTilesForRegion(string regionId)
        {
            if (!IsAvailable || regionId == null) return new List<int>().AsReadOnly();
            return component.PrototypeMappings
                .Where(item => item != null && item.RegionIds.Contains(regionId))
                .Select(item => item.TileId)
                .Distinct()
                .OrderBy(item => item)
                .ToList()
                .AsReadOnly();
        }
    }
}
