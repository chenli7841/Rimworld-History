using HarmonyLib;
using HistoricalRimWorld.Production;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Research
{
    internal static class HR_ResearchCurrentProjectState
    {
        public static ResearchProjectDef CurrentProject { get; private set; }

        public static void Set(ResearchProjectDef project) => CurrentProject = project;
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.SetCurrentProject))]
    internal static class HR_ResearchCurrentProjectPatch
    {
        private static void Postfix(ResearchProjectDef proj) => HR_ResearchCurrentProjectState.Set(proj);
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ExposeData))]
    internal static class HR_ResearchCurrentProjectLoadPatch
    {
        private static void Postfix(ResearchProjectDef ___currentProj)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                HR_ResearchCurrentProjectState.Set(___currentProj);
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.StopProject))]
    internal static class HR_ResearchCurrentProjectStopPatch
    {
        private static void Postfix(ResearchProjectDef ___currentProj) => HR_ResearchCurrentProjectState.Set(___currentProj);
    }

    [HarmonyPatch(typeof(WorkGiver_Researcher), nameof(WorkGiver_Researcher.HasJobOnThing))]
    internal static class HR_ResearchWorkGiverPatch
    {
        private static bool Prefix(Pawn pawn, Thing t, ref bool __result)
        {
            if (!HistoricalScenarioManager.IsScenarioActive) return true;
            ResearchProjectDef project = HR_ResearchCurrentProjectState.CurrentProject;
            if (HR_ResearchUtility.GetExtension(project) == null) return true;

            Building_ResearchBench bench = t as Building_ResearchBench;
            if (bench != null && HR_ResearchUtility.CanResearchAt(pawn, project, bench)) return true;

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.AddProgress))]
    internal static class HR_ResearchProgressPatch
    {
        private static bool Prefix(ResearchProjectDef proj, ref float amount, Pawn source)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || source == null
                || HR_ResearchUtility.GetExtension(proj) == null)
                return true;

            Building_ResearchBench bench = source.CurJob?.targetA.Thing as Building_ResearchBench;
            if (bench != null && !HR_ResearchUtility.CanResearchAt(source, proj, bench))
            {
                amount = 0f;
                return false;
            }

            if (bench == null) return true;
            amount *= HR_ResearchUtility.SkillReplacementFactor(source, proj);
            amount *= HR_ResearchUtility.FacilityAndRackFactor(bench);
            return true;
        }
    }

    [HarmonyPatch(typeof(BuildableDef), "get_BuildableByPlayer")]
    internal static class HR_ResearchFacilityAvailabilityPatch
    {
        private static bool Prefix(BuildableDef __instance, ref bool __result)
        {
            if (__instance == null) return true;

            HR_ResearchFacilityExtension extension = (__instance as ThingDef)?.GetModExtension<HR_ResearchFacilityExtension>();
            if (extension != null && extension.historicalScenarioOnly && !HistoricalScenarioManager.IsScenarioActive)
            {
                __result = false;
                return false;
            }

            if (HistoricalScenarioManager.IsScenarioActive && HR_ResearchUtility.IsModernResearchBench(__instance))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    internal static class HR_ResearchFacilitySpawnPatch
    {
        private static void Postfix(Thing __instance, Map map)
        {
            if (HR_ResearchUtility.IsHistoricalFacility(__instance?.def)
                || __instance?.def?.defName == "HR_ScrollStorageRack")
                HR_ResearchUtility.InvalidateMapCache(map);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    internal static class HR_ResearchFacilityDespawnPatch
    {
        private static void Prefix(Thing __instance)
        {
            if (HR_ResearchUtility.IsHistoricalFacility(__instance?.def)
                || __instance?.def?.defName == "HR_ScrollStorageRack")
                HR_ResearchUtility.InvalidateMapCache(__instance.Map);
        }
    }
}
