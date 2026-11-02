using Verse;
using RimWorld;
using RimWorld.Planet;

namespace HistoricalRimWorld.Geography.Terrain
{
    public enum HistoricalTerrainEnvironmentEvidence
    {
        Unknown,
        Observed,
        HistoricallyDocumented,
        Inferred,
        GameplayApproximation
    }

    /// <summary>Terrain measurements and source evidence for one existing RimWorld tile.</summary>
    public sealed class HistoricalTerrainTileState : IExposable
    {
        public int TileId;
        public double HistoricalLongitude;
        public double HistoricalLatitude;
        public float MeanElevationMeters;
        public float MinElevationMeters;
        public float MaxElevationMeters;
        public float MeanSlopeDegrees;
        public float MaximumSlopeDegrees;
        public float RuggednessMeters;
        public Hilliness TerrainType;
        public HistoricalTerrainEnvironmentEvidence EnvironmentEvidence;
        public float EnvironmentConfidence;

        public void ExposeData()
        {
            Scribe_Values.Look(ref TileId, "tileId", -1);
            Scribe_Values.Look(ref HistoricalLongitude, "historicalLongitude");
            Scribe_Values.Look(ref HistoricalLatitude, "historicalLatitude");
            Scribe_Values.Look(ref MeanElevationMeters, "meanElevationMeters");
            Scribe_Values.Look(ref MinElevationMeters, "minElevationMeters");
            Scribe_Values.Look(ref MaxElevationMeters, "maxElevationMeters");
            Scribe_Values.Look(ref MeanSlopeDegrees, "meanSlopeDegrees");
            Scribe_Values.Look(ref MaximumSlopeDegrees, "maximumSlopeDegrees");
            Scribe_Values.Look(ref RuggednessMeters, "ruggednessMeters");
            Scribe_Values.Look(ref TerrainType, "terrainType", Hilliness.Flat);
            Scribe_Values.Look(ref EnvironmentEvidence, "environmentEvidence", HistoricalTerrainEnvironmentEvidence.Unknown);
            Scribe_Values.Look(ref EnvironmentConfidence, "environmentConfidence", 0f);
        }
    }
}
