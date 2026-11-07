using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Scenarios;
using RimWorld;

namespace HistoricalRimWorld.Patches.UI
{
    /// <summary>
    /// Limits the Architect category tabs for the Eastern Zhou scenario to the
    /// categories intended for its historical setting. Other scenarios retain
    /// RimWorld's normal category list.
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Architect), "CacheDesPanels")]
    internal static class HistoricalArchitectCategoryFilterPatch
    {
        private const string EasternZhouScenarioDefName = "HR_Scenario_EasternZhou_770BCE";

        private static readonly HashSet<string> AllowedCategories = new HashSet<string>
        {
            "Orders",
            "Zone",
            "Structure",
            "Production",
            "Furniture",
            "Security",
            "Misc",
            "Floors",
            "Joy",
            "Temperature"
        };

        private static void Postfix(
            ref List<ArchitectCategoryTab> ___desPanelsCached,
            ref ArchitectCategoryTab ___selectedDesPanel)
        {
            if (!string.Equals(HistoricalScenarioManager.ActiveScenarioDefName,
                    EasternZhouScenarioDefName, System.StringComparison.Ordinal)
                || ___desPanelsCached == null)
            {
                return;
            }

            ___desPanelsCached.RemoveAll(panel => panel?.def == null
                || !AllowedCategories.Contains(panel.def.defName));

            if (___selectedDesPanel != null && !___desPanelsCached.Contains(___selectedDesPanel))
                ___selectedDesPanel = ___desPanelsCached.FirstOrDefault();
        }
    }
}
