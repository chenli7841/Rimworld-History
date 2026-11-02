using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Geography
{
    /// <summary>Scenario-specific runtime geographic data; mappings are derived and rebuilt for each WorldGrid.</summary>
    public sealed class HistoricalGeographicRegionWorldComponent : WorldComponent
    {
        private string scenarioId;
        private bool initialized;
        private List<HistoricalGeographicRegionData> regions = new List<HistoricalGeographicRegionData>();

        public string ScenarioId => scenarioId;
        public bool Initialized => initialized;
        public IEnumerable<HistoricalGeographicRegionData> Regions => regions ?? Enumerable.Empty<HistoricalGeographicRegionData>();

        public HistoricalGeographicRegionWorldComponent(RimWorldWorld world) : base(world) { }

        public void InitializeForScenario(string requestedScenarioId)
        {
            if (string.IsNullOrWhiteSpace(requestedScenarioId)) return;
            if (initialized)
            {
                if (scenarioId != requestedScenarioId)
                    Log.Warning("[Historical RimWorld] Geographic region data belongs to scenario " + scenarioId
                        + "; requested " + requestedScenarioId + " was ignored.");
                return;
            }
            scenarioId = requestedScenarioId;
            regions = DefDatabase<HistoricalGeographicRegionDef>.AllDefsListForReading
                .Where(def => def != null && (string.IsNullOrWhiteSpace(def.scenarioId)
                    || def.scenarioId == requestedScenarioId))
                .Select(HistoricalGeographicRegionData.FromDef).Where(item => item != null).ToList();
            HistoricalGeographicRegionOverrideFileStore.TryLoad(this, out string loadError);
            if (!string.IsNullOrWhiteSpace(loadError))
                Log.Warning("[Historical RimWorld] Geographic region overrides were not fully loaded: " + loadError);
            initialized = true;
        }

        public HistoricalGeographicRegionData GetRegion(string defName) =>
            regions?.FirstOrDefault(item => item != null && item.defName == defName);

        public void SetRegion(HistoricalGeographicRegionData region)
        {
            if (region == null || string.IsNullOrWhiteSpace(region.defName)) return;
            if (regions == null) regions = new List<HistoricalGeographicRegionData>();
            HistoricalGeographicRegionData existing = GetRegion(region.defName);
            if (existing != null) regions.Remove(existing);
            regions.Add(region.Clone());
            HistoricalGeographicRegionManager.Invalidate();
        }

        public bool RemoveRegion(string defName)
        {
            HistoricalGeographicRegionData existing = GetRegion(defName);
            if (existing == null) return false;
            regions.Remove(existing);
            HistoricalGeographicRegionManager.Invalidate();
            return true;
        }

        public bool TrySaveOverrides(out string path, out string error) =>
            HistoricalGeographicRegionOverrideFileStore.TrySave(this, out path, out error);

        internal void ReplaceRegions(IEnumerable<HistoricalGeographicRegionData> values)
        {
            regions = values?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<HistoricalGeographicRegionData>();
            HistoricalGeographicRegionManager.Invalidate();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioId, "historicalGeographicScenarioId");
            Scribe_Values.Look(ref initialized, "historicalGeographicRegionsInitialized", false);
            Scribe_Collections.Look(ref regions, "historicalGeographicRegions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                regions = regions ?? new List<HistoricalGeographicRegionData>();
                if (initialized && !string.IsNullOrWhiteSpace(scenarioId))
                {
                    HashSet<string> existing = new HashSet<string>(regions.Where(item => item != null)
                        .Select(item => item.defName), StringComparer.Ordinal);
                    foreach (HistoricalGeographicRegionDef def in DefDatabase<HistoricalGeographicRegionDef>.AllDefsListForReading
                        .Where(item => item != null && (string.IsNullOrWhiteSpace(item.scenarioId) || item.scenarioId == scenarioId)))
                    {
                        if (existing.Add(def.defName)) regions.Add(HistoricalGeographicRegionData.FromDef(def));
                    }
                }
                HistoricalGeographicRegionManager.Invalidate();
            }
        }
    }
}
