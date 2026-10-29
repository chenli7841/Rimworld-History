using System.Collections.Generic;
using Verse;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.WorldGeneration
{
    public sealed class HistoricalSettlementPlacementDef : Def
    {
        public HistoricalScenarioDef scenario;
        public HistoricalStateDef polity;
        public HistoricalRegionDef region;
        public HistoricalSettlementDef settlement;
        public bool required = true;
        public int minimumDistanceFromOtherSettlements = 3;
        public float prototypeLongitudeOffset;
        public float prototypeLatitudeOffset;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (polity == null) yield return "polity is required.";
            if (region == null) yield return "region is required.";
            if (minimumDistanceFromOtherSettlements < 0) yield return "minimumDistanceFromOtherSettlements cannot be negative.";
            if (scenario != null && polity != null && (scenario.states == null || !scenario.states.Contains(polity)))
                yield return "polity must be included by scenario.";
            if (scenario != null && region != null && (scenario.regions == null || !scenario.regions.Contains(region)))
                yield return "region must be included by scenario.";
            if (settlement != null && (settlement.initialState == null || settlement.initialState != polity))
                yield return "settlement.initialState must match polity.";
        }
    }
}
