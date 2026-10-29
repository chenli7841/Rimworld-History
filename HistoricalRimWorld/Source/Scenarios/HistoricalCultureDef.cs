using System.Collections.Generic;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Rituals;
using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public sealed class HistoricalCultureDef : Def
    {
        public string historicalName;
        public string culturalDescription;
        public List<string> traditionIds;
        public List<HistoricalCultureDef> relatedCultures;
        public List<HistoricalCustomDef> customs;
        public List<HistoricalRitualDef> rituals;
        public string sourceNotes;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            foreach (string error in HistoricalDefValidator.ValidateCulture(this))
            {
                yield return error;
            }
        }
    }
}
