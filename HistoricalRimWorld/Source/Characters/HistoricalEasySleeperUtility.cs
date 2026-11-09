using System;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Scenarios.EasternZhou;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Characters
{
    internal static class HistoricalEasySleeperUtility
    {
        private const string TraitDefName = "HR_EasySleeper";
        private const string EasternZhouScenarioDefName = "HR_Scenario_EasternZhou_770BCE";

        // Verified against RimWorld 1.6 Need_Rest.NeedInterval: a standard sleep tick adds
        // 0.005714286 rest per 150-tick need interval, before RestRateMultiplier/bed effectiveness.
        private const float BaselineSleepRestPerNeedInterval = 0.005714286f;
        private const float NeedIntervalTicks = 150f;
        private const float IdleRestFractionOfSleep = 0.40f;

        public static bool IsEnabledFor(Pawn pawn)
        {
            return pawn?.story?.traits != null
                && pawn.story.traits.allTraits.Any(trait => trait?.def?.defName == TraitDefName)
                && HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                && setup.scenario?.defName == EasternZhouScenarioDefName;
        }

        public static bool IsGenuinelyIdle(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Dead || pawn.Downed
                || pawn.Drafted || pawn.InMentalState || pawn.InAggroMentalState
                || pawn.needs?.rest == null || pawn.needs.rest.Resting
                || pawn.jobs?.curJob == null || pawn.jobs.curJob.def != JobDefOf.Wait
                || pawn.jobs.curJob.playerForced || pawn.jobs.jobQueue?.Count > 0
                || pawn.pather?.Moving == true || pawn.stances?.FullBodyBusy == true
                || pawn.mindState?.enemyTarget != null)
            {
                return false;
            }

            return true;
        }

        public static bool TryPrepareAwakeIdleRestGain(Need_Rest rest, Pawn pawn,
            out float awakeLoss, out float baselineSleepGain)
        {
            awakeLoss = 0f;
            baselineSleepGain = 0f;
            if (rest == null || !IsEnabledFor(pawn) || !IsGenuinelyIdle(pawn) || rest.CurLevel >= 1f)
                return false;

            // Capture D before vanilla changes CurLevel: RestFallPerTick depends on the
            // RestCategory and that category can change during this very interval.
            awakeLoss = rest.RestFallPerTick * NeedIntervalTicks
                * pawn.GetStatValue(StatDefOf.RestFallRateFactor);
            baselineSleepGain = BaselineSleepRestPerNeedInterval
                * pawn.GetStatValue(StatDefOf.RestRateMultiplier);
            return true;
        }

        public static void ApplyAwakeIdleRestGain(Need_Rest rest, Pawn pawn,
            float awakeLoss, float baselineSleepGain)
        {
            if (rest == null || !IsGenuinelyIdle(pawn) || rest.CurLevel >= 1f) return;

            // NeedInterval already subtracted awakeLoss. Restore it and add 40% of the
            // ordinary baseline sleep gain, so the resulting net gain is exactly 0.40S.
            float compensationAndGain = awakeLoss + baselineSleepGain * IdleRestFractionOfSleep;
            rest.CurLevel = Math.Min(1f, rest.CurLevel + compensationAndGain);
        }

        public static bool ShouldSuppressGroundSleepThought(Pawn pawn, Thought_Memory thought)
        {
            return thought?.def?.defName == "SleptOnGround" && IsEnabledFor(pawn);
        }

        public static bool IsHistoricalScenarioSelected()
        {
            return HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                && setup.scenario?.defName == EasternZhouScenarioDefName;
        }

        public static bool HasEasySleeper(Pawn pawn)
        {
            return pawn?.story?.traits != null
                && pawn.story.traits.allTraits.Any(trait => trait?.def?.defName == TraitDefName);
        }
    }
}
