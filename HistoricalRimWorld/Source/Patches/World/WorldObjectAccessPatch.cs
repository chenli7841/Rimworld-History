using HarmonyLib;
using HistoricalRimWorld.World;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.Patches.World
{
    // Hides world objects on Locked tiles from player selection/inspection without stopping background simulation.
    [HarmonyPatch(typeof(WorldObject), "get_SelectableNow")]
    internal static class WorldObjectSelectionAccessPatch
    {
        private static bool Prefix(WorldObject __instance, ref bool __result)
        {
            if (HistoricalWorldRevealManager.IsActive
                && !HistoricalWorldRevealManager.IsTileVisible((int)__instance.Tile))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    // Only player settlements are blocked; NPC and historical background world objects remain unchanged.
    [HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Add), typeof(WorldObject))]
    internal static class PlayerSettlementAccessPatch
    {
        private static bool Prefix(WorldObject o)
        {
            if (!(o is Settlement) || !HistoricalWorldRevealManager.IsActive) return true;

            Faction playerFaction = Faction.OfPlayerSilentFail;
            if (playerFaction == null || o.Faction != playerFaction
                || HistoricalWorldRevealManager.IsTileAccessible((int)o.Tile)) return true;

            Messages.Message(Translator.Translate("HR_World_LockedSettlement"), MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }
}
