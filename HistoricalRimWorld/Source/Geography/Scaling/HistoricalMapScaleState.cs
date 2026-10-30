using System;
using Verse;

namespace HistoricalRimWorld.Geography.Scaling
{
    public sealed class HistoricalMapScaleState : IExposable
    {
        public string ScaleDefName;
        public double ScaleFactor = 1d;
        public double CenterLatitude;
        public double CenterLongitude;
        public int ProjectionVersion;
        public int WorldGenerationVersion;
        public double TravelSpeedMultiplier = 1d;

        public void Initialize(HistoricalMapScaleDef definition, int worldGenerationVersion)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            ScaleDefName = definition.defName;
            ScaleFactor = definition.scaleFactor;
            CenterLatitude = definition.centerLatitude;
            CenterLongitude = definition.centerLongitude;
            ProjectionVersion = HistoricalMapScaleTransformer.CurrentProjectionVersion;
            WorldGenerationVersion = worldGenerationVersion;
            TravelSpeedMultiplier = definition.travelSpeedMultiplier;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ScaleDefName, "scaleDefName");
            Scribe_Values.Look(ref ScaleFactor, "scaleFactor", 1d);
            Scribe_Values.Look(ref CenterLatitude, "centerLatitude");
            Scribe_Values.Look(ref CenterLongitude, "centerLongitude");
            Scribe_Values.Look(ref ProjectionVersion, "projectionVersion", 0);
            Scribe_Values.Look(ref WorldGenerationVersion, "worldGenerationVersion", 0);
            Scribe_Values.Look(ref TravelSpeedMultiplier, "travelSpeedMultiplier", 1d);
        }
    }
}
