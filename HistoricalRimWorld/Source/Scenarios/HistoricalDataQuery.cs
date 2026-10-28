using System.Collections.Generic;
using System.Linq;

namespace HistoricalRimWorld.Scenarios
{
    public static class HistoricalDataQuery
    {
        public static IEnumerable<HistoricalStateDef> GetStates(HistoricalScenarioDef scenario)
        {
            return scenario?.states ?? Enumerable.Empty<HistoricalStateDef>();
        }

        public static IEnumerable<HistoricalRegionDef> GetRegions(HistoricalScenarioDef scenario)
        {
            return scenario?.regions ?? Enumerable.Empty<HistoricalRegionDef>();
        }

        public static IEnumerable<HistoricalCultureDef> GetCultures(HistoricalScenarioDef scenario)
        {
            return scenario?.cultures ?? Enumerable.Empty<HistoricalCultureDef>();
        }

        public static IEnumerable<HistoricalSettlementDef> GetSettlements(HistoricalScenarioDef scenario)
        {
            return scenario?.settlements ?? Enumerable.Empty<HistoricalSettlementDef>();
        }
    }
}
