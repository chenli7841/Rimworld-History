using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Production;
using HistoricalRimWorld.Technology;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.Technology
{
    [HarmonyPatch(typeof(MainTabWindow_Research), "get_VisibleResearchProjects")]
    internal static class HistoricalResearchTabPatch
    {
        private static void Postfix(ref List<ResearchProjectDef> __result)
        {
            if (__result != null && HistoricalTechnologyManager.IsActive)
                __result.RemoveAll(project => !HistoricalTechnologyManager.IsResearchAllowed(project));
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject))]
    internal static class HistoricalSetResearchPatch
    {
        private static bool Prefix(ResearchProjectDef proj)
        {
            if (!HistoricalTechnologyManager.IsActive || proj == null) return true;
            if (!HistoricalTechnologyManager.IsResearchAllowed(proj)) return false;
            if (HistoricalTechnologyManager.IsTechnologyProject(proj))
            {
                HistoricalTechnologyDef tech = HistoricalTechnologyManager.Technologies.FirstOrDefault(item => item.researchProject == proj);
                return tech == null || HistoricalTechnologyManager.TrySetUnderStudy(HistoricalTechnologyManager.GetPlayerCommunityId(), tech.defName, out _);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.AddProgress))]
    internal static class HistoricalResearchProgressPatch
    {
        private static bool Prefix(ResearchProjectDef proj) => !HistoricalTechnologyManager.IsActive || HistoricalTechnologyManager.IsResearchAllowed(proj);
        private static void Postfix(ResearchProjectDef proj)
        {
            if (HistoricalTechnologyManager.IsActive && proj != null)
                HistoricalTechnologyManager.RecordResearchProgress(proj, Find.ResearchManager.GetProgress(proj));
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    internal static class HistoricalResearchFinishedPatch
    {
        private static void Postfix(ResearchProjectDef proj)
        {
            if (!HistoricalTechnologyManager.IsActive || proj == null) return;
            HistoricalTechnologyDef tech = HistoricalTechnologyManager.Technologies.FirstOrDefault(item => item.researchProject == proj);
            if (tech != null)
            {
                HistoricalTechnologyManager.RecordResearchProgress(proj, proj.baseCost);
                HistoricalTechnologyManager.TryAdoptTechnology(HistoricalTechnologyManager.GetPlayerCommunityId(), tech.defName,
                    HistoricalTechnologyAcquisitionSource.Research, out _);
            }
        }
    }

    [HarmonyPatch(typeof(BuildableDef), "get_BuildableByPlayer")]
    internal static class HistoricalBuildablePatch
    {
        private static bool Prefix(BuildableDef __instance, ref bool __result)
        {
            if (!HistoricalTechnologyManager.IsActive || HistoricalTechnologyManager.IsBuildAllowed(__instance)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.CanDesignateCell))]
    internal static class HistoricalBuildPlacementPatch
    {
        private static bool Prefix(Designator_Build __instance, ref AcceptanceReport __result)
        {
            if (!HistoricalTechnologyManager.IsActive || HistoricalTechnologyManager.IsBuildAllowed(__instance.PlacingDef)) return true;
            __result = "This building requires an adopted historical technology.";
            return false;
        }
    }

    [HarmonyPatch(typeof(RecipeDef), "get_AvailableNow")]
    internal static class HistoricalRecipeAvailablePatch
    {
        private static bool Prefix(RecipeDef __instance, ref bool __result)
        {
            if (!HistoricalTechnologyManager.IsActive || HistoricalTechnologyManager.IsRecipeAllowed(__instance)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ShouldDoNow))]
    internal static class HistoricalBillExecutionPatch
    {
        private static bool Prefix(Bill_Production __instance, ref bool __result)
        {
            if (!HistoricalTechnologyManager.IsActive || HistoricalTechnologyManager.IsRecipeAllowed(__instance.recipe)) return true;
            __result = false;
            return false;
        }
    }

    // Covers item generation routed through the vanilla trader stock maker. Other reward and raid
    // generators have distinct pipelines and are intentionally not represented as covered here.
    [HarmonyPatch(typeof(ThingSetMaker_TraderStock), nameof(ThingSetMaker_TraderStock.Generate))]
    internal static class HistoricalTraderStockPatch
    {
        private static void Postfix(List<Thing> outThings)
        {
            if (outThings != null && HistoricalTechnologyManager.IsActive)
                outThings.RemoveAll(item => item == null || !HistoricalTechnologyManager.IsThingAllowed(item.def));
        }
    }
}
