using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public sealed class HistoricalSettlementDef : Def
    {
        public string historicalName;
        public HistoricalRegionDef region;
        public HistoricalStateDef initialState;
        public HistoricalSettlementCategory settlementCategory;
        public double? latitude;
        public double? longitude;
        public int? foundedAstronomicalYear;
        public int? abandonedAstronomicalYear;
        public string historicalDescription;
        public string sourceNotes;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            foreach (string error in HistoricalDefValidator.ValidateSettlement(this))
            {
                yield return error;
            }
        }
    }
}
