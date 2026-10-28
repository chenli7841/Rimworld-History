using HarmonyLib;
using HistoricalRimWorld.World;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.Patches.World
{
    // Verified against RimWorld 1.6 Assembly-CSharp: Caravan_PathFollower.StartPath(PlanetTile, CaravanArrivalAction, bool, bool).
    [HarmonyPatch(typeof(Caravan_PathFollower), nameof(Caravan_PathFollower.StartPath),
        new System.Type[] { typeof(PlanetTile), typeof(CaravanArrivalAction), typeof(bool), typeof(bool) })]
    internal static class CaravanStartPathAccessPatch
    {
        private static bool Prefix(Caravan ___caravan, PlanetTile destTile)
        {
            if (___caravan == null || !___caravan.IsPlayerControlled || !HistoricalWorldRevealManager.IsActive) return true;
            if (HistoricalWorldRevealManager.IsTileAccessible((int)destTile))
            {
                HistoricalWorldPatchContext.ClearCaravanBlockNotification(___caravan.ID);
                return true;
            }

            Messages.Message(Translator.Translate("HR_World_LockedDestination"), MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }

    // Covers a region becoming Locked while a player caravan is already travelling.
    [HarmonyPatch(typeof(Caravan_PathFollower), "IsPassable", typeof(PlanetTile))]
    internal static class CaravanNextTileAccessPatch
    {
        private static bool Prefix(Caravan ___caravan, PlanetTile tile, ref bool __result)
        {
            if (___caravan == null || !___caravan.IsPlayerControlled || !HistoricalWorldRevealManager.IsActive) return true;
            if (HistoricalWorldRevealManager.IsTileAccessible((int)tile))
            {
                HistoricalWorldPatchContext.ClearCaravanBlockNotification(___caravan.ID);
                return true;
            }

            __result = false;
            if (HistoricalWorldPatchContext.MarkCaravanBlockNotified(___caravan.ID))
            {
                Messages.Message(Translator.Translate("HR_World_LockedRouteStopped"), MessageTypeDefOf.RejectInput, false);
            }
            return false;
        }
    }
}
