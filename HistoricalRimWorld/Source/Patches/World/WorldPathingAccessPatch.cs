using System;
using HarmonyLib;
using HistoricalRimWorld.World;
using RimWorld.Planet;

namespace HistoricalRimWorld.Patches.World
{
    // Verified against RimWorld 1.6 Assembly-CSharp: WorldPathing.FindPath(PlanetTile, PlanetTile, Caravan, Func<float,bool>).
    [HarmonyPatch(typeof(WorldPathing), nameof(WorldPathing.FindPath),
        new Type[] { typeof(PlanetTile), typeof(PlanetTile), typeof(Caravan), typeof(Func<float, bool>) })]
    internal static class WorldPathingAccessPatch
    {
        private static void Prefix(Caravan caravan, out bool __state)
        {
            __state = caravan != null && caravan.IsPlayerControlled && HistoricalWorldRevealManager.IsActive;
            if (__state) HistoricalWorldPatchContext.EnterPlayerCaravanPath();
        }

        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) HistoricalWorldPatchContext.ExitPlayerCaravanPath();
            return __exception;
        }
    }
}
