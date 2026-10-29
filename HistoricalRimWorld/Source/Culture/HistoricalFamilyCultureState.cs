using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Culture
{
    public sealed class HistoricalFamilyCultureState : IExposable
    {
        public string FamilyId;
        public string PrimaryCultureId;
        public List<string> PreservedTraditionIds = new List<string>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref FamilyId, "familyId");
            Scribe_Values.Look(ref PrimaryCultureId, "primaryCultureId");
            Scribe_Collections.Look(ref PreservedTraditionIds, "preservedTraditionIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) PreservedTraditionIds = PreservedTraditionIds ?? new List<string>();
        }
    }
}
