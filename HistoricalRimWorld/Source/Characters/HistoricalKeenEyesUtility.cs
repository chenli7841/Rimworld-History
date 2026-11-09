using System.Linq;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public static class HistoricalKeenEyesUtility
    {
        public const int VisionRadiusBonus = 3;
        public const float BowRangeMultiplier = 1.10f;
        private const string TraitDefName = "HR_KeenEyes";

        public static bool HasTrait(Pawn pawn)
        {
            return pawn?.story?.traits?.allTraits != null
                && pawn.story.traits.allTraits.Any(trait => trait?.def?.defName == TraitDefName);
        }

        public static bool IsEnabledFor(Pawn pawn)
        {
            return HasTrait(pawn) && HistoricalScenarioManager.IsScenarioActive;
        }

        public static int GetVisionBonus(Pawn pawn)
        {
            return IsEnabledFor(pawn) ? VisionRadiusBonus : 0;
        }
    }
}
