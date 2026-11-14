using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.UI
{
    /// <summary>
    /// Hides selected electricity-dependent vanilla production buildings from
    /// the Architect menu in the Eastern Zhou historical scenario. The vanilla
    /// ThingDefs and recipes remain intact for other scenarios and saves.
    /// </summary>
    [HarmonyPatch(typeof(DesignationCategoryDef), "get_ResolvedAllowedDesignators")]
    internal static class HistoricalElectricArchitectDesignatorFilterPatch
    {
        private const string EasternZhouScenarioDefName = "HR_Scenario_EasternZhou_770BCE";

        private static readonly HashSet<string> HiddenBuildableDefNames = new HashSet<string>
        {
            "ElectricTailoringBench",
            "ElectricSmelter",
            "ElectricStove",
            "ElectricCrematorium"
        };

        private static void Postfix(ref IEnumerable<Designator> __result)
        {
            if (!string.Equals(HistoricalScenarioManager.ActiveScenarioDefName,
                    EasternZhouScenarioDefName, System.StringComparison.Ordinal)
                || __result == null)
            {
                return;
            }

            __result = __result.Where(designator =>
            {
                Designator_Build buildDesignator = designator as Designator_Build;
                return buildDesignator == null
                    || buildDesignator.PlacingDef == null
                    || !HiddenBuildableDefNames.Contains(buildDesignator.PlacingDef.defName);
            });
        }
    }
}
