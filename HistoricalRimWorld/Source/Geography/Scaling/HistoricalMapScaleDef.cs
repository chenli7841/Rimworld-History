using System;
using System.Collections.Generic;
using Verse;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.Geography.Scaling
{
    public sealed class HistoricalMapScaleDef : Def
    {
        public HistoricalScenarioDef scenario;
        public double scaleFactor = 1d;
        public double centerLatitude;
        public double centerLongitude;
        public string projectionType = "AzimuthalEquidistant";
        public bool preserveHistoricalCoordinates = true;
        public bool scaleRegionBoundaries = true;
        public bool scaleTravelDistance = true;
        public double travelSpeedMultiplier = 1d;

        public IEnumerable<string> ValidateSettings()
        {
            if (scenario == null) yield return "scenario is required.";
            if (double.IsNaN(scaleFactor) || double.IsInfinity(scaleFactor) || scaleFactor < 1d)
                yield return "scaleFactor must be finite and at least 1.0.";
            if (double.IsNaN(centerLatitude) || double.IsInfinity(centerLatitude)
                || centerLatitude < -90d || centerLatitude > 90d)
                yield return "centerLatitude must be within -90..90 degrees.";
            if (double.IsNaN(centerLongitude) || double.IsInfinity(centerLongitude)
                || centerLongitude < -180d || centerLongitude > 180d)
                yield return "centerLongitude must be within -180..180 degrees.";
            if (!string.Equals(projectionType, "AzimuthalEquidistant", StringComparison.Ordinal))
                yield return "Only AzimuthalEquidistant projectionType is currently implemented.";
            if (double.IsNaN(travelSpeedMultiplier) || double.IsInfinity(travelSpeedMultiplier)
                || travelSpeedMultiplier <= 0d)
                yield return "travelSpeedMultiplier must be finite and greater than zero.";
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            foreach (string error in ValidateSettings()) yield return error;
        }
    }
}
