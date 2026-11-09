using HarmonyLib;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.Characters
{
    /// <summary>Replaces vanilla sci-fi biographies only in the Eastern Zhou historical scenario.</summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), nameof(PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo))]
    internal static class HistoricalBackstoryGenerationPatch
    {
        private static void Postfix(Pawn pawn)
        {
            HistoricalRimWorld.Characters.HistoricalBackstorySelector.AssignForGeneratedPawn(pawn);
        }
    }

    /// <summary>Assigns a historical adult career when a child reaches the adult life stage.</summary>
    [HarmonyPatch(typeof(Pawn_AgeTracker), "PostResolveLifeStageChange")]
    internal static class HistoricalAdultBackstoryPatch
    {
        private static void Postfix(Pawn ___pawn)
        {
            HistoricalRimWorld.Characters.HistoricalBackstorySelector.EnsureAdultBackstory(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    internal static class HistoricalAdultBackstoryBirthdayPatch
    {
        private static void Postfix(Pawn ___pawn)
        {
            HistoricalRimWorld.Characters.HistoricalBackstorySelector.EnsureAdultBackstory(___pawn);
        }
    }
}
