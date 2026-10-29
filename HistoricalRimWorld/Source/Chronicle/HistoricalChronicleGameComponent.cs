using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Chronicle
{
    public sealed class HistoricalChronicleGameComponent : GameComponent
    {
        private string scenarioDefName;
        private bool initialized;
        private List<HistoricalChronicleEntry> entries = new List<HistoricalChronicleEntry>();

        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public IEnumerable<HistoricalChronicleEntry> Entries => entries ?? Enumerable.Empty<HistoricalChronicleEntry>();

        public bool InitializeForScenario(string scenarioId)
        {
            if (initialized) return string.Equals(scenarioDefName, scenarioId, StringComparison.Ordinal);
            scenarioDefName = scenarioId;
            entries = entries ?? new List<HistoricalChronicleEntry>();
            initialized = true;
            return true;
        }

        public bool AddEntry(HistoricalChronicleEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.EntryId)) return false;
            if (entries == null) entries = new List<HistoricalChronicleEntry>();
            if (entries.Any(item => item != null && item.EntryId == entry.EntryId)) return true;
            entries.Add(entry);
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalChronicleScenarioDefName");
            Scribe_Values.Look(ref initialized, "historicalChronicleInitialized", false);
            Scribe_Collections.Look(ref entries, "historicalChronicleEntries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) entries = entries ?? new List<HistoricalChronicleEntry>();
        }
    }
}
