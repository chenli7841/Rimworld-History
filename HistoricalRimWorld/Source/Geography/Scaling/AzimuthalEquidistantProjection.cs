using System;
using HistoricalRimWorld.Geography;

namespace HistoricalRimWorld.Geography.Scaling
{
    /// <summary>Azimuthal equidistant projection centered on the scenario origin.</summary>
    public sealed class AzimuthalEquidistantProjection : IHistoricalGeoProjection
    {
        public const double EarthRadiusKm = 6371.0088d;
        private const double DegreesToRadians = Math.PI / 180d;
        private const double RadiansToDegrees = 180d / Math.PI;
        private readonly double centerLatitude;
        private readonly double centerLongitude;
        private readonly double sinCenterLatitude;
        private readonly double cosCenterLatitude;

        public AzimuthalEquidistantProjection(double centerLatitude, double centerLongitude)
        {
            if (double.IsNaN(centerLatitude) || double.IsInfinity(centerLatitude)
                || centerLatitude < -90d || centerLatitude > 90d)
                throw new ArgumentOutOfRangeException(nameof(centerLatitude));
            if (double.IsNaN(centerLongitude) || double.IsInfinity(centerLongitude)
                || centerLongitude < -180d || centerLongitude > 180d)
                throw new ArgumentOutOfRangeException(nameof(centerLongitude));

            this.centerLatitude = centerLatitude * DegreesToRadians;
            this.centerLongitude = centerLongitude * DegreesToRadians;
            sinCenterLatitude = Math.Sin(this.centerLatitude);
            cosCenterLatitude = Math.Cos(this.centerLatitude);
        }

        public HistoricalProjectedCoordinate Project(HistoricalGeoCoordinate coordinate)
        {
            double latitude = coordinate.Latitude * DegreesToRadians;
            double deltaLongitude = NormalizeRadians((coordinate.Longitude * DegreesToRadians) - centerLongitude);
            double sinLatitude = Math.Sin(latitude);
            double cosLatitude = Math.Cos(latitude);
            double cosCentralAngle = Clamp(sinCenterLatitude * sinLatitude
                + cosCenterLatitude * cosLatitude * Math.Cos(deltaLongitude), -1d, 1d);
            double centralAngle = Math.Acos(cosCentralAngle);
            if (centralAngle < 1e-12d) return new HistoricalProjectedCoordinate(0d, 0d);
            if (Math.PI - centralAngle < 1e-9d)
                throw new ArgumentOutOfRangeException(nameof(coordinate), "Azimuthal equidistant projection is undefined at the antipode.");

            double scale = centralAngle / Math.Sin(centralAngle);
            double x = EarthRadiusKm * scale * cosLatitude * Math.Sin(deltaLongitude);
            double y = EarthRadiusKm * scale * (cosCenterLatitude * sinLatitude
                - sinCenterLatitude * cosLatitude * Math.Cos(deltaLongitude));
            return new HistoricalProjectedCoordinate(x, y);
        }

        public HistoricalGeoCoordinate Unproject(HistoricalProjectedCoordinate coordinate)
        {
            double x = coordinate.X / EarthRadiusKm;
            double y = coordinate.Y / EarthRadiusKm;
            double centralAngle = Math.Sqrt(x * x + y * y);
            if (centralAngle < 1e-12d) return new HistoricalGeoCoordinate(
                centerLatitude * RadiansToDegrees, centerLongitude * RadiansToDegrees);
            if (centralAngle >= Math.PI - 1e-9d)
                throw new ArgumentOutOfRangeException(nameof(coordinate), "Unprojected point is at or beyond the projection antipode.");

            double sinCentralAngle = Math.Sin(centralAngle);
            double cosCentralAngle = Math.Cos(centralAngle);
            double latitude = Math.Asin(Clamp(cosCentralAngle * sinCenterLatitude
                + y * sinCentralAngle * cosCenterLatitude / centralAngle, -1d, 1d));
            double longitude = centerLongitude + Math.Atan2(
                x * sinCentralAngle,
                centralAngle * cosCenterLatitude * cosCentralAngle
                    - y * sinCenterLatitude * sinCentralAngle);
            return new HistoricalGeoCoordinate(latitude * RadiansToDegrees,
                NormalizeDegrees(longitude * RadiansToDegrees));
        }

        private static double NormalizeRadians(double value)
        {
            while (value > Math.PI) value -= 2d * Math.PI;
            while (value < -Math.PI) value += 2d * Math.PI;
            return value;
        }

        private static double NormalizeDegrees(double value)
        {
            while (value > 180d) value -= 360d;
            while (value < -180d) value += 360d;
            return value;
        }

        private static double Clamp(double value, double minimum, double maximum) =>
            Math.Max(minimum, Math.Min(maximum, value));
    }
}
