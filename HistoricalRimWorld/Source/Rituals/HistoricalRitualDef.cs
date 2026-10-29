using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;
using System;

namespace HistoricalRimWorld.Rituals
{
    public sealed class HistoricalRitualDef : Def
    {
        public HistoricalRitualCategory category;
        public List<HistoricalCultureDef> associatedCultures;
        public int minimumParticipants = 1;
        public int durationTicks;
        public ThingDef requiredOffering;
        public ThoughtDef completionThought;
        public string preservedTraditionId;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;
        public string historicalDescription;
        public string sourceNotes;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (!Enum.IsDefined(typeof(HistoricalRitualCategory), category)) yield return "category is invalid.";
            if (minimumParticipants < 1) yield return "minimumParticipants must be at least one.";
            if (durationTicks < 0) yield return "durationTicks cannot be negative.";
            if (associatedCultures != null && associatedCultures.Where(item => item != null).Select(item => item.defName).Distinct().Count() != associatedCultures.Count)
                yield return "associatedCultures contains a null or duplicate reference.";
        }
    }
}
