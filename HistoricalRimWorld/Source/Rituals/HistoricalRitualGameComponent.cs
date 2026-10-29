using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Rituals
{
    public sealed class HistoricalRitualGameComponent : GameComponent
    {
        private string scenarioDefName;
        private bool initialized;
        private List<HistoricalRitualInstance> instances = new List<HistoricalRitualInstance>();
        public HistoricalRitualGameComponent(Game game) { }
        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public IEnumerable<HistoricalRitualInstance> Instances => instances ?? Enumerable.Empty<HistoricalRitualInstance>();
        public HistoricalRitualInstance Find(string id) => instances?.FirstOrDefault(item => item != null && item.RitualInstanceId == id);
        public bool Initialize(string scenarioId)
        {
            if (initialized) return string.Equals(scenarioDefName, scenarioId, StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(scenarioId)) return false;
            scenarioDefName = scenarioId;
            instances = instances ?? new List<HistoricalRitualInstance>();
            initialized = true;
            return true;
        }
        public void Add(HistoricalRitualInstance item)
        {
            if (item != null && Find(item.RitualInstanceId) == null) (instances ??= new List<HistoricalRitualInstance>()).Add(item);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalRitualScenarioDefName");
            Scribe_Values.Look(ref initialized, "historicalRitualInitialized", false);
            Scribe_Collections.Look(ref instances, "historicalRitualInstances", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) instances = (instances ?? new List<HistoricalRitualInstance>()).Where(item => item != null).GroupBy(item => item.RitualInstanceId).Select(group => group.First()).ToList();
        }
    }
}
