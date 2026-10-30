using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using Verse;

namespace HistoricalRimWorld.WorldGeneration
{
    public static class HistoricalWorldGenerationValidator
    {
        public static bool Validate(HistoricalWorldGenerationDef def, out string error)
        {
            error = null;
            if (def == null) { error = "Historical world generation Def is missing."; return false; }
            string configError = string.Join(" ", def.ConfigErrors());
            if (!string.IsNullOrEmpty(configError)) { error = configError; return false; }
            if (def.mode != HistoricalWorldGenerationMode.ScenarioBounded)
            { error = "Only ScenarioBounded mode is implemented in this release."; return false; }
            return true;
        }
    }

    public static class HistoricalCoverageCalculator
    {
        private const double TileSafetyMarginDegrees = 3.0;
        private static HistoricalWorldGenerationDef cachedDef;
        private static bool hasCachedResult;
        private static bool cachedSuccess;
        private static HistoricalGeoBounds cachedBounds;
        private static HistoricalSphericalCoverage cachedCoverage;
        private static float cachedPlanetCoverage;
        private static string cachedError;

        public static bool TryCalculate(HistoricalWorldGenerationDef def,
            out HistoricalGeoBounds bounds, out HistoricalSphericalCoverage coverage,
            out float planetCoverage, out string error)
        {
            if (hasCachedResult && ReferenceEquals(def, cachedDef))
            {
                bounds = cachedBounds;
                coverage = cachedCoverage;
                planetCoverage = cachedPlanetCoverage;
                error = cachedError;
                return cachedSuccess;
            }
            cachedSuccess = Calculate(def, out cachedBounds, out cachedCoverage,
                out cachedPlanetCoverage, out cachedError);
            cachedDef = def;
            hasCachedResult = true;
            bounds = cachedBounds;
            coverage = cachedCoverage;
            planetCoverage = cachedPlanetCoverage;
            error = cachedError;
            return cachedSuccess;
        }

