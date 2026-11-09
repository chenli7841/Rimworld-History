using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Scenarios.EasternZhou;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Characters
{
    internal static class HistoricalFrugalUtility
    {
        private const string TraitDefName = "HR_Frugal";
        private const string EasternZhouScenarioDefName = "HR_Scenario_EasternZhou_770BCE";

        public static bool HasTrait(Pawn pawn)
        {
            return pawn?.story?.traits?.allTraits != null
                && pawn.story.traits.allTraits.Any(trait => trait?.def?.defName == TraitDefName);
        }

        public static bool IsHistoricalScenarioSelected()
        {
            return HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                && setup.scenario?.defName == EasternZhouScenarioDefName;
        }

        public static bool IsEnabledFor(Pawn pawn)
        {
            return HasTrait(pawn) && IsHistoricalScenarioSelected();
        }

        public static bool IsModestHousingThought(ThoughtDef def)
        {
            if (def?.defName == null) return false;

            switch (def.defName)
            {
                case "SleptInBedroom":
                case "SleptInBarracks":
                case "NeedRoomSize":
                case "PrisonCell":
                case "PrisonBarracks":
                    return true;
                default:
                    return false;
            }
        }

        public static float AdjustHousingMood(Thought thought, float originalOffset)
        {
            if (thought?.pawn == null || thought.def == null || originalOffset >= 0f
                || !IsModestHousingThought(thought.def) || !IsEnabledFor(thought.pawn))
            {
                return originalOffset;
            }

            return originalOffset * 0.5f;
        }
    }

    public sealed class ThoughtWorker_LowQualityApparel : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            if (pawn?.apparel?.WornApparel == null || !pawn.Spawned
                || !HistoricalEasySleeperUtility.IsHistoricalScenarioSelected())
            {
                return ThoughtState.Inactive;
            }

            bool hasAwfulQualityClothing = false;
            bool hasPoorQualityClothing = false;
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                if (apparel == null || !QualityUtility.TryGetQuality(apparel, out QualityCategory quality))
                    continue;

                if (quality == QualityCategory.Awful) hasAwfulQualityClothing = true;
                else if (quality == QualityCategory.Poor) hasPoorQualityClothing = true;
            }

            if (hasAwfulQualityClothing)
                return ThoughtState.ActiveAtStage(HistoricalFrugalUtility.HasTrait(pawn) ? 2 : 0);
            if (hasPoorQualityClothing)
                return ThoughtState.ActiveAtStage(HistoricalFrugalUtility.HasTrait(pawn) ? 3 : 1);

            return ThoughtState.Inactive;
        }
    }
}
