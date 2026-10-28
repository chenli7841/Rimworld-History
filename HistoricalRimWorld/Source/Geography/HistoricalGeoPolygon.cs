using System;
using System.Collections.Generic;
using System.Linq;

namespace HistoricalRimWorld.Geography
{
    public sealed class HistoricalGeoPolygon
    {
        public List<HistoricalGeoVertex> vertices = new List<HistoricalGeoVertex>();

        public IEnumerable<string> Validate()
        {
            List<HistoricalGeoCoordinate> points;
            string error;
            if (!TryGetValidatedVertices(out points, out error))
            {
                yield return error;
                yield break;
            }

            if (Math.Abs(SignedArea(points)) < 1e-10d)
            {
                yield return "Polygon area is zero.";
                yield break;
            }

            int count = points.Count;
            for (int first = 0; first < count; first++)
            {
                int firstNext = (first + 1) % count;
                for (int second = first + 1; second < count; second++)
                {
                    int secondNext = (second + 1) % count;
                    if (first == second || firstNext == second || secondNext == first) continue;
                    if (SegmentsIntersect(points[first], points[firstNext], points[second], points[secondNext]))
                    {
                        yield return "Polygon edges self-intersect.";
                        yield break;
                    }
                }
            }
        }

        public bool Contains(HistoricalGeoCoordinate point)
        {
            List<HistoricalGeoCoordinate> points;
            string error;
            if (!TryGetValidatedVertices(out points, out error) || Validate().Any()
                || Math.Abs(SignedArea(points)) < 1e-10d) return false;

            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                HistoricalGeoCoordinate a = points[j];
                HistoricalGeoCoordinate b = points[i];
                if (IsOnSegment(point, a, b)) return true;

                bool crossesLatitude = (a.Latitude > point.Latitude) != (b.Latitude > point.Latitude);
                if (crossesLatitude)
                {
                    double crossingLongitude = (b.Longitude - a.Longitude)
                        * (point.Latitude - a.Latitude) / (b.Latitude - a.Latitude) + a.Longitude;
                    if (point.Longitude < crossingLongitude) inside = !inside;
                }
            }

            return inside;
        }

        public double ApproximateArea =>
            TryGetValidatedVertices(out List<HistoricalGeoCoordinate> points, out _)
                ? Math.Abs(SignedArea(points))
                : double.PositiveInfinity;

        private bool TryGetValidatedVertices(out List<HistoricalGeoCoordinate> points, out string error)
        {
            points = new List<HistoricalGeoCoordinate>();
            error = null;
            if (vertices == null || vertices.Count < 3)
            {
                error = "A polygon requires at least three vertices.";
                return false;
            }

            try
            {
                points = vertices.Select(item => item == null
                    ? throw new ArgumentException("Polygon contains a null vertex.")
                    : item.ToCoordinate()).ToList();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is ArgumentOutOfRangeException)
            {
                error = exception.Message;
                return false;
            }

            if (points.Distinct().Count() < 3)
            {
                error = "A polygon requires at least three different vertices.";
                return false;
            }
            return true;
        }

        private static double SignedArea(IReadOnlyList<HistoricalGeoCoordinate> points)
        {
            double area = 0d;
            for (int i = 0; i < points.Count; i++)
            {
                HistoricalGeoCoordinate current = points[i];
                HistoricalGeoCoordinate next = points[(i + 1) % points.Count];
                area += current.Longitude * next.Latitude - next.Longitude * current.Latitude;
            }
            return area / 2d;
        }

        private static bool SegmentsIntersect(
            HistoricalGeoCoordinate a,
            HistoricalGeoCoordinate b,
            HistoricalGeoCoordinate c,
            HistoricalGeoCoordinate d)
        {
            double abC = Orientation(a, b, c);
            double abD = Orientation(a, b, d);
            double cdA = Orientation(c, d, a);
            double cdB = Orientation(c, d, b);
            const double epsilon = 1e-10d;

            if (((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon))
                && ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon))) return true;

            return Math.Abs(abC) <= epsilon && IsOnSegment(c, a, b)
                || Math.Abs(abD) <= epsilon && IsOnSegment(d, a, b)
                || Math.Abs(cdA) <= epsilon && IsOnSegment(a, c, d)
                || Math.Abs(cdB) <= epsilon && IsOnSegment(b, c, d);
        }

        private static double Orientation(HistoricalGeoCoordinate a, HistoricalGeoCoordinate b, HistoricalGeoCoordinate c) =>
            (b.Longitude - a.Longitude) * (c.Latitude - a.Latitude)
            - (b.Latitude - a.Latitude) * (c.Longitude - a.Longitude);

        private static bool IsOnSegment(HistoricalGeoCoordinate point, HistoricalGeoCoordinate start, HistoricalGeoCoordinate end)
        {
            const double epsilon = 1e-10d;
            return Math.Abs(Orientation(start, end, point)) <= epsilon
                && point.Longitude >= Math.Min(start.Longitude, end.Longitude) - epsilon
                && point.Longitude <= Math.Max(start.Longitude, end.Longitude) + epsilon
                && point.Latitude >= Math.Min(start.Latitude, end.Latitude) - epsilon
                && point.Latitude <= Math.Max(start.Latitude, end.Latitude) + epsilon;
        }
    }
}