        private static bool Calculate(HistoricalWorldGenerationDef def,
            out HistoricalGeoBounds bounds, out HistoricalSphericalCoverage coverage,
            out float planetCoverage, out string error)
        {
            bounds = default(HistoricalGeoBounds);
            coverage = default(HistoricalSphericalCoverage);
            planetCoverage = 0f;
            if (!HistoricalWorldGenerationValidator.Validate(def, out error)) return false;

            double minLat = Math.Max(-90.0, def.minLatitude - def.latitudeMargin);
            double maxLat = Math.Min(90.0, def.maxLatitude + def.latitudeMargin);
            double lonWidth = def.maxLongitude - def.minLongitude;
            while (lonWidth < 0.0) lonWidth += 360.0;
            lonWidth += 2.0 * def.longitudeMargin;
            if (lonWidth >= 360.0)
            { error = "Longitude margin expands the scenario bounds to the entire globe."; return false; }
            double minLon = def.minLongitude - def.longitudeMargin;
            double maxLon = minLon + lonWidth;
            bounds = new HistoricalGeoBounds(minLat, maxLat, NormalizeLongitude(minLon), NormalizeLongitude(maxLon));

            // For a dateline-crossing range, unwrap the upper longitude so samples remain contiguous.
            double sampleMinLon = minLon;
            double sampleMaxLon = maxLon;
            if (sampleMaxLon < sampleMinLon) sampleMaxLon += 360.0;
            List<Tuple<double, double>> historicalSamples = SampleBounds(minLat, maxLat, sampleMinLon, sampleMaxLon, 1.0);
            HistoricalMapScaleTransformer transformer = new HistoricalMapScaleTransformer(def.mapScale);
            List<Tuple<double, double>> samples = new List<Tuple<double, double>>(historicalSamples.Count);
            try
            {
                foreach (Tuple<double, double> sample in historicalSamples)
                {
                    HistoricalGameGeoCoordinate gameCoordinate = transformer.ToGameCoordinate(
                        new HistoricalGeoCoordinate(sample.Item2, sample.Item1));
                    samples.Add(Tuple.Create(gameCoordinate.Longitude, gameCoordinate.Latitude));
                }
            }
            catch (Exception exception)
            {
                error = "The scaled scenario bounds exceed the usable Azimuthal Equidistant projection area: " + exception.Message;
                return false;
            }
            double centerLatitudeHistorical = (minLat + maxLat) / 2d;
            double centerLongitudeHistorical = (sampleMinLon + sampleMaxLon) / 2d;
            HistoricalGameGeoCoordinate gameCenter = transformer.ToGameCoordinate(
                new HistoricalGeoCoordinate(centerLatitudeHistorical, NormalizeLongitude(centerLongitudeHistorical)));
            double centerLat = gameCenter.Latitude;
            double centerLon = gameCenter.Longitude;
            double radius = samples.Max(p => AngularDistance(centerLon, centerLat, p.Item1, p.Item2)) + TileSafetyMarginDegrees;
            if (radius > 180.0)
            { error = "Expanded scenario bounds cannot fit within a spherical cap."; return false; }

            coverage = new HistoricalSphericalCoverage(centerLat, centerLon, radius);
            // RimWorld 1.6 sets surfaceViewAngle to planetCoverage * 180 degrees.
            // This parameter accepts a float, so the scenario can select a bounded
            // value directly instead of rounding up to the UI's 30/50/100% presets.
            planetCoverage = (float)(radius / 180.0);
            if (planetCoverage <= 0f || planetCoverage > 1f)
            { error = "Calculated planet coverage is outside the supported 0..1 range."; return false; }
            if (def.mapScale.scaleFactor > 5d)
                Log.Warning("[Historical RimWorld] Map scale " + def.mapScale.scaleFactor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + "x exceeds the recommended 5x limit and may create a much larger WorldGrid.");
            Log.Message("[Historical RimWorld] Historical bounds were projected at "
                + def.mapScale.scaleFactor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                + "x using " + def.mapScale.projectionType + "; game coverage center is "
                + centerLon.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°, "
                + centerLat.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°, radius "
                + radius.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°.");
            error = null;
            return true;
        }

        public static List<Tuple<double, double>> SampleBounds(double minLat, double maxLat,
            double minLonUnwrapped, double maxLonUnwrapped, double step)
        {
            List<Tuple<double, double>> result = new List<Tuple<double, double>>();
            int latSteps = Math.Max(1, (int)Math.Ceiling((maxLat - minLat) / step));
            int lonSteps = Math.Max(1, (int)Math.Ceiling((maxLonUnwrapped - minLonUnwrapped) / step));
            for (int latIndex = 0; latIndex <= latSteps; latIndex++)
            {
                double lat = minLat + (maxLat - minLat) * latIndex / latSteps;
                for (int lonIndex = 0; lonIndex <= lonSteps; lonIndex++)
                {
                    double lon = minLonUnwrapped + (maxLonUnwrapped - minLonUnwrapped) * lonIndex / lonSteps;
                    result.Add(Tuple.Create(NormalizeLongitude(lon), lat));
                }
            }
            return result;
        }

        public static double AngularDistance(double lonA, double latA, double lonB, double latB)
        {
            const double radians = Math.PI / 180.0;
            double aLat = latA * radians, bLat = latB * radians;
            double deltaLat = (latB - latA) * radians;
            double deltaLon = NormalizeLongitude(lonB - lonA) * radians;
            double sinLat = Math.Sin(deltaLat / 2.0), sinLon = Math.Sin(deltaLon / 2.0);
            double haversine = sinLat * sinLat + Math.Cos(aLat) * Math.Cos(bLat) * sinLon * sinLon;
            return 2.0 * Math.Asin(Math.Sqrt(Math.Max(0.0, Math.Min(1.0, haversine)))) / radians;
        }

        public static UnityEngine.Vector3 ToUnitVector(double longitude, double latitude)
        {
            const double radians = Math.PI / 180.0;
            double lat = latitude * radians, lon = longitude * radians, cosLat = Math.Cos(lat);
            return new UnityEngine.Vector3((float)(Math.Sin(lon) * cosLat),
                (float)Math.Sin(lat), (float)(-Math.Cos(lon) * cosLat));
        }

        private static double NormalizeLongitude(double longitude)
        {
            while (longitude > 180.0) longitude -= 360.0;
            while (longitude < -180.0) longitude += 360.0;
            return longitude;
        }
    }
}
