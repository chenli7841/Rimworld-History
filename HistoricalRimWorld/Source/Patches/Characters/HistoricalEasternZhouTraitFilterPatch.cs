using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Scenarios.EasternZhou;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.Characters
{
    /// <summary>Filters generated traits after RimWorld has finished constructing a new starting pawn.</summary>
    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn))]
    internal static class HistoricalEasternZhouTraitFilterPatch
    {
        private const string EasternZhouScenarioDefName = "HR_Scenario_EasternZhou_770BCE";

        private static void Postfix(ref Pawn __result)
        {
            Pawn pawn = __result;
            if (pawn?.story?.traits == null
                || !HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                || setup.scenario?.defName != EasternZhouScenarioDefName)
            {
                return;
            }

            Trait[] excludedTraits = pawn.story.traits.allTraits
                .Where(IsExcludedTrait)
                .ToArray();

            foreach (Trait trait in excludedTraits)
            {
                pawn.story.traits.RemoveTrait(trait);
                Log.Message("[Historical RimWorld] Removed an era-inappropriate generated trait ('"
                    + trait.def.defName + "', degree " + trait.Degree + ") from "
                    + pawn.LabelShort + " in the Eastern Zhou scenario.");
            }
        }

        private static bool IsExcludedTrait(Trait trait)
        {
            if (trait?.def == null) return false;

            switch (trait.def.defName)
            {
                case "Transhumanist":
                case "BodyPurist":
                case "ShootingAccuracy":
                case "PsychicSensitivity":
                case "DrugDesire":
                case "Gay":
                case "Bisexual":
                    return true;
                default:
                    return false;
            }
        }
    }
}
