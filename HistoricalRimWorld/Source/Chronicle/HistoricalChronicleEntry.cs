using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Chronicle
{
    public sealed class HistoricalChronicleEntry : IExposable
    {
        private string entryId;
        private string sourceEventId;
        private HistoricalChronicleEntryType entryType;
        private int astronomicalYear;
        private int month = -1;
        private int day = -1;
        private HistoricalDatePrecision precision;
        private string titleKey;
        private string descriptionKey;
        private string regionId;
        private string polityId;
        private HistoricalEvidenceLevel evidenceLevel;
        private string sourceNotes;
        private HistoricalEventCategory category;
        private HistoricalEventScope scope;
        private bool visibleToPlayer;

        public string EntryId => entryId;
        public string SourceEventId => sourceEventId;
        public HistoricalChronicleEntryType EntryType => entryType;
        public int AstronomicalYear => astronomicalYear;
        public int? Month => month < 0 ? (int?)null : month;
        public int? Day => day < 0 ? (int?)null : day;
        public HistoricalDatePrecision Precision => precision;
        public string TitleKey => titleKey;
        public string DescriptionKey => descriptionKey;
        public string RegionId => regionId;
        public string PolityId => polityId;
        public HistoricalEvidenceLevel EvidenceLevel => evidenceLevel;
        public string SourceNotes => sourceNotes;
        public HistoricalEventCategory Category => category;
        public HistoricalEventScope Scope => scope;
        public bool VisibleToPlayer => visibleToPlayer;
        public long AbsoluteDay => new HistoricalDate(astronomicalYear, month < 0 ? 1 : month, day < 0 ? 1 : day).AbsoluteDay;

        public void ExposeData()
        {
            Scribe_Values.Look(ref entryId, "entryId");
            Scribe_Values.Look(ref sourceEventId, "sourceEventId");
            Scribe_Values.Look(ref entryType, "entryType", HistoricalChronicleEntryType.AttestedHistory);
            Scribe_Values.Look(ref astronomicalYear, "astronomicalYear");
            Scribe_Values.Look(ref month, "month", -1);
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref precision, "precision", HistoricalDatePrecision.Year);
            Scribe_Values.Look(ref titleKey, "titleKey");
            Scribe_Values.Look(ref descriptionKey, "descriptionKey");
            Scribe_Values.Look(ref regionId, "regionId");
            Scribe_Values.Look(ref polityId, "polityId");
            Scribe_Values.Look(ref evidenceLevel, "evidenceLevel", HistoricalEvidenceLevel.Probable);
            Scribe_Values.Look(ref sourceNotes, "sourceNotes");
            Scribe_Values.Look(ref category, "category", HistoricalEventCategory.Other);
            Scribe_Values.Look(ref scope, "scope", HistoricalEventScope.World);
            Scribe_Values.Look(ref visibleToPlayer, "visibleToPlayer", false);
        }

        internal void Initialize(string id, string eventId, HistoricalChronicleEntryType type,
            HistoricalDateReference date, string title, string description, string region, string polity,
            HistoricalEvidenceLevel evidence, string sources, HistoricalEventCategory eventCategory,
            HistoricalEventScope eventScope, bool visible)
        {
            entryId = id;
            sourceEventId = eventId;
            entryType = type;
            astronomicalYear = date.AstronomicalYear;
            month = date.Month ?? -1;
            day = date.Day ?? -1;
            precision = date.Precision;
            titleKey = title;
            descriptionKey = description;
            regionId = region;
            polityId = polity;
            evidenceLevel = evidence;
            sourceNotes = sources;
            category = eventCategory;
            scope = eventScope;
            visibleToPlayer = visible;
        }

        internal void SetVisible(bool value) => visibleToPlayer = value;
    }
}
