using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Culture
{
    public sealed class HistoricalCultureSetupDef : Def
    {
        public HistoricalScenarioDef scenario;
        public string communityId;
        public HistoricalCultureDef dominantCulture;
        public List<HistoricalCulturePresenceSetup> culturalPresence;
        public List<string> activeCustomIds;
    }

    public sealed class HistoricalCulturePresenceSetup
    {
        public HistoricalCultureDef culture;
        public float familiarity = 0.5f;
    }
}
