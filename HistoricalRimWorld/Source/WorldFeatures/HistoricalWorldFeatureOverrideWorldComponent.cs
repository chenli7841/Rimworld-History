using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldFeatures
{
    /// <summary>Stores manual feature-name choices; native WorldFeature names remain the save-game source of truth.</summary>
    public sealed class HistoricalWorldFeatureOverrideWorldComponent : WorldComponent
    {
        private string scenarioId;
        private List<HistoricalWorldFeatureNameRecord> manualOverrides = new List<HistoricalWorldFeatureNameRecord>();
        private List<HistoricalWorldFeatureOriginalName> originalNames = new List<HistoricalWorldFeatureOriginalName>();
        public string ScenarioId => scenarioId;
        public IEnumerable<HistoricalWorldFeatureNameRecord> ManualOverrides => manualOverrides ?? Enumerable.Empty<HistoricalWorldFeatureNameRecord>();

        public HistoricalWorldFeatureOverrideWorldComponent(RimWorldWorld world) : base(world) { }

        public void Initialize(string id)
        {
            if (!string.IsNullOrWhiteSpace(scenarioId) && scenarioId != id) return;
            scenarioId = id;
            manualOverrides = manualOverrides ?? new List<HistoricalWorldFeatureNameRecord>();
        }

        public HistoricalWorldFeatureNameRecord GetRecord(int featureId) =>
            manualOverrides?.FirstOrDefault(item => item != null && item.featureUniqueId == featureId);

        public void SetRecord(HistoricalWorldFeatureNameRecord record)
        {
            if (record == null) return;
            if (manualOverrides == null) manualOverrides = new List<HistoricalWorldFeatureNameRecord>();
            manualOverrides.RemoveAll(item => item != null && item.featureUniqueId == record.featureUniqueId);
            manualOverrides.Add(record.Clone());
        }

        public void RemoveRecord(int featureId) =>
            manualOverrides?.RemoveAll(item => item != null && item.featureUniqueId == featureId);

        public void SetOriginalName(int featureId, string name)
        {
            if (featureId < 0 || string.IsNullOrWhiteSpace(name)) return;
            if (originalNames == null) originalNames = new List<HistoricalWorldFeatureOriginalName>();
            HistoricalWorldFeatureOriginalName record = originalNames.FirstOrDefault(item => item != null && item.featureUniqueId == featureId);
            if (record == null) originalNames.Add(new HistoricalWorldFeatureOriginalName { featureUniqueId = featureId, name = name });
            else if (string.IsNullOrWhiteSpace(record.name)) record.name = name;
        }

        public string GetOriginalName(int featureId) =>
            originalNames?.FirstOrDefault(item => item != null && item.featureUniqueId == featureId)?.name;

        public bool TrySaveManualOverrides(out string path, out string error) =>
            HistoricalWorldFeatureOverrideFileStore.TrySave(this, out path, out error);

        internal void ReplaceManualOverrides(IEnumerable<HistoricalWorldFeatureNameRecord> records)
        {
            manualOverrides = records?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<HistoricalWorldFeatureNameRecord>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioId, "historicalWorldFeatureScenarioId");
            Scribe_Collections.Look(ref manualOverrides, "historicalWorldFeatureManualOverrides", LookMode.Deep);
            Scribe_Collections.Look(ref originalNames, "historicalWorldFeatureOriginalNames", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                manualOverrides = manualOverrides ?? new List<HistoricalWorldFeatureNameRecord>();
                originalNames = originalNames ?? new List<HistoricalWorldFeatureOriginalName>();
            }
        }
    }

    public sealed class HistoricalWorldFeatureNameRecord : IExposable
    {
        public int featureUniqueId = -1;
        public string featureType;
        public string historicalName;
        public string originalName;
        public double targetLongitude;
        public double targetLatitude;
        public double searchRadiusKm = 100d;

        public HistoricalWorldFeatureNameRecord Clone() => (HistoricalWorldFeatureNameRecord)MemberwiseClone();

        public void ExposeData()
        {
            Scribe_Values.Look(ref featureUniqueId, "featureUniqueId", -1);
            Scribe_Values.Look(ref featureType, "featureType");
            Scribe_Values.Look(ref historicalName, "historicalName");
            Scribe_Values.Look(ref originalName, "originalName");
            Scribe_Values.Look(ref targetLongitude, "targetLongitude");
            Scribe_Values.Look(ref targetLatitude, "targetLatitude");
            Scribe_Values.Look(ref searchRadiusKm, "searchRadiusKm", 100d);
        }
    }

    public sealed class HistoricalWorldFeatureOriginalName : IExposable
    {
        public int featureUniqueId = -1;
        public string name;
        public void ExposeData()
        {
            Scribe_Values.Look(ref featureUniqueId, "featureUniqueId", -1);
            Scribe_Values.Look(ref name, "name");
        }
    }
}
