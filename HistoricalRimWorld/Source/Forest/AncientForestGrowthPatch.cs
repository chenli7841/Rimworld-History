using HarmonyLib;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Applies the living-soil growth bonus to ancient trees only.</summary>
    [HarmonyPatch(typeof(Plant), "get_GrowthRate")]
    internal static class AncientForestGrowthPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Plant __instance, ref float __result)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || __instance?.Spawned != true
                || __instance.def?.GetModExtension<HR_AncientTreeExtension>() == null || __result <= 0f)
                return;

            MapComponent_AncientForestSoil soil = __instance.Map?.GetComponent<MapComponent_AncientForestSoil>();
            bool livingSoil = soil?.IsAncientForestSoil(__instance.Position) == true;
            float multiplier = AncientForestGrowthSettings.GetGrowthMultiplier(livingSoil);
            if (multiplier != 1f) __result *= multiplier;
        }
    }
}
