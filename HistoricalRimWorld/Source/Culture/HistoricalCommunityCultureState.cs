using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Culture
{
    public sealed class HistoricalCommunityCultureState : IExposable
    {
        public string CommunityId;
        public string DominantCultureId;
        public List<HistoricalCulturalFamiliarity> CulturalPresence = new List<HistoricalCulturalFamiliarity>();
        public List<string> ActiveCustomIds = new List<string>();
        public IEnumerable<HistoricalCulturalFamiliarity> PresenceRecords => CulturalPresence ?? Enumerable.Empty<HistoricalCulturalFamiliarity>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref CommunityId, "communityId");
            Scribe_Values.Look(ref DominantCultureId, "dominantCultureId");
            Scribe_Collections.Look(ref CulturalPresence, "culturalPresence", LookMode.Deep);
            Scribe_Collections.Look(ref ActiveCustomIds, "activeCustomIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                CulturalPresence = CulturalPresence ?? new List<HistoricalCulturalFamiliarity>();
                ActiveCustomIds = ActiveCustomIds ?? new List<string>();
            }
        }
    }
}
