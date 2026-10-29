using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public sealed class HistoricalTechnologySetupDef : Def
    {
        public HistoricalScenarioDef scenario;
        public string communityId;
        public List<HistoricalTechnologyInitialState> technologies;
    }

    public sealed class HistoricalTechnologyInitialState
    {
        public HistoricalTechnologyDef technology;
        public HistoricalTechnologyAvailability availability;
        public HistoricalTechnologyAdoption adoption;
    }
}
