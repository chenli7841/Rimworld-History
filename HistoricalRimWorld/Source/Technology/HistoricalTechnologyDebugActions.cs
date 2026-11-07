using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using LudeonTK;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public static class HistoricalTechnologyDebugActions
    {
        private const AllowedGameStates Playing = AllowedGameStates.PlayingOnMap;

        [DebugAction("Historical RimWorld", "HR: Show World Technology Status", allowedGameStates = Playing)]
        public static void ShowWorldTechnologyStatus()
        {
            if (!Prefs.DevMode) return;
            HistoricalDate date;
            string currentYear = HistoricalCalendarManager.TryGetCurrentDate(out date)
                ? HistoricalTechnologyTimelineLayout.FormatYear(date.AstronomicalYear) : "inactive";
            foreach (ResearchProjectDef project in HistoricalTechnologyTimelineLayout.HistoricalProjects.OrderBy(item => item.defName))
            {
                HistoricalWorldTechStatus status = HistoricalTechnologyManager.GetWorldStatus(project);
                int mastered = HistoricalTechnologyManager.ComponentFactionStates.Count(state => !state.IsPlayerFaction
                    && state.Find(project.defName)?.Level == FactionTechKnowledgeLevel.Mastered);
                Log.Message("[Historical RimWorld][Tech Debug] Technology=" + project.defName + "; CurrentYear=" + currentYear
                    + "; WorldStatus=" + status + "; MasteredFactions=" + mastered
                    + "; PlayerAwareness=" + HistoricalTechnologyManager.ComponentAwareness(project)
                    + "; EmergenceNoticeSent=" + (HistoricalTechnologyManager.ComponentAwarenessRecord(project)?.EmergenceNoticeSent ?? false));
            }
        }

        [DebugAction("Historical RimWorld", "HR: Show Faction Technology Status", allowedGameStates = Playing)]
        public static void ShowFactionTechnologyStatus()
        {
            if (!Prefs.DevMode) return;
            foreach (HistoricalFactionTechnologyState faction in HistoricalTechnologyManager.ComponentFactionStates)
            foreach (HistoricalFactionTechRecord technology in faction.Technologies.Where(item => item.Level != FactionTechKnowledgeLevel.Unknown))
                Log.Message("[Historical RimWorld][Tech Debug] Faction=" + faction.StableFactionId + "; Research="
                    + technology.ResearchDefName + "; Level=" + technology.Level + "; FirstKnown="
                    + technology.FirstKnownAstronomicalYear + "; Mastered=" + technology.MasteredAstronomicalYear
                    + "; Source=" + technology.AcquisitionSource);
        }

        [DebugAction("Historical RimWorld", "HR: Force Technology Emergence", allowedGameStates = Playing)]
        public static void ForceTechnologyEmergence()
        {
            if (!Prefs.DevMode) return;
            OpenProjectMenu(project => HistoricalTechnologyDiffusionService.ForceEmergence(project), "HR: Emerge ");
        }

        [DebugAction("Historical RimWorld", "HR: Force Technology Diffusion", allowedGameStates = Playing)]
        public static void ForceTechnologyDiffusion()
        {
            if (!Prefs.DevMode) return;
            OpenProjectMenu(project => Log.Message("[Historical RimWorld][Tech Debug] Diffusion changed records: "
                + HistoricalTechnologyDiffusionService.ForceDiffusion(project) + " for " + project.defName), "HR: Diffuse ");
        }

        [DebugAction("Historical RimWorld", "HR: Reveal Historical Technology", allowedGameStates = Playing)]
        public static void RevealHistoricalTechnology()
        {
            if (!Prefs.DevMode) return;
            OpenProjectMenu(project => HistoricalTechnologyDiffusionService.RevealToPlayer(project), "HR: Reveal ");
        }

        [DebugAction("Historical RimWorld", "HR: Simulate 10 Years of Diffusion", allowedGameStates = Playing)]
        public static void SimulateTenYearsOfDiffusion()
        {
            if (!Prefs.DevMode) return;
            int intervals = HistoricalTechnologyDiffusionService.SimulateYears(10);
            Log.Message("[Historical RimWorld][Tech Debug] Simulated ten historical years through " + intervals
                + " diffusion intervals. Game calendar was not advanced; notification spam was suppressed.");
        }

        [DebugAction("Historical RimWorld", "HR: Rebuild Technology State", allowedGameStates = Playing)]
        public static void RebuildTechnologyState()
        {
            if (!Prefs.DevMode) return;
            if (HistoricalTechnologyManager.RebuildMissingFactionStates(out string error))
                Log.Message("[Historical RimWorld][Tech Debug] Missing faction technology records rebuilt; existing history preserved.");
            else Log.Warning("[Historical RimWorld][Tech Debug] Could not rebuild technology state: " + error);
        }

        private static void OpenProjectMenu(System.Action<ResearchProjectDef> action, string prefix)
        {
            List<FloatMenuOption> options = HistoricalTechnologyTimelineLayout.HistoricalProjects
                .OrderBy(project => project.LabelCap.ToString())
                .Select(project => new FloatMenuOption(prefix + project.LabelCap, () => action(project)))
                .ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
