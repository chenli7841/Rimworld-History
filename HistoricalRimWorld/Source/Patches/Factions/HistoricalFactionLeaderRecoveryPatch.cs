using HarmonyLib;
using HistoricalRimWorld.Factions;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.Factions
{
    /// <summary>Repairs missing leaders for factions managed by the Eastern Zhou scenario.</summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.FactionTick))]
    internal static class HistoricalFactionLeaderRecoveryPatch
    {
        private const string ScenarioDefName = "HR_Scenario_EasternZhou_770BCE";

        private static void Prefix(Faction __instance)
        {
            if (__instance?.leader != null) return;
            if (__instance == Faction.OfPlayerSilentFail) return;
            HistoricalFactionWorldComponent component = Find.World?
                .GetComponent<HistoricalFactionWorldComponent>();
            if (component?.ScenarioDefName != ScenarioDefName
                || !HistoricalFactionManager.IsAllowedFaction(__instance, ScenarioDefName))
                return;

            if (__instance.TryGenerateNewLeader())
                Log.Message("[Historical RimWorld] Regenerated missing leader for faction "
                    + __instance.Name + ".");
        }
    }
}
