using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Culture
{
    public sealed class HistoricalPersonCultureState : IExposable
    {
        public string PersonId;
        public string BirthCultureId;
        public string PrimaryCultureId;
        public List<HistoricalCulturalFamiliarity> Familiarities = new List<HistoricalCulturalFamiliarity>();
        public IEnumerable<HistoricalCulturalFamiliarity> FamiliarityRecords => Familiarities ?? Enumerable.Empty<HistoricalCulturalFamiliarity>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref PersonId, "personId");
            Scribe_Values.Look(ref BirthCultureId, "birthCultureId");
            Scribe_Values.Look(ref PrimaryCultureId, "primaryCultureId");
            Scribe_Collections.Look(ref Familiarities, "familiarities", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Familiarities = Familiarities ?? new List<HistoricalCulturalFamiliarity>();
        }
        public HistoricalCulturalFamiliarity FindFamiliarity(string cultureId) => Familiarities?.FirstOrDefault(item => item != null && item.CultureId == cultureId);
    }
}
