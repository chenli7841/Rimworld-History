using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public sealed class HistoricalStateDef : Def
    {
        public string historicalName;
        public int? foundedAstronomicalYear;
        public int? dissolvedAstronomicalYear;
        public HistoricalCultureDef primaryCulture;
        public HistoricalStateDef nominalOverlord;
        public HistoricalStateCategory stateCategory;
        public string historicalDescription;
        public string sourceNotes;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            foreach (string error in HistoricalDefValidator.ValidateState(this))
            {
                yield return error;
            }
        }
    }
}
