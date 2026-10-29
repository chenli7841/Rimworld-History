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
        private HistoricalEventProvenance provenance;
        private HistoricalChronologyTradition chronologyTradition;
        private string sourceNotes;
        private HistoricalEventCategory category;
        private HistoricalEventScope scope;
        private bool visibleToPlayer;
        private int knownAstronomicalYear = int.MinValue;
        private int knownMonth = -1;
        private int knownDay = -1;
        private HistoricalDatePrecision knownPrecision;

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
        public HistoricalEventProvenance Provenance => provenance;
        public HistoricalChronologyTradition ChronologyTradition => chronologyTradition;
        public string SourceNotes => sourceNotes;
        public HistoricalEventCategory Category => category;
        public HistoricalEventScope Scope => scope;
        public bool VisibleToPlayer => visibleToPlayer;
        public bool HasKnownDate => knownAstronomicalYear != int.MinValue;
        public int? KnownAstronomicalYear => HasKnownDate ? knownAstronomicalYear : (int?)null;
        public int? KnownMonth => knownMonth < 0 ? (int?)null : knownMonth;
        public int? KnownDay => knownDay < 0 ? (int?)null : knownDay;
        public HistoricalDatePrecision KnownPrecision => knownPrecision;
        public long? KnownAbsoluteDay => HasKnownDate
            ? (long?)new HistoricalDate(knownAstronomicalYear, knownMonth < 0 ? 1 : knownMonth, knownDay < 0 ? 1 : knownDay).AbsoluteDay
            : null;
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
            Scribe_Values.Look(ref provenance, "eventProvenance", HistoricalEventProvenance.AttestedHistoricalEvent);
            Scribe_Values.Look(ref chronologyTradition, "chronologyTradition", HistoricalChronologyTradition.TraditionalReceivedChronology);
            Scribe_Values.Look(ref sourceNotes, "sourceNotes");
            Scribe_Values.Look(ref category, "category", HistoricalEventCategory.Other);
            Scribe_Values.Look(ref scope, "scope", HistoricalEventScope.World);
            Scribe_Values.Look(ref visibleToPlayer, "visibleToPlayer", false);
            Scribe_Values.Look(ref knownAstronomicalYear, "knownAstronomicalYear", int.MinValue);
            Scribe_Values.Look(ref knownMonth, "knownMonth", -1);
            Scribe_Values.Look(ref knownDay, "knownDay", -1);
            Scribe_Values.Look(ref knownPrecision, "knownPrecision", HistoricalDatePrecision.Year);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && visibleToPlayer && knownAstronomicalYear == int.MinValue)
            {
                // Backward compatibility: older saves had a single displayed date.
                knownAstronomicalYear = astronomicalYear;
                knownMonth = month;
                knownDay = day;
                knownPrecision = precision;
            }
        }

        internal void Initialize(string id, string eventId, HistoricalChronicleEntryType type,
            HistoricalDateReference date, string title, string description, string region, string polity,
            HistoricalEvidenceLevel evidence, string sources, HistoricalEventCategory eventCategory,
            HistoricalEventScope eventScope, bool visible,
            HistoricalEventProvenance eventProvenance = HistoricalEventProvenance.AttestedHistoricalEvent,
            HistoricalChronologyTradition tradition = HistoricalChronologyTradition.TraditionalReceivedChronology,
            HistoricalDateReference knownDate = null)
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
            provenance = eventProvenance;
            chronologyTradition = tradition;
            sourceNotes = sources;
            category = eventCategory;
            scope = eventScope;
            visibleToPlayer = visible;
            knownAstronomicalYear = knownDate?.AstronomicalYear ?? int.MinValue;
            knownMonth = knownDate?.Month ?? -1;
            knownDay = knownDate?.Day ?? -1;
            knownPrecision = knownDate?.Precision ?? HistoricalDatePrecision.Year;
        }

        internal void SetVisible(bool value, HistoricalDateReference knownDate = null)
        {
            visibleToPlayer = value;
            if (value && knownDate != null)
            {
                knownAstronomicalYear = knownDate.AstronomicalYear;
                knownMonth = knownDate.Month ?? -1;
                knownDay = knownDate.Day ?? -1;
                knownPrecision = knownDate.Precision;
            }
        }
    }
}
