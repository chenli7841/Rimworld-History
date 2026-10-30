namespace HistoricalRimWorld.Geography.Scaling
{
    /// <summary>Local tangent-plane coordinate in kilometers, east-positive X and north-positive Y.</summary>
    public readonly struct HistoricalProjectedCoordinate
    {
        public double X { get; }
        public double Y { get; }

        public HistoricalProjectedCoordinate(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
