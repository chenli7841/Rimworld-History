using System;
using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.WorldFeatures
{
    /// <summary>Scenario-specific name for an existing RimWorld WorldFeature.</summary>
    public sealed class HistoricalWorldFeatureOverrideDef : Def
    {
        public string scenarioId;
        public string historicalName;
        // RimWorld 1.6 FeatureDef.defName (for example MountainRange or Lake).
        public string featureType;
        public double targetLongitude;
        public double targetLatitude;
        public double searchRadiusKm = 35d;
        public float minimumOverlapRatio;
        public int priority = 50;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(scenarioId)) yield return "scenarioId is required.";
            if (string.IsNullOrWhiteSpace(historicalName)) yield return "historicalName is required.";
            if (string.IsNullOrWhiteSpace(featureType)) yield return "featureType must name a native RimWorld FeatureDef.";
            if (double.IsNaN(targetLongitude) || double.IsInfinity(targetLongitude)
                || targetLongitude < -180d || targetLongitude > 180d) yield return "targetLongitude must be within -180..180.";
            if (double.IsNaN(targetLatitude) || double.IsInfinity(targetLatitude)
                || targetLatitude < -90d || targetLatitude > 90d) yield return "targetLatitude must be within -90..90.";
            if (double.IsNaN(searchRadiusKm) || double.IsInfinity(searchRadiusKm)
                || searchRadiusKm <= 0d || searchRadiusKm > 20000d) yield return "searchRadiusKm must be within 0..20000.";
            if (minimumOverlapRatio < 0f || minimumOverlapRatio > 1f) yield return "minimumOverlapRatio must be within 0..1.";
        }
    }
}
