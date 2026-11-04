using System.Linq;
using RimWorld;
using HistoricalRimWorld.Characters;
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
            HistoricalChineseNameGenerator.ResetUsedNames();
            Log.Message("[Historical RimWorld] Captured selected scenario context: "
                + (scenario?.name ?? "<none>") + ".");
        }

        public static bool IsSelectedPlayerFaction(FactionDef factionDef)
        {
            return factionDef != null && TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                && factionDef == setup.playerFactionDef;
        }

        public static bool TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
        {
            setup = null;
            Scenario selected = hasScenarioSelection ? selectedScenario : Current.Game?.Scenario;
            if (selected == null || string.IsNullOrWhiteSpace(selected.name)) return false;
            setup = HistoricalPlayableScenarioSetupDef.FindForVanillaScenario(selected);
            return setup != null;
        }
    }
}
