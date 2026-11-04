using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    /// <summary>Saveable per-world color overrides for native RimWorld WorldFeatures.</summary>
    public sealed class HistoricalWorldTerrainColorWorldComponent : WorldComponent
    {
        private List<HistoricalWorldFeatureColorOverride> featureOverrides =
            new List<HistoricalWorldFeatureColorOverride>();

        public HistoricalWorldTerrainColorWorldComponent(RimWorldWorld world) : base(world) { }

        public IEnumerable<HistoricalWorldFeatureColorOverride> FeatureOverrides =>
            featureOverrides ?? Enumerable.Empty<HistoricalWorldFeatureColorOverride>();

        public HistoricalWorldFeatureColorOverride GetFeatureOverride(int uniqueId) =>
            featureOverrides?.FirstOrDefault(item => item != null && item.FeatureUniqueId == uniqueId);

        public void SetFeatureOverride(int uniqueId, string featureDefName, string featureName,
            double historicalLongitude, double historicalLatitude, string colorHex, int priority)
        {
            if (featureOverrides == null) featureOverrides = new List<HistoricalWorldFeatureColorOverride>();
            HistoricalWorldFeatureColorOverride record = GetFeatureOverride(uniqueId);
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                if (record != null) featureOverrides.Remove(record);
                return;
            }
            if (record == null)
            {
                record = new HistoricalWorldFeatureColorOverride();
                featureOverrides.Add(record);
            }
            record.FeatureUniqueId = uniqueId;
            record.FeatureDefName = featureDefName;
            record.FeatureName = featureName;
            record.HistoricalLongitude = historicalLongitude;
            record.HistoricalLatitude = historicalLatitude;
            record.ColorHex = colorHex;
            record.Priority = priority;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref featureOverrides, "historicalWorldFeatureColorOverrides", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                featureOverrides = featureOverrides ?? new List<HistoricalWorldFeatureColorOverride>();
                featureOverrides.RemoveAll(item => item == null || item.FeatureUniqueId < 0
                    || string.IsNullOrWhiteSpace(item.FeatureDefName) || string.IsNullOrWhiteSpace(item.ColorHex));
            }
        }
    }

    public sealed class HistoricalWorldFeatureColorOverride : IExposable
    {
        public int FeatureUniqueId = -1;
        public string FeatureDefName;
        public string FeatureName;
        public double HistoricalLongitude;
        public double HistoricalLatitude;
        public string ColorHex;
        public int Priority;

        public void ExposeData()
        {
            Scribe_Values.Look(ref FeatureUniqueId, "featureUniqueId", -1);
            Scribe_Values.Look(ref FeatureDefName, "featureDefName");
            Scribe_Values.Look(ref FeatureName, "featureName");
            Scribe_Values.Look(ref HistoricalLongitude, "historicalLongitude");
            Scribe_Values.Look(ref HistoricalLatitude, "historicalLatitude");
            Scribe_Values.Look(ref ColorHex, "colorHex");
            Scribe_Values.Look(ref Priority, "priority", 0);
        }
    }
}
