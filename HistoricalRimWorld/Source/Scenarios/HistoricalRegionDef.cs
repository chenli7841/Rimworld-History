using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public sealed class HistoricalRegionDef : Def
    {
        public string historicalName;
        public string geographicDescription;
        public List<HistoricalRegionDef> adjacentRegions;
        public List<HistoricalCultureDef> associatedCultures;
        public string historicalDescription;
        public string sourceNotes;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            foreach (string error in HistoricalDefValidator.ValidateRegion(this))
            {
                yield return error;
            }
        }
    }
}
