using System;
using System.Collections.Generic;
using Verse;
using HistoricalRimWorld.Geography.Scaling;

namespace HistoricalRimWorld.WorldGeneration
{
    public enum HistoricalWorldGenerationMode
    {
        Vanilla,
        ScenarioBounded,
        FullEarth
    }

    public sealed class HistoricalWorldGenerationDef : Def
    {
        public HistoricalRimWorld.Scenarios.HistoricalScenarioDef scenario;
        public HistoricalMapScaleDef mapScale;
        public HistoricalWorldGenerationMode mode = HistoricalWorldGenerationMode.ScenarioBounded;
        public float minLatitude;
        public float maxLatitude;
        public float minLongitude;
        public float maxLongitude;
        public float latitudeMargin;
        public float longitudeMargin;
        public bool requireCompleteCoverage = true;
        public bool useRealEarthGeography;
        public float maximumTileCenterErrorDegrees = 5f;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (mapScale == null) yield return "mapScale is required.";
            else if (scenario != null && mapScale.scenario != scenario)
                yield return "mapScale must belong to the same scenario.";
            if (!Enum.IsDefined(typeof(HistoricalWorldGenerationMode), mode)) yield return "mode is invalid.";
            if (minLatitude < -90f || minLatitude > 90f || maxLatitude < -90f || maxLatitude > 90f)
                yield return "Latitude bounds must be within -90..90 degrees.";
            if (minLatitude >= maxLatitude) yield return "minLatitude must be less than maxLatitude.";
            if (minLongitude < -180f || minLongitude > 180f || maxLongitude < -180f || maxLongitude > 180f)
                yield return "Longitude bounds must be within -180..180 degrees.";
            if (latitudeMargin < 0f || longitudeMargin < 0f) yield return "Geographic margins cannot be negative.";
            if (maximumTileCenterErrorDegrees <= 0f || maximumTileCenterErrorDegrees > 180f)
                yield return "maximumTileCenterErrorDegrees must be greater than zero and at most 180.";
            if (useRealEarthGeography)
                yield return "Real Earth terrain mapping is unavailable; do not enable useRealEarthGeography until a validated dataset is installed.";
        }
    }

    public readonly struct HistoricalGeoBounds
    {
        public readonly double MinLatitude;
        public readonly double MaxLatitude;
        public readonly double MinLongitude;
        public readonly double MaxLongitude;

        public HistoricalGeoBounds(double minLatitude, double maxLatitude, double minLongitude, double maxLongitude)
        {
            MinLatitude = minLatitude;
            MaxLatitude = maxLatitude;
            MinLongitude = minLongitude;
            MaxLongitude = maxLongitude;
        }
    }

    public readonly struct HistoricalSphericalCoverage
    {
        public readonly double CenterLatitude;
        public readonly double CenterLongitude;
        public readonly double AngularRadiusDegrees;

        public HistoricalSphericalCoverage(double centerLatitude, double centerLongitude, double angularRadiusDegrees)
        {
            CenterLatitude = centerLatitude;
            CenterLongitude = centerLongitude;
            AngularRadiusDegrees = angularRadiusDegrees;
        }

        public UnityEngine.Vector3 CenterVector
        {
            get
            {
                double lat = CenterLatitude * Math.PI / 180.0;
                double lon = CenterLongitude * Math.PI / 180.0;
                double cosLat = Math.Cos(lat);
                return new UnityEngine.Vector3(
                    (float)(Math.Sin(lon) * cosLat),
                    (float)Math.Sin(lat),
                    (float)(-Math.Cos(lon) * cosLat));
            }
        }
    }
}
