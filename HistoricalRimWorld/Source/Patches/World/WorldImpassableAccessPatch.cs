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
        private static bool Prefix(PlanetTile tileID, ref bool __result)
        {
            if (!HistoricalWorldPatchContext.IsPlayerCaravanPath || !HistoricalWorldRevealManager.IsActive) return true;
            if (HistoricalWorldRevealManager.IsTileAccessible((int)tileID)) return true;
            __result = true;
            return false;
        }
    }
}
