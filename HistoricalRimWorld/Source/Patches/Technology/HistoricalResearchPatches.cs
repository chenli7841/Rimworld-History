using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Production;
using HistoricalRimWorld.Technology;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.Patches.Technology
{
    [HarmonyPatch(typeof(MainTabWindow_Research), "get_VisibleResearchProjects")]
    internal static class HistoricalResearchTabPatch
    {
        private static readonly HashSet<string> HiddenResearchRoots = new HashSet<string>
        {
            "Electricity",
            "Machining"
        };

        private static void Postfix(MainTabWindow_Research __instance, ref List<ResearchProjectDef> __result)
        {
            if (__result == null) return;

            if (HistoricalResearchTimelineUIState.IsHistoricalTab(__instance))
            {
                __result = HistoricalResearchTimelineUIState.VisibleProjects();
                return;
            }

            if (HistoricalTechnologyManager.IsActive)
                __result.RemoveAll(project => !HistoricalTechnologyManager.IsTechnologyProject(project)
                    && !HistoricalTechnologyManager.IsResearchAllowed(project));

            // Keep the historical scenario's main research tab within the intended era.
            // Descendants are discovered from the game's actual prerequisite graph, including
            // hidden prerequisites, so DLC and modded projects are handled without hardcoding names.
            if (HistoricalScenarioManager.IsScenarioActive)
            {
                HashSet<ResearchProjectDef> hidden = FindHiddenProjects();
                __result.RemoveAll(project => hidden.Contains(project)
                    && !HistoricalTechnologyManager.IsTechnologyProject(project));
            }
        }

        private static HashSet<ResearchProjectDef> FindHiddenProjects()
        {
            HashSet<ResearchProjectDef> hidden = new HashSet<ResearchProjectDef>();
            List<ResearchProjectDef> allProjects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;

            foreach (ResearchProjectDef project in allProjects)
            {
                if (project != null && HiddenResearchRoots.Contains(project.defName))
                    hidden.Add(project);
            }

            bool changed;
            do
            {
                changed = false;
                foreach (ResearchProjectDef project in allProjects)
                {
                    if (project == null || hidden.Contains(project)) continue;
                    if (HasHiddenPrerequisite(project.prerequisites, hidden)
                        || HasHiddenPrerequisite(project.hiddenPrerequisites, hidden))
                    {
                        hidden.Add(project);
                        changed = true;
                    }
                }
            } while (changed);

            return hidden;
        }

        private static bool HasHiddenPrerequisite(List<ResearchProjectDef> prerequisites, HashSet<ResearchProjectDef> hidden)
        {
            return prerequisites != null && prerequisites.Any(hidden.Contains);
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject))]
    internal static class HistoricalSetResearchPatch
    {
        private static bool Prefix(ResearchProjectDef proj)
        {
            if (!HistoricalTechnologyManager.IsActive || proj == null) return true;
            if (!HistoricalTechnologyManager.IsResearchAllowed(proj))
            {
                if (HistoricalTechnologyManager.IsTechnologyProject(proj)
                    && !HistoricalTechnologyManager.CanStartHistoricalResearch(proj, out string reason))
                    Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return false;
            }
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
        private static bool Prefix(ResearchProjectDef proj, ref float amount)
        {
            if (HistoricalTechnologyManager.IsActive && !HistoricalTechnologyManager.IsResearchAllowed(proj)) return false;
            if (HistoricalTechnologyManager.IsActive && HistoricalPracticeService.HasPracticeRequirements(proj))
                return !HistoricalPracticeService.LimitResearchProgress(proj, ref amount);
            return true;
        }
        private static void Postfix(ResearchProjectDef proj)
        {
            if (HistoricalTechnologyManager.IsActive && proj != null)
            {
                HistoricalTechnologyManager.RecordResearchProgress(proj, Find.ResearchManager.GetProgress(proj));
                HistoricalPracticeService.OnResearchProgress(proj);
            }
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    internal static class HistoricalPracticeFinishGatePatch
    {
        private static bool Prefix(ResearchProjectDef proj)
        {
            if (!HistoricalTechnologyManager.IsActive || !HistoricalPracticeService.HasPracticeRequirements(proj)) return true;
            // Existing completed research is grandfathered; all new completion routes must pass practice.
            if (proj?.IsFinished == true) return true;
            return HistoricalPracticeService.CanComplete(proj);
        }

        private static void Postfix(ResearchProjectDef proj)
        {
            if (HistoricalTechnologyManager.IsActive && proj?.IsFinished == true)
                HistoricalPracticeService.MarkFormalCompletion(proj);
        }
    }

    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    internal static class HistoricalCraftCompletedPatch
    {
        private static void Postfix(Thing __0, RecipeDef __1, Pawn __2)
        {
            HistoricalPracticeService.NotifyCraftCompleted(__2, __0, __1);
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
            ThingDef thing = __instance.PlacingDef as ThingDef;
            string type = "null";
            if (thing != null)
            {
                type = thing.techLevel.ToString();
            }
            if (!HistoricalTechnologyManager.IsActive || HistoricalTechnologyManager.IsBuildAllowed(__instance.PlacingDef)) return true;
            __result = "This building requires an adopted historical technology. " + type;
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
        private static void Postfix(ThingSetMakerParams parms, List<Thing> outThings)
        {
            if (outThings != null && HistoricalTechnologyManager.IsActive)
            {
                outThings.RemoveAll(item => item == null || !HistoricalTechnologyManager.IsThingAllowed(item.def));
                HistoricalBlueprintService.TryAppendTraderBlueprints(parms.makingFaction, outThings);
            }
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "get_LabelNoCount")]
    internal static class HistoricalBlueprintLabelPatch
    {
        private static void Postfix(ThingWithComps __instance, ref string __result)
        {
            CompHistoricalBlueprint blueprint = __instance?.TryGetComp<CompHistoricalBlueprint>();
            ResearchProjectDef research = blueprint?.Research;
            if (research != null) __result = "HR_Blueprint_Label".Translate(research.LabelCap).ToString();
        }
    }

    [HarmonyPatch(typeof(Settlement_TraderTracker), "RegenerateStock")]
    internal static class HistoricalSettlementBlueprintStockPatch
    {
        private static void Postfix(Settlement_TraderTracker __instance)
        {
            if (!HistoricalTechnologyManager.IsActive || __instance?.settlement?.Faction == null) return;
            HistoricalBlueprintService.TryAppendSettlementBlueprint(__instance.settlement.Faction,
                __instance.StockListForReading);
        }
    }
}
