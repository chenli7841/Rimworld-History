using HistoricalRimWorld.Geography;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.Geography.Scaling
{
    public interface IHistoricalTerrainSource
    {
        bool TryGetTerrain(HistoricalGeoCoordinate historicalCoordinate,
            out BiomeDef biome, out float elevation, out Hilliness hilliness);
    }
}
