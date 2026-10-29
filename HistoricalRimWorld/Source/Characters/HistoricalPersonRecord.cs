using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Lifespan;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public sealed class HistoricalPersonRecord : IExposable
    {
        private string personId;
        private string familyId;
        private string displayName;
        private HistoricalPersonOrigin origin;
        private int birthAstronomicalYear;
        private int birthMonth;
        private int birthDay;
        private HistoricalBirthDateSource birthDateSource;
        private bool isAlive;
        private int deathAstronomicalYear = int.MinValue;
        private int deathMonth = -1;
        private int deathDay = -1;
        private HistoricalDeathCause lastDeathCause;
        private int deathCount;
        private int resurrectionCount;
        private string birthRegionId;
        private string currentRegionId;
        private string birthPolityId;
        private string currentAllegiancePolityId;

        public string PersonId => personId;
        public string FamilyId => familyId;
        public string DisplayName => displayName;
        public HistoricalPersonOrigin Origin => origin;
        public HistoricalDate BirthDate => new HistoricalDate(birthAstronomicalYear, birthMonth, birthDay);
        public HistoricalBirthDateSource BirthDateSource => birthDateSource;
        public bool IsAlive => isAlive;
        public HistoricalDate? DeathDate => deathAstronomicalYear == int.MinValue
            ? (HistoricalDate?)null
            : new HistoricalDate(deathAstronomicalYear, deathMonth, deathDay);
        public HistoricalDeathCause LastDeathCause => lastDeathCause;
        public int DeathCount => deathCount;
        public int ResurrectionCount => resurrectionCount;
        public string BirthRegionId => birthRegionId;
        public string CurrentRegionId => currentRegionId;
        public string BirthPolityId => birthPolityId;
        public string CurrentAllegiancePolityId => currentAllegiancePolityId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref personId, "personId");
            Scribe_Values.Look(ref familyId, "familyId");
            Scribe_Values.Look(ref displayName, "displayName");
            Scribe_Values.Look(ref origin, "origin", HistoricalPersonOrigin.Generated);
            Scribe_Values.Look(ref birthAstronomicalYear, "birthAstronomicalYear");
            Scribe_Values.Look(ref birthMonth, "birthMonth", 1);
            Scribe_Values.Look(ref birthDay, "birthDay", 1);
            Scribe_Values.Look(ref birthDateSource, "birthDateSource", HistoricalBirthDateSource.EstimatedFromInitialAge);
            Scribe_Values.Look(ref isAlive, "isAlive", true);
            Scribe_Values.Look(ref deathAstronomicalYear, "deathAstronomicalYear", int.MinValue);
            Scribe_Values.Look(ref deathMonth, "deathMonth", -1);
            Scribe_Values.Look(ref deathDay, "deathDay", -1);
            Scribe_Values.Look(ref lastDeathCause, "lastDeathCause", HistoricalDeathCause.Unknown);
            Scribe_Values.Look(ref deathCount, "deathCount", 0);
            Scribe_Values.Look(ref resurrectionCount, "resurrectionCount", 0);
            Scribe_Values.Look(ref birthRegionId, "birthRegionId");
            Scribe_Values.Look(ref currentRegionId, "currentRegionId");
            Scribe_Values.Look(ref birthPolityId, "birthPolityId");
            Scribe_Values.Look(ref currentAllegiancePolityId, "currentAllegiancePolityId");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && !IsValid())
                Log.Warning("[Historical RimWorld] A malformed historical person record was retained but will not be indexed.");
        }

        internal void Initialize(string id, string family, string name, HistoricalPersonOrigin personOrigin,
            HistoricalDate birth, HistoricalBirthDateSource dateSource, string regionId, string polityId)
        {
            personId = id;
            familyId = family;
            displayName = name;
            origin = personOrigin;
            birthAstronomicalYear = birth.AstronomicalYear;
            birthMonth = birth.Month;
            birthDay = birth.Day;
            birthDateSource = dateSource;
            isAlive = true;
            birthRegionId = regionId;
            currentRegionId = regionId;
            birthPolityId = polityId;
            currentAllegiancePolityId = polityId;
        }

        internal void SetFamilyId(string value) => familyId = value;
        internal void SetDisplayName(string value) => displayName = value;
        internal void MarkDeath(HistoricalDate date, HistoricalDeathCause cause)
        {
            deathAstronomicalYear = date.AstronomicalYear;
            deathMonth = date.Month;
            deathDay = date.Day;
            lastDeathCause = cause;
            deathCount++;
            isAlive = false;
        }
        internal void MarkResurrected() { resurrectionCount++; isAlive = true; }

        internal bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(personId) || string.IsNullOrWhiteSpace(displayName)) return false;
            try
            {
                new HistoricalDate(birthAstronomicalYear, birthMonth, birthDay);
                return deathAstronomicalYear == int.MinValue
                    || (deathMonth >= 1 && deathMonth <= HistoricalCalendarConstants.MonthsPerYear
                        && deathDay >= 1 && deathDay <= HistoricalCalendarConstants.DaysPerMonth);
            }
            catch { return false; }
        }
    }
}
