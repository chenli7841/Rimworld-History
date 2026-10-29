using HistoricalRimWorld.Calendar;
using Verse;

namespace HistoricalRimWorld.Lifespan
{
    public sealed class HistoricalNaturalLifespanRecord : IExposable
    {
        private string personId;
        private int assignedLifespanYears;
        private int naturalDeathAstronomicalYear;
        private int naturalDeathMonth;
        private int naturalDeathDay;
        private bool naturalDeathProcessed;

        public string PersonId => personId;
        public int AssignedLifespanYears => assignedLifespanYears;
        public HistoricalDate NaturalDeathDate => new HistoricalDate(naturalDeathAstronomicalYear, naturalDeathMonth, naturalDeathDay);
        public bool NaturalDeathProcessed => naturalDeathProcessed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref personId, "personId");
            Scribe_Values.Look(ref assignedLifespanYears, "assignedLifespanYears");
            Scribe_Values.Look(ref naturalDeathAstronomicalYear, "naturalDeathAstronomicalYear");
            Scribe_Values.Look(ref naturalDeathMonth, "naturalDeathMonth", 1);
            Scribe_Values.Look(ref naturalDeathDay, "naturalDeathDay", 1);
            Scribe_Values.Look(ref naturalDeathProcessed, "naturalDeathProcessed", false);
        }

        internal void Initialize(string id, int lifespan, HistoricalDate deathDate)
        {
            personId = id;
            assignedLifespanYears = lifespan;
            naturalDeathAstronomicalYear = deathDate.AstronomicalYear;
            naturalDeathMonth = deathDate.Month;
            naturalDeathDay = deathDate.Day;
        }

        internal void SetNaturalDeathProcessed(bool value) => naturalDeathProcessed = value;

        internal bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(personId) || assignedLifespanYears < 1) return false;
            try { new HistoricalDate(naturalDeathAstronomicalYear, naturalDeathMonth, naturalDeathDay); return true; }
            catch { return false; }
        }
    }
}
