using System.Collections.Generic;
using HarmonyLib;
using HistoricalRimWorld.Factions;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.Patches.Factions
{
    /// <summary>
    /// Zheng's historical starting territory is expected to host the player's colony.
    /// Exclude Zheng-owned settlements from the vanilla proximity warning and goodwill loss there.
    /// </summary>
    [HarmonyPatch(typeof(SettlementProximityGoodwillUtility), "AppendProximityGoodwillOffsets")]
    internal static class HistoricalZhengTerritoryProximityGoodwillPatch
    {
        private const string ScenarioDefName = "HR_Scenario_EasternZhou_770BCE";
        private const string ZhengFactionDefName = "HR_Faction_Zheng";
        private const string ZhengRegionDefName = "HR_Region_ZhengHeartland";

        private static void Prefix(List<Pair<Settlement, int>> outOffsets, out int __state)
        {
            __state = outOffsets?.Count ?? 0;
        }

        private static void Postfix(PlanetTile tile, List<Pair<Settlement, int>> outOffsets, int __state)
        {
            HistoricalFactionWorldComponent component = Find.World?
                .GetComponent<HistoricalFactionWorldComponent>();
            if (component == null
                || component.GenerationMode != HistoricalFactionGenerationMode.HistoricalScenario
                || component.ScenarioDefName != ScenarioDefName
                || !component.IsTileInRegion(tile.tileId, ZhengRegionDefName)
                || outOffsets == null)
                return;

            for (int index = outOffsets.Count - 1; index >= __state; index--)
            {
                Settlement nearbySettlement = outOffsets[index].First;
                if (nearbySettlement?.Faction?.def?.defName == ZhengFactionDefName)
                    outOffsets.RemoveAt(index);
            }
        }
    }
}
