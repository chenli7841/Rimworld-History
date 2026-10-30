using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Geography;

namespace HistoricalRimWorld.Geography.Scaling
{
    /// <summary>Projects region outlines, sampling long edges before projection to limit curvature error.</summary>
    public static class HistoricalRegionProjectionUtility
    {
        public static IReadOnlyList<HistoricalGameGeoCoordinate> ProjectBoundary(
            HistoricalGeoPolygon polygon, HistoricalMapScaleTransformer transformer,
            double maximumSegmentLengthKm = 100d)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (transformer == null) throw new ArgumentNullException(nameof(transformer));
            if (double.IsNaN(maximumSegmentLengthKm) || double.IsInfinity(maximumSegmentLengthKm)
                || maximumSegmentLengthKm <= 0d)
                throw new ArgumentOutOfRangeException(nameof(maximumSegmentLengthKm));

            string error = string.Join(" ", polygon.Validate());
            if (!string.IsNullOrEmpty(error)) throw new ArgumentException("Invalid historical region polygon: " + error, nameof(polygon));

            List<HistoricalGeoCoordinate> vertices = polygon.vertices.Select(item => item.ToCoordinate()).ToList();
            List<HistoricalGameGeoCoordinate> result = new List<HistoricalGameGeoCoordinate>();
            for (int index = 0; index < vertices.Count; index++)
            {
                HistoricalGeoCoordinate start = vertices[index];
                HistoricalGeoCoordinate end = vertices[(index + 1) % vertices.Count];
                double distance = HistoricalGeographicDistanceUtility.GetGreatCircleDistanceKm(start, end);
                int segments = Math.Max(1, (int)Math.Ceiling(distance / maximumSegmentLengthKm));
                for (int segment = 0; segment < segments; segment++)
                {
                    HistoricalGeoCoordinate sample = SphericalInterpolate(start, end, (double)segment / segments);
                    result.Add(transformer.ToGameCoordinate(sample));
                }
            }
            return result.AsReadOnly();
        }

        private static HistoricalGeoCoordinate SphericalInterpolate(
            HistoricalGeoCoordinate start, HistoricalGeoCoordinate end, double fraction)
        {
            const double radians = Math.PI / 180d;
            double lat1 = start.Latitude * radians;
            double lon1 = start.Longitude * radians;
            double lat2 = end.Latitude * radians;
            double lon2 = end.Longitude * radians;
            double[] first = UnitVector(lat1, lon1);
            double[] second = UnitVector(lat2, lon2);
            double dot = Clamp(first[0] * second[0] + first[1] * second[1] + first[2] * second[2], -1d, 1d);
            double angle = Math.Acos(dot);
            if (angle < 1e-12d) return start;
            if (Math.PI - angle < 1e-9d)
                throw new ArgumentException("A region edge cannot use antipodal vertices.");

            double denominator = Math.Sin(angle);
            double firstWeight = Math.Sin((1d - fraction) * angle) / denominator;
            double secondWeight = Math.Sin(fraction * angle) / denominator;
            double x = firstWeight * first[0] + secondWeight * second[0];
            double y = firstWeight * first[1] + secondWeight * second[1];
            double z = firstWeight * first[2] + secondWeight * second[2];
            double latitude = Math.Atan2(y, Math.Sqrt(x * x + z * z)) / radians;
            double longitude = Math.Atan2(z, x) / radians;
            if (longitude > 180d) longitude -= 360d;
            if (longitude < -180d) longitude += 360d;
            return new HistoricalGeoCoordinate(latitude, longitude);
        }

        private static double[] UnitVector(double latitude, double longitude)
        {
            double cosLatitude = Math.Cos(latitude);
            return new[] { cosLatitude * Math.Cos(longitude), Math.Sin(latitude), cosLatitude * Math.Sin(longitude) };
        }

        private static double Clamp(double value, double minimum, double maximum) =>
            Math.Max(minimum, Math.Min(maximum, value));
    }
}
