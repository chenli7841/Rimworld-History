using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalDomainDef : Def
    {
        public HistoricalStateDef parentPolity;
        public HistoricalRegionDef region;
        public string historicalDescription;
        public string sourceNotes;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            foreach (string error in HistoricalPoliticsValidator.ValidateDomainDef(this)) yield return error;
        }
    }
}
