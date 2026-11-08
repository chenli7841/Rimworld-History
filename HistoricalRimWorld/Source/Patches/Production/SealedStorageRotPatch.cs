using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace HistoricalRimWorld.Patches.Production
{
    /// <summary>Scales vanilla rottable elapsed ticks only for opted-in food occupying our own storage buildings.</summary>
    [HarmonyPatch(typeof(CompRottable), "CompTickInterval")]
    internal static class SealedStorageRotPatch
    {
        private static void Prefix(CompRottable __instance, ref int delta)
        {
            float multiplier = HistoricalRimWorld.Production.SealedStorage.SealedStorageUtility.RotMultiplierFor(__instance?.parent);
            if (multiplier >= 1f) return;

            // CompRottable is updated at interval boundaries in RimWorld. Keeping at least one tick
            // prevents low-frequency callers from accidentally freezing food indefinitely.
            delta = Mathf.Max(1, Mathf.RoundToInt(delta * multiplier));
        }
    }
}
