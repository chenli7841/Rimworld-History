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

        [DebugAction("Historical RimWorld", "HR: Spawn Technology Blueprint", allowedGameStates = Playing)]
        public static void SpawnTechnologyBlueprint()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            OpenProjectMenu(project =>
            {
                Thing blueprint = HistoricalBlueprintService.CreateBlueprint(project);
                if (blueprint != null) GenPlace.TryPlaceThing(blueprint, Find.CurrentMap.Center, Find.CurrentMap, ThingPlaceMode.Near);
                else Log.Warning("[Historical RimWorld][Tech Debug] Blueprint cannot be created for " + project.defName + ".");
            }, "HR: Spawn blueprint ");
        }

        [DebugAction("Historical RimWorld", "HR: Grant Research Authorization", allowedGameStates = Playing)]
        public static void GrantResearchAuthorization()
        {
            if (!Prefs.DevMode) return;
            OpenProjectMenu(project =>
            {
                if (HistoricalBlueprintService.TryGrantAuthorization(project, HistoricalResearchAuthorizationSource.SpecialEvent, "Developer action", out string error))
                    Log.Message("[Historical RimWorld][Tech Debug] Granted authorization for " + project.defName + ".");
                else Log.Warning("[Historical RimWorld][Tech Debug] Authorization not granted: " + error);
            }, "HR: Grant authorization ");
        }

        [DebugAction("Historical RimWorld", "HR: Revoke Research Authorization", allowedGameStates = Playing)]
        public static void RevokeResearchAuthorization()
        {
            if (!Prefs.DevMode || HistoricalBlueprintService.Component == null) return;
            OpenProjectMenu(project => Log.Message("[Historical RimWorld][Tech Debug] Revoke " + project.defName + ": "
                + HistoricalBlueprintService.Component.RevokeAuthorization(project.defName)), "HR: Revoke authorization ");
        }

        [DebugAction("Historical RimWorld", "HR: Show Research Authorization", allowedGameStates = Playing)]
        public static void ShowResearchAuthorization()
        {
            if (!Prefs.DevMode) return;
            foreach (ResearchProjectDef project in HistoricalTechnologyTimelineLayout.HistoricalProjects)
            {
                HistoricalResearchAuthorization record = HistoricalBlueprintService.Component?.FindAuthorization(project.defName);
                Log.Message("[Historical RimWorld][Tech Debug] Research=" + project.defName + "; status="
                    + HistoricalBlueprintService.GetStatus(project) + "; authorized=" + (record?.IsAuthorized ?? false)
                    + "; source=" + (record?.SourceDetails ?? "none") + "; reason="
                    + (HistoricalBlueprintService.CanStartHistoricalResearch(project, out string reason) ? "allowed" : reason));
            }
        }

        [DebugAction("Historical RimWorld", "HR: List Available Blueprint Sources", allowedGameStates = Playing)]
        public static void ListAvailableBlueprintSources()
        {
            if (!Prefs.DevMode) return;
            foreach (ResearchProjectDef project in HistoricalTechnologyTimelineLayout.HistoricalProjects)
            {
                List<string> sources = HistoricalTechnologyManager.ComponentFactionStates
                    .Where(state => state != null && !state.IsPlayerFaction)
                    .Select(state => HistoricalTechnologyManager.ResolveFaction(state))
                    .Where(faction => faction != null && HistoricalBlueprintService.CanFactionSellBlueprint(faction, project))
                    .Select(faction => faction.Name).ToList();
                Log.Message("[Historical RimWorld][Tech Debug] " + project.defName + " sellers=[" + string.Join(", ", sources)
                    + "]; eligible=" + (sources.Count > 0) + "; reason="
                    + (sources.Count > 0 ? "eligible" : "no known mastered trading faction or the technology has not emerged"));
            }
        }

        [DebugAction("Historical RimWorld", "HR: Force Caravan Blueprint Stock", allowedGameStates = Playing)]
        public static void ForceCaravanBlueprintStock()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            List<Faction> sellers = HistoricalTechnologyManager.ComponentFactionStates
                .Where(state => state != null && !state.IsPlayerFaction)
                .Select(state => HistoricalTechnologyManager.ResolveFaction(state)).Where(faction => faction != null).ToList();
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Faction seller in sellers)
            foreach (ResearchProjectDef project in HistoricalTechnologyTimelineLayout.HistoricalProjects.Where(item => HistoricalBlueprintService.CanFactionSellBlueprint(seller, item)))
                options.Add(new FloatMenuOption(seller.Name + " — " + project.LabelCap, () =>
                {
                    Thing blueprint = HistoricalBlueprintService.CreateBlueprint(project, seller);
                    if (blueprint != null) GenPlace.TryPlaceThing(blueprint, Find.CurrentMap.Center, Find.CurrentMap, ThingPlaceMode.Near);
                }));
            if (options.Count == 0) Log.Warning("[Historical RimWorld][Tech Debug] No eligible faction can supply a blueprint.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Historical RimWorld", "HR: Complete Blueprint Decoding", allowedGameStates = Playing)]
        public static void CompleteBlueprintDecoding()
        {
            if (!Prefs.DevMode) return;
            List<CompHistoricalBlueprint> blueprints = HistoricalBlueprintService.EnumerateAccessibleBlueprints().ToList();
            List<FloatMenuOption> options = blueprints.Select(item => new FloatMenuOption(item.Research.LabelCap.ToString(), () =>
            {
                if (!HistoricalBlueprintService.TryCompleteDecoding(item.parent, out string error))
                    Log.Warning("[Historical RimWorld][Tech Debug] Could not force decoding: " + error);
            })).ToList();
            if (options.Count == 0) Log.Warning("[Historical RimWorld][Tech Debug] No player-owned blueprint is available to decode.");
            else Find.WindowStack.Add(new FloatMenu(options));
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
