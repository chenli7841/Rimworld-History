using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using Verse;
using System;
using System.Linq;

namespace HistoricalRimWorld.Relics
{
    public sealed class HistoricalRelicDef : Def
    {
        public HistoricalRelicCategory category;
        public ThingDef baseThingDef;
        public List<HistoricalCultureDef> associatedCultures;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;
        public string historicalDescription;
        public string sourceNotes;
        public override System.Collections.Generic.IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (baseThingDef == null) yield return "baseThingDef is required.";
            if (!Enum.IsDefined(typeof(HistoricalRelicCategory), category)) yield return "category is invalid.";
            if (associatedCultures != null && associatedCultures.Where(item => item != null).Select(item => item.defName).Distinct().Count() != associatedCultures.Count)
                yield return "associatedCultures contains a null or duplicate reference.";
        }
    }
}
