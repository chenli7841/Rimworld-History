using System.Collections.Generic;
using Verse;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.Scenarios.EasternZhou
{
    public sealed class HistoricalPlayableScenarioSetupDef : Def
    {
        public HistoricalScenarioDef scenario;
        public HistoricalRegionDef defaultStartingRegion;
        public HistoricalStateDef defaultStartingPolity;
        public int startingPopulation;
        public List<string> startingTechnologyIds;
        public HistoricalStartingSuppliesDef startingSupplies;
        public string startingCommunityName;
        public int? playableEndAstronomicalYear;
        public string vanillaScenarioName;
        public string worldSeedOverride;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "Historical playable setup requires a HistoricalScenarioDef.";
            if (defaultStartingRegion == null) yield return "Historical playable setup requires a starting region.";
            if (defaultStartingPolity == null) yield return "Historical playable setup requires a starting polity.";
            if (startingPopulation < 1) yield return "Starting population must be positive.";
            if (startingSupplies == null) yield return "Historical playable setup requires a supplies Def.";
            if (string.IsNullOrWhiteSpace(vanillaScenarioName)) yield return "A vanilla Scenario name is required to identify this playable scenario.";
            if (string.IsNullOrWhiteSpace(worldSeedOverride)) yield return "A fixed world seed is required for this playable scenario.";
        }
    }
}
