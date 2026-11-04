using System.Collections.Generic;
using System.Linq;
using System;
using HistoricalRimWorld.Characters;
using RimWorld;
using Verse;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.Scenarios.EasternZhou
{
    public sealed class HistoricalPlayableScenarioSetupDef : Def
    {
        public HistoricalScenarioDef scenario;
        public HistoricalChineseNamePoolDef startingNamePool;
        public FactionDef playerFactionDef;
        public HistoricalRegionDef defaultStartingRegion;
        public HistoricalStateDef defaultStartingPolity;
        public int startingPopulation;
        public int minimumStartingPawnAge = 18;
        public int maximumStartingPawnAge = 35;
        public List<string> startingTechnologyIds;
        public HistoricalStartingSuppliesDef startingSupplies;
        public string startingCommunityName;
        public int? playableEndAstronomicalYear;
        public string vanillaScenarioDefName;
        public string worldSeedOverride;

        public static ScenarioDef ResolveVanillaScenarioDef(Scenario runtimeScenario)
        {
            if (runtimeScenario == null || string.IsNullOrWhiteSpace(runtimeScenario.name)) return null;
            return DefDatabase<ScenarioDef>.AllDefsListForReading.FirstOrDefault(def => def?.scenario != null
                && string.Equals(def.scenario.name, runtimeScenario.name, StringComparison.Ordinal));
        }

        public static HistoricalPlayableScenarioSetupDef FindForVanillaScenario(Scenario runtimeScenario)
        {
            ScenarioDef scenarioDef = ResolveVanillaScenarioDef(runtimeScenario);
            if (scenarioDef == null) return null;
            return DefDatabase<HistoricalPlayableScenarioSetupDef>.AllDefsListForReading.FirstOrDefault(item => item != null
                && string.Equals(item.vanillaScenarioDefName, scenarioDef.defName, StringComparison.Ordinal));
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "Historical playable setup requires a HistoricalScenarioDef.";
            if (startingNamePool == null) yield return "Historical playable setup requires a Chinese name pool Def.";
            if (playerFactionDef == null) yield return "Historical playable setup requires the player faction Def for name generation.";
            if (defaultStartingRegion == null) yield return "Historical playable setup requires a starting region.";
            if (defaultStartingPolity == null) yield return "Historical playable setup requires a starting polity.";
            if (startingPopulation < 1) yield return "Starting population must be positive.";
            if (minimumStartingPawnAge < 18 || maximumStartingPawnAge < minimumStartingPawnAge)
                yield return "Starting pawn age range must include adults and have a maximum no lower than its minimum.";
            if (startingSupplies == null) yield return "Historical playable setup requires a supplies Def.";
            if (string.IsNullOrWhiteSpace(vanillaScenarioDefName)) yield return "A vanilla ScenarioDef defName is required to identify this playable scenario.";
            if (string.IsNullOrWhiteSpace(worldSeedOverride)) yield return "A fixed world seed is required for this playable scenario.";
        }
    }
}
