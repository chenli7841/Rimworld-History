using HarmonyLib;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Combat;
using Verse;

namespace HistoricalRimWorld.Patches.Characters
{
    /// <summary>
    /// EffectiveRange is the per-verb range used by targeting, range rings, AI positioning,
    /// and CanHitTargetFrom. Modifying this Pawn-owned result keeps shared weapon Defs intact.
    /// </summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.EffectiveRange), MethodType.Getter)]
    internal static class HistoricalKeenEyesBowRangePatch
    {
        private static void Postfix(Verb __instance, ref float __result)
        {
            Pawn pawn = __instance?.CasterPawn;
            ThingDef weaponDef = __instance?.EquipmentSource?.def;
            if (!HistoricalKeenEyesUtility.IsEnabledFor(pawn)
                || weaponDef?.GetModExtension<HR_BowWeaponExtension>() == null)
            {
                return;
            }

            __result *= HistoricalKeenEyesUtility.BowRangeMultiplier;
        }
    }
}
