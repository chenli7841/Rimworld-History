using System.Linq;
using HistoricalRimWorld.Patches.Technology;
using LudeonTK;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public static class HistoricalPracticeDebugActions
    {
        private const AllowedGameStates Playing = AllowedGameStates.PlayingOnMap;

        [DebugAction("Historical RimWorld", "HR: Show Practice Progress", allowedGameStates = Playing)]
        public static void ShowProgress()
        {
            if (!Prefs.DevMode) return;
            foreach (ResearchProjectDef project in HistoricalTechnologyTimelineLayout.HistoricalProjects.Where(HistoricalPracticeService.HasPracticeRequirements))
            {
                HistoricalPracticeProjectProgress state = Current.Game?.GetComponent<HistoricalPracticeGameComponent>()?.Find(project);
                Log.Message("[Historical RimWorld][Practice] " + project.defName + ": theory=" + (state?.TheoryComplete ?? false)
                    + ", finished=" + project.IsFinished + ", progress=" + (state == null ? "none" : string.Join("; ", state.Requirements.Select(item =>
                        item.RequirementDefName + "=" + item.CompletedCount + "/" + item.CraftsmanshipExperience.ToString("0.#")))));
            }
        }

        [DebugAction("Historical RimWorld", "HR: Add Practice Count", allowedGameStates = Playing)]
        public static void AddPracticeCount()
        {
            if (!Prefs.DevMode) return;
            SelectProject(project => SelectRequirement(project, requirement =>
            {
                HistoricalPracticeProjectProgress state = Current.Game.GetComponent<HistoricalPracticeGameComponent>().GetOrCreate(project);
                HistoricalPracticeRequirementProgress progress = state?.FindRequirement(requirement.defName);
                if (progress == null) return;
                if (requirement.requirementType == HistoricalPracticeType.CraftsmanshipExperience) progress.CraftsmanshipExperience += 1f;
                else progress.CompletedCount++;
                Log.Message("[Historical RimWorld][Practice] Added one debug progress to " + project.defName + "/" + requirement.defName + ".");
                HistoricalPracticeService.TryCompleteTechnology(project);
            }));
        }

        [DebugAction("Historical RimWorld", "HR: Reset Practice Progress", allowedGameStates = Playing)]
        public static void ResetPracticeProgress()
        {
            if (!Prefs.DevMode) return;
            SelectProject(project =>
            {
                HistoricalPracticeProjectProgress state = Current.Game.GetComponent<HistoricalPracticeGameComponent>().GetOrCreate(project);
                if (state == null) return;
                if (state.Completed || project.IsFinished)
                {
                    Log.Warning("[Historical RimWorld][Practice] Completed research cannot be reset without changing vanilla research progress: " + project.defName);
                    return;
                }
                state.TheoryComplete = false;
                state.TheoryNoticeSent = false;
                state.BreakthroughNoticeSent = false;
                foreach (HistoricalPracticeRequirementProgress requirement in state.Requirements)
                {
                    requirement.CompletedCount = 0;
                    requirement.CraftsmanshipExperience = 0f;
                }
                Log.Message("[Historical RimWorld][Practice] Reset custom practice state for " + project.defName + ". Vanilla research progress was preserved.");
            });
        }

        [DebugAction("Historical RimWorld", "HR: Complete Theory", allowedGameStates = Playing)]
        public static void CompleteTheory()
        {
            if (!Prefs.DevMode) return;
            SelectProject(project =>
            {
                HistoricalPracticeProjectProgress state = Current.Game.GetComponent<HistoricalPracticeGameComponent>().GetOrCreate(project);
                if (state == null) return;
                state.TheoryComplete = true;
                state.TheoryCompletedTick = Find.TickManager?.TicksGame ?? 0;
                Log.Message("[Historical RimWorld][Practice] Marked theory complete for " + project.defName + "; vanilla research points were not changed.");
                HistoricalPracticeService.TryCompleteTechnology(project);
            });
        }

        [DebugAction("Historical RimWorld", "HR: Complete Practice", allowedGameStates = Playing)]
        public static void CompletePractice()
        {
            if (!Prefs.DevMode) return;
            SelectProject(project =>
            {
                HistoricalPracticeProjectProgress state = Current.Game.GetComponent<HistoricalPracticeGameComponent>().GetOrCreate(project);
                if (state == null) return;
                foreach (HistoricalPracticeRequirementProgress progress in state.Requirements)
                {
                    HistoricalPracticeRequirementDef def = DefDatabase<HistoricalPracticeRequirementDef>.GetNamedSilentFail(progress.RequirementDefName);
                    if (def == null) continue;
                    if (def.requirementType == HistoricalPracticeType.CraftsmanshipExperience) progress.CraftsmanshipExperience = def.requiredExperience;
                    else progress.CompletedCount = def.requiredCount;
                }
                Log.Message("[Historical RimWorld][Practice] Debug-filled practice requirements for " + project.defName + ".");
                HistoricalPracticeService.TryCompleteTechnology(project);
            });
        }

        [DebugAction("Historical RimWorld", "HR: Force Technology Breakthrough", allowedGameStates = Playing)]
        public static void ForceBreakthrough()
        {
            if (!Prefs.DevMode) return;
            SelectProject(project =>
            {
                HistoricalPracticeProjectProgress state = Current.Game.GetComponent<HistoricalPracticeGameComponent>().GetOrCreate(project);
                if (state == null) return;
                state.TheoryComplete = true;
                foreach (HistoricalPracticeRequirementProgress progress in state.Requirements)
                {
                    HistoricalPracticeRequirementDef def = DefDatabase<HistoricalPracticeRequirementDef>.GetNamedSilentFail(progress.RequirementDefName);
                    if (def == null) continue;
                    if (def.requirementType == HistoricalPracticeType.CraftsmanshipExperience) progress.CraftsmanshipExperience = def.requiredExperience;
                    else progress.CompletedCount = def.requiredCount;
                }
                if (!HistoricalPracticeService.TryCompleteTechnology(project))
                    Log.Warning("[Historical RimWorld][Practice] Force breakthrough bypasses practice only; authorization, prerequisites and an active game scenario still apply.");
            });
        }

        private static void SelectProject(System.Action<ResearchProjectDef> action)
        {
            var options = HistoricalTechnologyTimelineLayout.HistoricalProjects
                .Where(HistoricalPracticeService.HasPracticeRequirements)
                .Select(project => new FloatMenuOption(project.LabelCap.ToString(), () => action(project))).ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void SelectRequirement(ResearchProjectDef project, System.Action<HistoricalPracticeRequirementDef> action)
        {
            var options = HistoricalPracticeService.RequirementIds(project)
                .Select(DefDatabase<HistoricalPracticeRequirementDef>.GetNamedSilentFail).Where(def => def != null)
                .Select(def => new FloatMenuOption(def.LabelCap.ToString(), () => action(def))).ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
