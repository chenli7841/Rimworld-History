using HistoricalRimWorld.Calendar;
using Verse;

namespace HistoricalRimWorld.Genealogy
{
    public sealed class HistoricalKinshipRelation : IExposable
    {
        private string personIdA;
        private string personIdB;
        private HistoricalKinshipType type;
        private int startYear;
        private int startMonth = -1;
        private int startDay = -1;
        private int endYear = int.MinValue;
        private int endMonth = -1;
        private int endDay = -1;

        public string PersonIdA => personIdA;
        public string PersonIdB => personIdB;
        public HistoricalKinshipType Type => type;
        public HistoricalDate StartDate => new HistoricalDate(startYear, startMonth, startDay);
        public HistoricalDate? EndDate => endYear == int.MinValue ? (HistoricalDate?)null : new HistoricalDate(endYear, endMonth, endDay);
        public bool IsActive => endYear == int.MinValue;

        public void ExposeData()
        {
            Scribe_Values.Look(ref personIdA, "personIdA");
            Scribe_Values.Look(ref personIdB, "personIdB");
            Scribe_Values.Look(ref type, "type", HistoricalKinshipType.BiologicalParent);
            Scribe_Values.Look(ref startYear, "startAstronomicalYear");
            Scribe_Values.Look(ref startMonth, "startMonth", 1);
            Scribe_Values.Look(ref startDay, "startDay", 1);
            Scribe_Values.Look(ref endYear, "endAstronomicalYear", int.MinValue);
            Scribe_Values.Look(ref endMonth, "endMonth", -1);
            Scribe_Values.Look(ref endDay, "endDay", -1);
        }

        internal void Initialize(string a, string b, HistoricalKinshipType relationType, HistoricalDate start)
        {
            personIdA = a;
            personIdB = b;
            type = relationType;
            startYear = start.AstronomicalYear;
            startMonth = start.Month;
            startDay = start.Day;
        }

        internal void Close(HistoricalDate end)
        {
            if (!IsActive) return;
            endYear = end.AstronomicalYear;
            endMonth = end.Month;
            endDay = end.Day;
        }
    }
}
