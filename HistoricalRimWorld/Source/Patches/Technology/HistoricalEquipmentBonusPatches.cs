using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Technology;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.Technology
{
    /// <summary>Applies faction-specific bonuses after RimWorld has calculated tool damage.</summary>
    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedMeleeDamageAmount),
        new[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) })]
    internal static class HistoricalMeleeDamageBonusPatch
    {
        private static void Postfix(Pawn __1, Thing __2, ref float __result)
        {
            if (__1 == null || __2 == null) return;
            bool equipped = __1.equipment?.Primary == __2;
            float bonus = HistoricalTechnologyBonusService.GetBonus(__1.Faction, __2,
                HistoricalTechnologyBonusType.MeleeDamage, equipped);
            __result *= 1f + bonus;
        }
    }

    /// <summary>Modifies the actual per-tool melee penetration result, preserving blunt attacks.</summary>
    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration),
        new[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) })]
    internal static class HistoricalMeleeArmorPenetrationBonusPatch
    {
        private static void Postfix(Tool __0, Pawn __1, Thing __2, ref float __result)
        {
            if (__1 == null || __2 == null) return;
            bool equipped = __1.equipment?.Primary == __2;
            bool sharpTool = __0?.capacities != null && __0.capacities.Any(capacity =>
                capacity?.defName == "Cut" || capacity?.defName == "Stab");
            float bonus = HistoricalTechnologyBonusService.GetBonus(__1.Faction, __2,
                HistoricalTechnologyBonusType.MeleeArmorPenetration, equipped, sharpTool);
            __result *= 1f + bonus;
        }
    }

    /// <summary>Adjusts each worn apparel layer only when that layer is used by vanilla armor resolution.</summary>
    [HarmonyPatch]
    internal static class HistoricalArmorProtectionBonusPatch
    {
        private static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method(typeof(Verse.ArmorUtility), "ApplyArmor");

        private static void Prefix(ref float __2, Thing __3, DamageDef __4, Pawn __5)
        {
            if (!( __3 is Apparel apparel) || __4?.armorCategory == null || __5?.apparel?.WornApparel == null
                || !__5.apparel.WornApparel.Contains(apparel)) return;

            HistoricalTechnologyBonusType type;
            switch (__4.armorCategory.defName)
            {
                case "Sharp": type = HistoricalTechnologyBonusType.ArmorSharp; break;
                case "Blunt": type = HistoricalTechnologyBonusType.ArmorBlunt; break;
                default: return;
            }

            float bonus = HistoricalTechnologyBonusService.GetBonus(__5.Faction, apparel, type, equipped: true);
            __2 *= 1f + bonus;
        }
    }

    /// <summary>Explains active and future player-faction equipment effects without changing item stats.</summary>
    [HarmonyPatch(typeof(Thing), "GetInspectString")]
    internal static class HistoricalEquipmentBonusInspectPatch
    {
        private static void Postfix(Thing __instance, ref string __result)
        {
            string detail = HistoricalTechnologyBonusService.DescribePlayerBonusesForEquipment(__instance);
            if (string.IsNullOrWhiteSpace(detail)) return;
            __result = (string.IsNullOrWhiteSpace(__result) ? string.Empty : __result + "\n") + detail;
        }
    }
}
