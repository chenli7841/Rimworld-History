using System;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Scenarios.EasternZhou;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.Characters
{
    /// <summary>Removes generated firearms from the Eastern Zhou scenario's starting pawn loadout.</summary>
    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn))]
    internal static class HistoricalEasternZhouStartingPawnFirearmPatch
    {
        private const string ScenarioDefName = "HR_Scenario_EasternZhou_770BCE";

        private static void Postfix(ref Pawn __result)
        {
            if (__result == null
                || !HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                || setup.scenario?.defName != ScenarioDefName) return;

            int removed = RemoveFirearms(__result);
            if (removed > 0)
            {
                Log.Message("[Historical RimWorld] Removed " + removed
                    + " firearm(s) from a generated Eastern Zhou starting colonist. Cold weapons are retained.");
            }
        }

        private static int RemoveFirearms(Pawn pawn)
        {
            int removed = 0;
            if (pawn.equipment != null)
            {
                ThingWithComps[] equippedFirearms = pawn.equipment.AllEquipmentListForReading
                    .Where(item => IsFirearm(item?.def)).ToArray();
                for (int i = 0; i < equippedFirearms.Length; i++)
                {
                    ThingWithComps firearm = equippedFirearms[i];
                    pawn.equipment.Remove(firearm);
                    firearm.Destroy(DestroyMode.Vanish);
                    removed++;
                }
            }

            ThingOwner<Thing> inventory = pawn.inventory?.innerContainer;
            if (inventory != null)
            {
                Thing[] carriedFirearms = inventory.InnerListForReading
                    .Where(item => IsFirearm(item?.def)).ToArray();
                for (int i = 0; i < carriedFirearms.Length; i++)
                {
                    Thing firearm = carriedFirearms[i];
                    inventory.Remove(firearm);
                    firearm.Destroy(DestroyMode.Vanish);
                    removed++;
                }
            }

            return removed;
        }

        private static bool IsFirearm(ThingDef def)
        {
            if (def == null || !def.IsWeapon || !def.IsRangedWeapon) return false;

            // RimWorld's firearm defs use tags such as Gun, SimpleGun, IndustrialGunAdvanced,
            // and SpacerGun. This also catches firearms added by mods using the normal tags.
            if (def.weaponTags != null && def.weaponTags.Any(tag => tag != null
                && tag.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) >= 0)) return true;

            // Retain a fallback for firearm defs that use the conventional vanilla defName prefix.
            return def.defName != null && def.defName.StartsWith("Gun_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
