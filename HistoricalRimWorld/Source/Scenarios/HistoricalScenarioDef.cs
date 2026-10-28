using System;
using System.Collections.Generic;
using HistoricalRimWorld.Calendar;
using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public sealed class HistoricalScenarioDef : Def
    {
        public int startAstronomicalYear;
        public int startMonth = 1;
        public int startDay = 1;
        public int? endAstronomicalYear;
        public string historicalPeriod;
        public List<HistoricalStateDef> states;
        public List<HistoricalRegionDef> regions;
        public List<HistoricalCultureDef> cultures;
        public List<HistoricalSettlementDef> settlements;
        public HistoricalRegionDef defaultStartingRegion;
        public HistoricalStateDef defaultStartingState;
        public string sourceNotes;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;

        public HistoricalDate GetStartDate()
        {
            return new HistoricalDate(startAstronomicalYear, startMonth, startDay);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            foreach (string error in HistoricalDefValidator.ValidateScenario(this))
            {
                yield return error;
            }
        }
    }
}
