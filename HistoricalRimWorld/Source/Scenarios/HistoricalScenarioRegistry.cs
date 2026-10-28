using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public static class HistoricalScenarioRegistry
    {
        public static IEnumerable<HistoricalScenarioDef> AllScenarios =>
            DefDatabase<HistoricalScenarioDef>.AllDefsListForReading;

        public static HistoricalScenarioDef GetById(string defName)
        {
            return DefDatabase<HistoricalScenarioDef>.GetNamed(defName);
        }

        public static bool TryGetById(string defName, out HistoricalScenarioDef scenario)
        {
            scenario = string.IsNullOrEmpty(defName)
                ? null
                : DefDatabase<HistoricalScenarioDef>.GetNamedSilentFail(defName);
            return scenario != null;
        }
    }
}
