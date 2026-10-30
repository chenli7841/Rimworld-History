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
        private static Scenario selectedScenario;
        private static bool hasScenarioSelection;

        public static bool IsHistoricalWorldGenerationActive => TryGetSelectedSetup(out _);

        public static void CaptureSelectedScenario(Scenario scenario)
        {
            selectedScenario = scenario;
            hasScenarioSelection = true;
            Log.Message("[Historical RimWorld] Captured selected scenario context: "
                + (scenario?.name ?? "<none>") + ".");
        }

        public static bool TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
        {
            setup = null;
            Scenario selected = hasScenarioSelection ? selectedScenario : Current.Game?.Scenario;
            if (selected == null || string.IsNullOrWhiteSpace(selected.name)) return false;

            setup = DefDatabase<HistoricalPlayableScenarioSetupDef>.AllDefsListForReading
                .FirstOrDefault(item => item.defName == "HR_PlayableSetup_EasternZhou770");
            return setup != null;
        }
    }
}
