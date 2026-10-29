using System.Collections.Generic;
using RimWorld;
using Verse;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.Factions
{
    public sealed class HistoricalFactionBindingDef : Def
    {
        public HistoricalScenarioDef scenario;
        public HistoricalStateDef historicalState;
        public FactionDef rimWorldFactionDef;
        public HistoricalRegionDef prototypeRegion;
        public bool generateAtWorldCreation = true;
        public int desiredFactionCount = 1;
        public int desiredSettlementCount = 1;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (historicalState == null) yield return "historicalState is required.";
            if (rimWorldFactionDef == null) yield return "rimWorldFactionDef is required.";
            if (prototypeRegion == null) yield return "prototypeRegion is required.";
            if (desiredFactionCount < 0) yield return "desiredFactionCount cannot be negative.";
            if (desiredSettlementCount < 0) yield return "desiredSettlementCount cannot be negative.";
            if (scenario != null && historicalState != null
                && (scenario.states == null || !scenario.states.Contains(historicalState)))
                yield return "historicalState must be included by scenario.";
            if (scenario != null && prototypeRegion != null
                && (scenario.regions == null || !scenario.regions.Contains(prototypeRegion)))
                yield return "prototypeRegion must be included by scenario.";
        }
    }
}
