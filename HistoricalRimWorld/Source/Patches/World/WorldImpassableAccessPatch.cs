using HarmonyLib;
using HistoricalRimWorld.World;
using RimWorld.Planet;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Patches.World
{
    // WorldPathing.FindPath calls World.Impassable while expanding path nodes. Scope the rule to player caravan searches.
    [HarmonyPatch(typeof(RimWorldWorld), "Impassable", typeof(PlanetTile))]
    internal static class WorldImpassableAccessPatch
    {
        // Use Harmony's positional argument binding: the target has exposed this parameter as `tileID`,
        // while compiled mod copies have previously attempted to bind it by the stale name `tile`.
        private static bool Prefix(PlanetTile __0, ref bool __result)
        {
            if (!HistoricalWorldPatchContext.IsPlayerCaravanPath || !HistoricalWorldRevealManager.IsActive) return true;
            if (HistoricalWorldRevealManager.IsTileAccessible((int)__0)) return true;
            __result = true;
            return false;
        }
    }
}
