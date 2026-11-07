using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public sealed class HistoricalTechnologySetupDef : Def
    {
        public HistoricalScenarioDef scenario;
        public string communityId;
        public List<HistoricalTechnologyInitialState> technologies;
        public int diffusionIntervalDays = 30;
        public float neighborDistanceWeight = 1f;
        public float tradeConnectionWeight = 1.35f;
        public float remoteConnectionWeight = 0.18f;
    }

    public sealed class HistoricalTechnologyInitialState
    {
        public HistoricalTechnologyDef technology;
        public HistoricalTechnologyAvailability availability;
        public HistoricalTechnologyAdoption adoption;
    }

    /// <summary>Scenario-specific game setup for a faction's initial knowledge; it is not a claim of historical invention.</summary>
    public sealed class HistoricalFactionTechnologyProfileDef : Def
    {
        public HistoricalScenarioDef scenario;
        public HistoricalStateDef historicalState;
        public FactionDef factionDef;
        public List<string> profileTags;
        public List<string> cultureTags;
        public List<HistoricalTechnologyDef> initialMasteredTechnologies;
        public List<HistoricalTechnologyDef> initialHeardOfTechnologies;
        public float diffusionSusceptibility = 1f;
        public string sourceNotes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (historicalState == null && factionDef == null) yield return "historicalState or factionDef is required.";
            if (historicalState != null && scenario != null && (scenario.states == null || !scenario.states.Contains(historicalState)))
                yield return "historicalState must be included by scenario.";
            if (diffusionSusceptibility <= 0f) yield return "diffusionSusceptibility must be positive.";
            if (initialMasteredTechnologies != null && initialMasteredTechnologies.Any(item => item == null))
                yield return "initialMasteredTechnologies cannot contain null entries.";
            if (initialHeardOfTechnologies != null && initialHeardOfTechnologies.Any(item => item == null))
                yield return "initialHeardOfTechnologies cannot contain null entries.";
        }
    }
}
