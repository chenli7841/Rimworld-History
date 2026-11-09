using System.Reflection;
using HarmonyLib;
using HistoricalRimWorld.Characters;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.Characters
{
    /// <summary>Applies Easy Sleeper effects at the vanilla rest-need interval, not every game tick.</summary>
    [HarmonyPatch(typeof(Need_Rest), "NeedInterval")]
    internal static class HistoricalEasySleeperRestPatch
    {
        private struct IntervalState
        {
            public bool Active;
            public float AwakeLoss;
            public float BaselineSleepGain;
        }

        private static void Prefix(Need_Rest __instance, Pawn ___pawn, ref IntervalState __state)
        {
            __state.Active = HistoricalEasySleeperUtility.TryPrepareAwakeIdleRestGain(
                __instance, ___pawn, out __state.AwakeLoss, out __state.BaselineSleepGain);
        }

        private static void Postfix(Need_Rest __instance, Pawn ___pawn, IntervalState __state)
        {
            if (__state.Active)
            {
                HistoricalEasySleeperUtility.ApplyAwakeIdleRestGain(
                    __instance, ___pawn, __state.AwakeLoss, __state.BaselineSleepGain);
            }
        }
    }

    /// <summary>Suppresses only the ground-sleep memory for pawns with the trait.</summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
        new[] { typeof(Thought_Memory), typeof(Pawn) })]
    internal static class HistoricalEasySleeperGroundThoughtPatch
    {
        private static bool Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought)
        {
            return !HistoricalEasySleeperUtility.ShouldSuppressGroundSleepThought(__instance?.pawn, newThought);
        }
    }

    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
        new[] { typeof(ThoughtDef), typeof(Pawn), typeof(Precept) })]
    internal static class HistoricalEasySleeperGroundThoughtDefPatch
    {
        private static bool Prefix(MemoryThoughtHandler __instance, ThoughtDef def)
        {
            return def?.defName != "SleptOnGround"
                || !HistoricalEasySleeperUtility.IsEnabledFor(__instance?.pawn);
        }
    }

    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemoryFast),
        new[] { typeof(ThoughtDef), typeof(Precept) })]
    internal static class HistoricalEasySleeperGroundThoughtFastPatch
    {
        private static bool Prefix(MemoryThoughtHandler __instance, ThoughtDef mem)
        {
            return mem?.defName != "SleptOnGround"
                || !HistoricalEasySleeperUtility.IsEnabledFor(__instance?.pawn);
        }
    }

    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemoryFast),
        new[] { typeof(ThoughtDef), typeof(int), typeof(Precept) })]
    internal static class HistoricalEasySleeperGroundThoughtFastStagePatch
    {
        private static bool Prefix(MemoryThoughtHandler __instance, ThoughtDef mem)
        {
            return mem?.defName != "SleptOnGround"
                || !HistoricalEasySleeperUtility.IsEnabledFor(__instance?.pawn);
        }
    }

    /// <summary>Prevents this historical-only trait from being randomly generated in unrelated scenarios.</summary>
    [HarmonyPatch]
    internal static class HistoricalEasySleeperAvailabilityPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PawnGenerator), "GenerateTraits",
                new[] { typeof(Pawn), typeof(PawnGenerationRequest) });
        }

        private static void Postfix(Pawn pawn)
        {
            if (HistoricalEasySleeperUtility.IsHistoricalScenarioSelected()) return;

            RemoveTrait(pawn, "HR_EasySleeper");
            RemoveTrait(pawn, "HR_Frugal");
            RemoveTrait(pawn, "HR_KeenEyes");
        }

        private static void RemoveTrait(Pawn pawn, string defName)
        {
            if (pawn?.story?.traits == null) return;
            Trait trait = pawn.story.traits.allTraits.Find(item => item?.def?.defName == defName);
            if (trait != null) pawn.story.traits.RemoveTrait(trait);
        }
    }

    /// <summary>Halves only negative mood effects caused by modest housing conditions.</summary>
    [HarmonyPatch(typeof(Thought), nameof(Thought.MoodOffset))]
    internal static class HistoricalFrugalHousingMoodPatch
    {
        private static void Postfix(Thought __instance, ref float __result)
        {
            __result = HistoricalFrugalUtility.AdjustHousingMood(__instance, __result);
        }
    }

    /// <summary>Thought_Memory overrides Thought.MoodOffset, so patch that override as well.</summary>
    [HarmonyPatch(typeof(Thought_Memory), nameof(Thought.MoodOffset))]
    internal static class HistoricalFrugalHousingMemoryMoodPatch
    {
        private static void Postfix(Thought_Memory __instance, ref float __result)
        {
            __result = HistoricalFrugalUtility.AdjustHousingMood(__instance, __result);
        }
    }
}
