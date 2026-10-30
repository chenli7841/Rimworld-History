namespace HistoricalRimWorld.Geography.Scaling
{
    /// <summary>Separates map expansion from travel-speed balancing; the initial policy leaves vanilla speed unchanged.</summary>
    public sealed class HistoricalTravelScalePolicy
    {
        public double MapScaleFactor { get; }
        public double TravelSpeedMultiplier { get; }

        public HistoricalTravelScalePolicy(HistoricalMapScaleDef definition)
        {
            MapScaleFactor = definition?.scaleFactor ?? 1d;
            TravelSpeedMultiplier = definition?.travelSpeedMultiplier ?? 1d;
        }
    }
}
