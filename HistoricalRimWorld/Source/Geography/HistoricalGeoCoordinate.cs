using System;

namespace HistoricalRimWorld.Geography
{
    public readonly struct HistoricalGeoCoordinate : IEquatable<HistoricalGeoCoordinate>
    {
        public double Latitude { get; }
        public double Longitude { get; }

        public HistoricalGeoCoordinate(double latitude, double longitude)
        {
            if (double.IsNaN(latitude) || double.IsInfinity(latitude) || latitude < -90d || latitude > 90d)
            {
                throw new ArgumentOutOfRangeException(nameof(latitude));
            }
            if (double.IsNaN(longitude) || double.IsInfinity(longitude) || longitude < -180d || longitude > 180d)
            {
                throw new ArgumentOutOfRangeException(nameof(longitude));
            }

            Latitude = latitude;
            Longitude = longitude;
        }

        public bool Equals(HistoricalGeoCoordinate other) =>
            Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);

        public override bool Equals(object obj) => obj is HistoricalGeoCoordinate other && Equals(other);

        public override int GetHashCode() => (Latitude.GetHashCode() * 397) ^ Longitude.GetHashCode();
    }
}
