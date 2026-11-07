using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Patches.Technology;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public sealed class HistoricalPracticeGameComponent : GameComponent
    {
        private List<HistoricalPracticeProjectProgress> projects = new List<HistoricalPracticeProjectProgress>();
        private List<int> recentlyProcessedCraftThingIds = new List<int>();

        public HistoricalPracticeGameComponent(Game game) { }

        public IEnumerable<HistoricalPracticeProjectProgress> Projects => projects ?? Enumerable.Empty<HistoricalPracticeProjectProgress>();

        public HistoricalPracticeProjectProgress Find(ResearchProjectDef project) => project == null
            ? null : projects?.FirstOrDefault(item => item != null && item.ResearchDefName == project.defName);

        public bool TryRegisterCraftEvent(Thing product)
        {
            if (product == null) return false;
            int id = product.thingIDNumber;
            if (id <= 0) return true;
            if (recentlyProcessedCraftThingIds == null) recentlyProcessedCraftThingIds = new List<int>();
            if (recentlyProcessedCraftThingIds.Contains(id)) return false;
            recentlyProcessedCraftThingIds.Add(id);
            const int retainedEvents = 512;
            if (recentlyProcessedCraftThingIds.Count > retainedEvents)
                recentlyProcessedCraftThingIds.RemoveRange(0, recentlyProcessedCraftThingIds.Count - retainedEvents);
            return true;
        }

        public HistoricalPracticeProjectProgress GetOrCreate(ResearchProjectDef project, bool create = true)
        {
            if (!HistoricalPracticeService.HasPracticeRequirements(project)) return null;
            HistoricalPracticeProjectProgress state = Find(project);
            if (state != null || !create) return state;
            state = new HistoricalPracticeProjectProgress { ResearchDefName = project.defName };
            HistoricalDate date;
            state.PracticeStartedTick = Verse.Find.TickManager?.TicksGame ?? 0;
            if (HistoricalCalendarManager.TryGetCurrentDate(out date)) state.PracticeStartedAstronomicalYear = date.AstronomicalYear;
            foreach (string id in HistoricalPracticeService.RequirementIds(project))
                state.Requirements.Add(new HistoricalPracticeRequirementProgress { RequirementDefName = id });
            if (projects == null) projects = new List<HistoricalPracticeProjectProgress>();
            projects.Add(state);

            // Preserve research already completed in saves made before practice requirements existed.
            if (project.IsFinished) MarkCompleted(state, project, true);
            return state;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref projects, "historicalPracticeProjects", LookMode.Deep);
            Scribe_Collections.Look(ref recentlyProcessedCraftThingIds, "historicalPracticeRecentCraftThingIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                projects = projects ?? new List<HistoricalPracticeProjectProgress>();
                recentlyProcessedCraftThingIds = recentlyProcessedCraftThingIds ?? new List<int>();
                if (recentlyProcessedCraftThingIds.Count > 512)
                    recentlyProcessedCraftThingIds.RemoveRange(0, recentlyProcessedCraftThingIds.Count - 512);
                projects.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.ResearchDefName));
                foreach (HistoricalPracticeProjectProgress state in projects)
                {
                    state.Requirements = state.Requirements ?? new List<HistoricalPracticeRequirementProgress>();
                    state.Requirements.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.RequirementDefName));
                    ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(state.ResearchDefName);
                    if (project?.IsFinished == true) MarkCompleted(state, project, true);
                }
            }
        }

        private static void MarkCompleted(HistoricalPracticeProjectProgress state, ResearchProjectDef project, bool migrated)
        {
            if (state.Completed) return;
            state.Completed = true;
            state.MigratedFromCompletedResearch = migrated;
            HistoricalDate date;
            if (HistoricalCalendarManager.TryGetCurrentDate(out date)) state.CompletedAstronomicalYear = date.AstronomicalYear;
        }
    }

    public sealed class HistoricalPracticeProjectProgress : IExposable
    {
        public string ResearchDefName;
        public List<HistoricalPracticeRequirementProgress> Requirements = new List<HistoricalPracticeRequirementProgress>();
        public bool TheoryComplete;
        public bool Completed;
        public bool TheoryNoticeSent;
        public bool BreakthroughNoticeSent;
        public bool MigratedFromCompletedResearch;
        public int PracticeStartedTick;
        public int PracticeStartedAstronomicalYear;
        public int TheoryCompletedTick = -1;
        public int TheoryCompletedAstronomicalYear = int.MinValue;
        public int CompletedTick = -1;
        public int CompletedAstronomicalYear = int.MinValue;

        public HistoricalPracticeRequirementProgress FindRequirement(string id) =>
            Requirements?.FirstOrDefault(item => item != null && item.RequirementDefName == id);

        public void ExposeData()
        {
            Scribe_Values.Look(ref ResearchDefName, "researchDefName");
            Scribe_Collections.Look(ref Requirements, "requirements", LookMode.Deep);
            Scribe_Values.Look(ref TheoryComplete, "theoryComplete", false);
            Scribe_Values.Look(ref Completed, "completed", false);
            Scribe_Values.Look(ref TheoryNoticeSent, "theoryNoticeSent", false);
            Scribe_Values.Look(ref BreakthroughNoticeSent, "breakthroughNoticeSent", false);
            Scribe_Values.Look(ref MigratedFromCompletedResearch, "migratedFromCompletedResearch", false);
            Scribe_Values.Look(ref PracticeStartedTick, "practiceStartedTick", 0);
            Scribe_Values.Look(ref PracticeStartedAstronomicalYear, "practiceStartedAstronomicalYear", 0);
            Scribe_Values.Look(ref TheoryCompletedTick, "theoryCompletedTick", -1);
            Scribe_Values.Look(ref TheoryCompletedAstronomicalYear, "theoryCompletedAstronomicalYear", int.MinValue);
            Scribe_Values.Look(ref CompletedTick, "completedTick", -1);
            Scribe_Values.Look(ref CompletedAstronomicalYear, "completedAstronomicalYear", int.MinValue);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Requirements = Requirements ?? new List<HistoricalPracticeRequirementProgress>();
        }
    }

    public sealed class HistoricalPracticeRequirementProgress : IExposable
    {
        public string RequirementDefName;
        public int CompletedCount;
        public float CraftsmanshipExperience;
        public void ExposeData()
        {
            Scribe_Values.Look(ref RequirementDefName, "requirementDefName");
            Scribe_Values.Look(ref CompletedCount, "completedCount", 0);
            Scribe_Values.Look(ref CraftsmanshipExperience, "craftsmanshipExperience", 0f);
        }
    }
}
