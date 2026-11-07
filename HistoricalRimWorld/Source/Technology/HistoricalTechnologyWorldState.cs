using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Technology
{
    /// <summary>Persistent knowledge held by one faction. StableFactionId is a saved faction load ID or a historical polity DefName.</summary>
    public sealed class HistoricalFactionTechnologyState : IExposable
    {
        private string stableFactionId;
        private string historicalStateId;
        private string factionDefName;
        private bool isPlayerFaction;
        private List<HistoricalFactionTechRecord> technologies = new List<HistoricalFactionTechRecord>();

        public string StableFactionId => stableFactionId;
        public string HistoricalStateId => historicalStateId;
        public string FactionDefName => factionDefName;
        public bool IsPlayerFaction => isPlayerFaction;
        public IEnumerable<HistoricalFactionTechRecord> Technologies => technologies ?? Enumerable.Empty<HistoricalFactionTechRecord>();
        public HistoricalFactionTechRecord Find(string researchDefName) => technologies?.FirstOrDefault(item => item != null && item.ResearchDefName == researchDefName);

        internal void Initialize(string id, string stateId, string factionDef, bool player, IEnumerable<HistoricalFactionTechRecord> records)
        {
            stableFactionId = id;
            historicalStateId = stateId;
            factionDefName = factionDef;
            isPlayerFaction = player;
            technologies = records == null ? new List<HistoricalFactionTechRecord>() : records.ToList();
        }

        internal HistoricalFactionTechRecord GetOrAdd(string researchDefName)
        {
            if (string.IsNullOrWhiteSpace(researchDefName)) return null;
            HistoricalFactionTechRecord record = Find(researchDefName);
            if (record != null) return record;
            record = new HistoricalFactionTechRecord();
            record.Initialize(researchDefName, FactionTechKnowledgeLevel.Unknown, int.MinValue, int.MinValue, "");
            if (technologies == null) technologies = new List<HistoricalFactionTechRecord>();
            technologies.Add(record);
            return record;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref stableFactionId, "stableFactionId");
            Scribe_Values.Look(ref historicalStateId, "historicalStateId");
            Scribe_Values.Look(ref factionDefName, "factionDefName");
            Scribe_Values.Look(ref isPlayerFaction, "isPlayerFaction", false);
            Scribe_Collections.Look(ref technologies, "technologies", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                technologies = technologies ?? new List<HistoricalFactionTechRecord>();
                technologies.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.ResearchDefName));
            }
        }
    }

    public sealed class HistoricalFactionTechRecord : IExposable
    {
        public string ResearchDefName;
        public FactionTechKnowledgeLevel Level;
        public int FirstKnownAstronomicalYear = int.MinValue;
        public int MasteredAstronomicalYear = int.MinValue;
        public string AcquisitionSource;
        public int LastDiffusionGameTick = -1;

        internal void Initialize(string research, FactionTechKnowledgeLevel level, int firstKnownYear, int masteredYear, string source)
        {
            ResearchDefName = research;
            Level = level;
            FirstKnownAstronomicalYear = firstKnownYear;
            MasteredAstronomicalYear = masteredYear;
            AcquisitionSource = source;
        }

        public void SetLevel(FactionTechKnowledgeLevel level, int year, string source)
        {
            if (Level < level)
            {
                Level = level;
                if (FirstKnownAstronomicalYear == int.MinValue) FirstKnownAstronomicalYear = year;
                if (level == FactionTechKnowledgeLevel.Mastered) MasteredAstronomicalYear = year;
                AcquisitionSource = source;
            }
            else if (Level == level && level == FactionTechKnowledgeLevel.Mastered && MasteredAstronomicalYear == int.MinValue)
            {
                MasteredAstronomicalYear = year;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ResearchDefName, "researchDefName");
            Scribe_Values.Look(ref Level, "level", FactionTechKnowledgeLevel.Unknown);
            Scribe_Values.Look(ref FirstKnownAstronomicalYear, "firstKnownAstronomicalYear", int.MinValue);
            Scribe_Values.Look(ref MasteredAstronomicalYear, "masteredAstronomicalYear", int.MinValue);
            Scribe_Values.Look(ref AcquisitionSource, "acquisitionSource");
            Scribe_Values.Look(ref LastDiffusionGameTick, "lastDiffusionGameTick", -1);
        }
    }

    public sealed class PlayerHistoricalTechAwarenessState : IExposable
    {
        public string ResearchDefName;
        public PlayerHistoricalTechAwareness Awareness;
        public int LearnedAstronomicalYear = int.MinValue;
        public bool EmergenceNoticeSent;
        public bool SourceNoticeSent;
        public long LastNoticeAbsoluteDay = long.MinValue;
        public List<string> KnownSourceFactionIds = new List<string>();

        public void Raise(PlayerHistoricalTechAwareness value, int year, string sourceFactionId = null)
        {
            if (Awareness < value)
            {
                Awareness = value;
                if (LearnedAstronomicalYear == int.MinValue) LearnedAstronomicalYear = year;
            }
            if (!string.IsNullOrWhiteSpace(sourceFactionId))
            {
                if (KnownSourceFactionIds == null) KnownSourceFactionIds = new List<string>();
                if (!KnownSourceFactionIds.Contains(sourceFactionId)) KnownSourceFactionIds.Add(sourceFactionId);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ResearchDefName, "researchDefName");
            Scribe_Values.Look(ref Awareness, "awareness", PlayerHistoricalTechAwareness.Unknown);
            Scribe_Values.Look(ref LearnedAstronomicalYear, "learnedAstronomicalYear", int.MinValue);
            Scribe_Values.Look(ref EmergenceNoticeSent, "emergenceNoticeSent", false);
            Scribe_Values.Look(ref SourceNoticeSent, "sourceNoticeSent", false);
            Scribe_Values.Look(ref LastNoticeAbsoluteDay, "lastNoticeAbsoluteDay", long.MinValue);
            Scribe_Collections.Look(ref KnownSourceFactionIds, "knownSourceFactionIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) KnownSourceFactionIds = KnownSourceFactionIds ?? new List<string>();
        }
    }

    /// <summary>Scheduling metadata only. The current world status is derived from faction knowledge records.</summary>
    public sealed class HistoricalTechnologyScheduleState : IExposable
    {
        public string ResearchDefName;
        public int LastEmergenceAttemptAstronomicalYear = int.MinValue;
        public long LastDiffusionAbsoluteDay = long.MinValue;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ResearchDefName, "researchDefName");
            Scribe_Values.Look(ref LastEmergenceAttemptAstronomicalYear, "lastEmergenceAttemptAstronomicalYear", int.MinValue);
            Scribe_Values.Look(ref LastDiffusionAbsoluteDay, "lastDiffusionAbsoluteDay", long.MinValue);
        }
    }
}
