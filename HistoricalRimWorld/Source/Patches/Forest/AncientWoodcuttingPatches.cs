using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Forest;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Patches.Forest
{
    [HarmonyPatch(typeof(Plant), nameof(Plant.YieldNow))]
    internal static class AncientTreeYieldPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Plant __instance, ref int __result)
        {
            if (HistoricalScenarioManager.IsScenarioActive && AncientWoodcuttingUtility.IsAncientTree(__instance))
                __result = AncientWoodcuttingUtility.AdjustWoodYield(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    internal static class AncientTreeRareDropPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Plant __instance, Pawn by, PlantDestructionMode __1)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || __1 != PlantDestructionMode.Chop
                || !AncientWoodcuttingUtility.IsAncientTree(__instance) || by?.Map == null) return;
            MapComponent_AncientTreeHarvest state = by.Map.GetComponent<MapComponent_AncientTreeHarvest>();
            if (state != null && state.TryMarkResolved(__instance.thingIDNumber))
                AncientWoodcuttingUtility.ResolveRareDrops(__instance, by, by.Map, __instance.Position);
        }
    }

    [HarmonyPatch(typeof(WorkGiver_PlantsCut), nameof(WorkGiver_PlantsCut.PotentialWorkThingsGlobal))]
    internal static class AncientTreeCutCandidatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, ref IEnumerable<Thing> __result)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || __result == null) return;
            __result = __result.Where(thing => !(thing is Plant plant && AncientWoodcuttingUtility.IsAncientTree(plant))
                || AncientWoodcuttingUtility.CanCutAncientTree(pawn, (Plant)thing, out _));
        }
    }

    [HarmonyPatch(typeof(WorkGiver_PlantsCut), nameof(WorkGiver_PlantsCut.JobOnThing))]
    internal static class AncientTreeCutJobPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, Thing t, ref Job __result)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || !(t is Plant tree)
                || !AncientWoodcuttingUtility.IsAncientTree(tree)) return;
            if (!AncientWoodcuttingUtility.CanCutAncientTree(pawn, tree, out _))
            {
                __result = null;
                return;
            }
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("HR_CutAncientTree");
            if (def != null) __result = JobMaker.MakeJob(def, tree);
        }
    }

    [HarmonyPatch(typeof(JobDriver_PlantCut), "Init")]
    internal static class AncientTreeManualCutValidationPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(JobDriver_PlantCut __instance)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || !(__instance.job?.targetA.Thing is Plant tree)
                || !AncientWoodcuttingUtility.IsAncientTree(tree)) return true;
            if (AncientWoodcuttingUtility.CanCutAncientTree(__instance.pawn, tree, out string reason))
            {
                JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("HR_CutAncientTree");
                if (def == null) return true;
                Job replacement = JobMaker.MakeJob(def, tree);
                replacement.playerForced = __instance.job.playerForced;
                if (!__instance.pawn.jobs.TryTakeOrderedJob(replacement, JobTag.Misc))
                    __instance.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                return false;
            }
            Messages.Message(reason ?? "HR_AncientTreeNeedsAxe".Translate().ToString(), tree,
                MessageTypeDefOf.RejectInput, false);
            __instance.pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
            return false;
        }
    }
}
