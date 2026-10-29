using System.Linq;
using RimWorld;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Scenarios.EasternZhou;
using Verse;

namespace HistoricalRimWorld.Factions
{
    /// <summary>Derives the creation mode from the selected scenario; no global FactionDef state is mutated.</summary>
    public static class HistoricalFactionGenerationContext
    {
        public static bool IsHistoricalWorldGenerationActive => TryGetSelectedSetup(out _);

        public static bool TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
        {
            setup = null;
            Scenario selected = Current.Game?.Scenario;
            if (selected == null || string.IsNullOrWhiteSpace(selected.name)) return false;

            setup = DefDatabase<HistoricalPlayableScenarioSetupDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.scenario != null
                    && item.vanillaScenarioName == selected.name);
            return setup != null;
        }
    }
}
