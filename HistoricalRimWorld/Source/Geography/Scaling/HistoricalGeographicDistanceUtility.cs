using System;
using HistoricalRimWorld.Geography;

namespace HistoricalRimWorld.Geography.Scaling
{
    public static class HistoricalGeographicDistanceUtility
    {
        public static double GetGreatCircleDistanceKm(HistoricalGeoCoordinate first, HistoricalGeoCoordinate second)
        {
            const double radians = Math.PI / 180d;
            double latitudeDelta = (second.Latitude - first.Latitude) * radians;
            double longitudeDelta = NormalizeLongitude(second.Longitude - first.Longitude) * radians;
            double firstLatitude = first.Latitude * radians;
            double secondLatitude = second.Latitude * radians;
            double sinLatitude = Math.Sin(latitudeDelta / 2d);
            double sinLongitude = Math.Sin(longitudeDelta / 2d);
            double haversine = sinLatitude * sinLatitude
                + Math.Cos(firstLatitude) * Math.Cos(secondLatitude) * sinLongitude * sinLongitude;
            double centralAngle = 2d * Math.Asin(Math.Sqrt(Math.Max(0d, Math.Min(1d, haversine))));
            return centralAngle * AzimuthalEquidistantProjection.EarthRadiusKm;
        }

        public static double GetGameWorldDistanceDegrees(HistoricalGameGeoCoordinate first,
            HistoricalGameGeoCoordinate second)
        {
            const double radians = Math.PI / 180d;
            double firstLatitude = first.Latitude * radians;
            double secondLatitude = second.Latitude * radians;
            double latitudeDelta = (second.Latitude - first.Latitude) * radians;
            double longitudeDelta = NormalizeLongitude(second.Longitude - first.Longitude) * radians;
            double sinLatitude = Math.Sin(latitudeDelta / 2d);
            double sinLongitude = Math.Sin(longitudeDelta / 2d);
            double haversine = sinLatitude * sinLatitude
                + Math.Cos(firstLatitude) * Math.Cos(secondLatitude) * sinLongitude * sinLongitude;
            return 2d * Math.Asin(Math.Sqrt(Math.Max(0d, Math.Min(1d, haversine)))) / radians;
        }

        private static double NormalizeLongitude(double value)
        {
            while (value > 180d) value -= 360d;
            while (value < -180d) value += 360d;
            return value;
        }
    }
}
