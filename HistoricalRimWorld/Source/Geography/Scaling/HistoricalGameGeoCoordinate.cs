namespace HistoricalRimWorld.Geography.Scaling
{
    /// <summary>Coordinate on the generated game sphere; these are not real-world coordinates.</summary>
    public readonly struct HistoricalGameGeoCoordinate
    {
        public double Latitude { get; }
        public double Longitude { get; }

        public HistoricalGameGeoCoordinate(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
