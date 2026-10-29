using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Relics
{
    public sealed class HistoricalRelicGameComponent : GameComponent
    {
        private string scenarioDefName;
        private bool initialized;
        private int dataVersion = 1;
        private List<HistoricalRelicRecord> relics = new List<HistoricalRelicRecord>();
        private List<HistoricalRelicEventRecord> events = new List<HistoricalRelicEventRecord>();
        public HistoricalRelicGameComponent(Game game) { }
        public bool Initialized => initialized;
        public IEnumerable<HistoricalRelicRecord> Relics => relics ?? Enumerable.Empty<HistoricalRelicRecord>();
        public IEnumerable<HistoricalRelicEventRecord> Events => events ?? Enumerable.Empty<HistoricalRelicEventRecord>();
        public HistoricalRelicRecord Find(string id) => relics?.FirstOrDefault(item => item != null && item.RelicId == id);
        public bool Initialize(string id) { if (initialized) return string.Equals(scenarioDefName, id, StringComparison.Ordinal); scenarioDefName = id; initialized = !string.IsNullOrWhiteSpace(id); return initialized; }
        public void Add(HistoricalRelicRecord record) { if (record != null && Find(record.RelicId) == null) (relics ??= new List<HistoricalRelicRecord>()).Add(record); }
        public void Add(HistoricalRelicEventRecord record) { if (record != null) (events ??= new List<HistoricalRelicEventRecord>()).Add(record); }
        public override void ExposeData()
        {
            base.ExposeData(); Scribe_Values.Look(ref scenarioDefName, "historicalRelicScenarioDefName");
            Scribe_Values.Look(ref initialized, "historicalRelicInitialized", false); Scribe_Values.Look(ref dataVersion, "historicalRelicDataVersion", 1);
            Scribe_Collections.Look(ref relics, "historicalRelicRecords", LookMode.Deep); Scribe_Collections.Look(ref events, "historicalRelicEvents", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                relics = (relics ?? new List<HistoricalRelicRecord>()).Where(item => item != null).GroupBy(item => item.RelicId).Select(group => group.First()).ToList();
                events = (events ?? new List<HistoricalRelicEventRecord>()).Where(item => item != null).ToList();
                dataVersion = Math.Max(1, dataVersion);
            }
        }
    }
}
