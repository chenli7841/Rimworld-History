using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Culture
{
    public sealed class HistoricalCustomDef : Def
    {
        public List<HistoricalCultureDef> associatedCultures;
        public HistoricalCustomCategory category;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;
        public string historicalDescription;
        public string sourceNotes;
        public ThoughtDef thought;
    }
}
