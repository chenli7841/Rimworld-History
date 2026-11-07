using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Production;
using HistoricalRimWorld.Patches.Technology;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public static class HistoricalPracticeService
    {
        public static event Action<ResearchProjectDef> HistoricalTechnologyCompleted;

        private static HistoricalPracticeGameComponent Component => Current.Game?.GetComponent<HistoricalPracticeGameComponent>();

        public static bool HasPracticeRequirements(ResearchProjectDef project)
        {
            HistoricalTechExtension extension = project?.GetModExtension<HistoricalTechExtension>();
            return extension != null && extension.RequiresPractice
                && extension.PracticeRequirementIds != null && extension.PracticeRequirementIds.Count > 0;
        }

        public static IEnumerable<string> RequirementIds(ResearchProjectDef project) =>
            (project?.GetModExtension<HistoricalTechExtension>()?.PracticeRequirementIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal);

        public static bool IsProjectAuthorized(ResearchProjectDef project) =>
            HistoricalScenarioManager.IsScenarioActive && HistoricalTechnologyManager.IsActive
            && project != null && HistoricalTechnologyManager.IsTechnologyProject(project)
            && HistoricalTechnologyManager.IsResearchAllowed(project);

        public static HistoricalPracticeProjectProgress Progress(ResearchProjectDef project, bool create = false)
        {
            if (!IsProjectAuthorized(project)) return null;
            return Component?.GetOrCreate(project, create);
        }

        public static bool IsTheoryComplete(ResearchProjectDef project) => project?.IsFinished == true
            || Progress(project)?.TheoryComplete == true;

        public static bool IsPracticeComplete(ResearchProjectDef project)
        {
            if (!HasPracticeRequirements(project)) return true;
            HistoricalPracticeProjectProgress state = Progress(project);
            if (project?.IsFinished == true) return true;
            return state != null && AllRequirementsMet(project, state);
        }

        public static bool CanComplete(ResearchProjectDef project)
        {
            if (!HasPracticeRequirements(project)) return true;
            if (!IsProjectAuthorized(project)) return false;
            HistoricalPracticeProjectProgress state = Progress(project);
            return project != null && state != null && !state.Completed && !project.IsFinished
                && state.TheoryComplete && AllRequirementsMet(project, state)
                && PrerequisitesComplete(project);
        }

        public static bool TryCompleteTechnology(ResearchProjectDef project)
        {
            if (project == null || project.IsFinished) return false;
            if (!HasPracticeRequirements(project)) return false;
            if (!CanComplete(project)) return false;
            if (Find.ResearchManager == null) return false;
            Find.ResearchManager.FinishProject(project, true, null, true);
            return project.IsFinished;
        }

        public static bool IsLearningRecipeAllowed(RecipeDef recipe, HistoricalTechnologyRequirementExtension requirement)
        {
            if (recipe == null || requirement == null) return false;
            HistoricalPracticeLearningRecipeExtension learning = recipe.GetModExtension<HistoricalPracticeLearningRecipeExtension>();
            ResearchProjectDef project = learning == null ? null
                : DefDatabase<ResearchProjectDef>.GetNamedSilentFail(learning.researchDefName);
            if (project == null || !HasPracticeRequirements(project) || !IsProjectAuthorized(project)) return false;
            return requirement.requiredTechnology == HistoricalTechnologyTimelineLayout.GetTechnology(project)?.defName
                && Progress(project, true)?.Completed != true;
        }

        internal static bool LimitResearchProgress(ResearchProjectDef project, ref float amount)
        {
            if (!HasPracticeRequirements(project)) return false;
            if (!IsProjectAuthorized(project)) return false;
            HistoricalPracticeProjectProgress state = Progress(project, true);
            if (state == null || state.Completed || project.IsFinished) return false;
            if (state.TheoryComplete) { amount = 0f; return true; }

            float current = Find.ResearchManager?.GetProgress(project) ?? 0f;
            float epsilon = Math.Max(0.01f, project.Cost * 0.000001f);
            float theoryLimit = Math.Max(0f, project.Cost - epsilon);
            if (current >= theoryLimit)
            {
                amount = 0f;
                MarkTheoryComplete(project, state);
                return true;
            }

            float requested = Math.Max(0f, amount);
            if (current + requested >= project.Cost)
                amount = Math.Min(requested, theoryLimit - current);
            return false;
        }

        internal static void OnResearchProgress(ResearchProjectDef project)
        {
            if (!HasPracticeRequirements(project) || !IsProjectAuthorized(project)) return;
            HistoricalPracticeProjectProgress state = Progress(project, true);
            if (state == null || state.Completed) return;
            float epsilon = Math.Max(0.01f, project.Cost * 0.000001f);
            if (Find.ResearchManager.GetProgress(project) >= project.Cost - epsilon - 0.0001f)
                MarkTheoryComplete(project, state);
        }

        private static void MarkTheoryComplete(ResearchProjectDef project, HistoricalPracticeProjectProgress state)
        {
            if (state.TheoryComplete || state.Completed) return;
            state.TheoryComplete = true;
            HistoricalDate date;
            if (HistoricalCalendarManager.TryGetCurrentDate(out date)) state.TheoryCompletedAstronomicalYear = date.AstronomicalYear;
            state.TheoryCompletedTick = Find.TickManager?.TicksGame ?? 0;
            if (Find.ResearchManager?.IsCurrentProject(project) == true) Find.ResearchManager.StopProject(project);
            HistoricalResearchTimelineUIState.Invalidate();
            TryCompleteTechnology(project);
            state = Progress(project) ?? state;
            if (!state.Completed && !state.TheoryNoticeSent)
            {
                state.TheoryNoticeSent = true;
                Messages.Message("HR_Practice_TheoryComplete".Translate(project.LabelCap), MessageTypeDefOf.NeutralEvent, false);
                RecordChronicle("Theory", project);
            }
        }

        public static string GetProgressDescription(ResearchProjectDef project)
        {
            if (!HasPracticeRequirements(project) || !IsProjectAuthorized(project)) return null;
            HistoricalPracticeProjectProgress state = Progress(project, true);
            if (state == null) return null;
            float researchProgress = Find.ResearchManager?.GetProgress(project) ?? 0f;
            if (state.TheoryComplete) researchProgress = project.Cost;
            List<string> lines = new List<string>
            {
                "HR_Practice_Section".Translate(),
                string.Format("HR_Practice_TheoryProgress".Translate(), researchProgress.ToString("0.#"), project.Cost.ToString("0.#"))
            };
            foreach (string id in RequirementIds(project))
            {
                HistoricalPracticeRequirementDef def = DefDatabase<HistoricalPracticeRequirementDef>.GetNamedSilentFail(id);
                HistoricalPracticeRequirementProgress progress = state.FindRequirement(id);
                if (def == null || progress == null) continue;
                if (def.requirementType == HistoricalPracticeType.CraftsmanshipExperience)
                    lines.Add(string.Format("HR_Practice_ExperienceProgress".Translate(), def.LabelCap,
                        progress.CraftsmanshipExperience.ToString("0.#"), def.requiredExperience.ToString("0.#")));
                else
                    lines.Add(string.Format("HR_Practice_CountProgress".Translate(), def.LabelCap,
                        progress.CompletedCount, def.requiredCount));
            }
            string status = state.Completed ? "HR_Practice_Status_Completed".Translate()
                : state.TheoryComplete && !AllRequirementsMet(project, state) ? "HR_Practice_Status_Awaiting".Translate()
                : state.TheoryComplete ? "HR_Practice_Status_Ready".Translate() : "HR_Practice_Status_Researching".Translate();
            lines.Add(string.Format("HR_Practice_Status".Translate(), status));
            return string.Join("\n", lines);
        }

        internal static void NotifyCraftCompleted(Pawn worker, Thing product, RecipeDef recipe)
        {
            if (worker?.Faction != Faction.OfPlayer || product == null || recipe == null
                || !HistoricalScenarioManager.IsScenarioActive || !HistoricalTechnologyManager.IsActive) return;
            if (Component?.TryRegisterCraftEvent(product) != true) return;

            CompQuality qualityComp = product.TryGetComp<CompQuality>();
            bool hasQuality = qualityComp != null;
            QualityCategory quality = hasQuality ? qualityComp.Quality : QualityCategory.Normal;
            int produced = Math.Max(1, product.stackCount);
            List<ResearchProjectDef> candidates = HistoricalTechnologyTimelineLayout.HistoricalProjects
                .Where(project => HasPracticeRequirements(project) && IsProjectAuthorized(project)).ToList();
            foreach (ResearchProjectDef project in candidates)
            {
                HistoricalPracticeProjectProgress state = Progress(project, true);
                if (state == null || state.Completed || project.IsFinished) continue;
                bool changed = false;
                foreach (string id in RequirementIds(project))
                {
                    HistoricalPracticeRequirementDef def = DefDatabase<HistoricalPracticeRequirementDef>.GetNamedSilentFail(id);
                    HistoricalPracticeRequirementProgress progress = state.FindRequirement(id);
                    if (def == null || progress == null || RequirementMet(def, progress) || !Matches(def, product)) continue;
                    if (def.hasMinimumQuality && (!hasQuality || quality < def.minimumQuality)) continue;
                    switch (def.requirementType)
                    {
                        case HistoricalPracticeType.CraftItemCount:
                            progress.CompletedCount = SaturatingAdd(progress.CompletedCount, produced);
                            changed = true;
                            break;
                        case HistoricalPracticeType.CraftItemQualityCount:
                            if (hasQuality)
                            {
                                progress.CompletedCount = SaturatingAdd(progress.CompletedCount, produced);
                                changed = true;
                            }
                            break;
                        case HistoricalPracticeType.CraftsmanshipExperience:
                            if (hasQuality)
                            {
                                progress.CraftsmanshipExperience += ExperienceFor(quality) * produced;
                                changed = true;
                            }
                            break;
                    }
                }
                if (!changed) continue;
                HistoricalResearchTimelineUIState.Invalidate();
                if (state.TheoryComplete) TryCompleteTechnology(project);
            }
        }

        public static bool AllRequirementsMet(ResearchProjectDef project, HistoricalPracticeProjectProgress state)
        {
            if (project == null || state == null) return false;
            foreach (string id in RequirementIds(project))
            {
                HistoricalPracticeRequirementDef def = DefDatabase<HistoricalPracticeRequirementDef>.GetNamedSilentFail(id);
                HistoricalPracticeRequirementProgress progress = state.FindRequirement(id);
                if (def == null || progress == null || !RequirementMet(def, progress)) return false;
            }
            return true;
        }

        private static bool RequirementMet(HistoricalPracticeRequirementDef def, HistoricalPracticeRequirementProgress progress) =>
            def.requirementType == HistoricalPracticeType.CraftsmanshipExperience
                ? progress.CraftsmanshipExperience >= def.requiredExperience
                : progress.CompletedCount >= def.requiredCount;

        private static bool Matches(HistoricalPracticeRequirementDef def, Thing product)
        {
            bool thingMatch = def.targetThingDefs != null && def.targetThingDefs.Contains(product.def);
            if (!thingMatch && def.targetTags != null && def.targetTags.Count > 0)
            {
                HistoricalPracticeTagsExtension tags = product.def.GetModExtension<HistoricalPracticeTagsExtension>();
                thingMatch = tags?.tags != null && tags.tags.Any(def.targetTags.Contains);
            }
            if (!thingMatch) return false;
            if (def.allowedMaterials != null && def.allowedMaterials.Count > 0
                && (product.Stuff == null || !def.allowedMaterials.Contains(product.Stuff))) return false;
            return true;
        }

        private static float ExperienceFor(QualityCategory quality)
        {
            HistoricalCraftsmanshipExperienceDef def = DefDatabase<HistoricalCraftsmanshipExperienceDef>.GetNamedSilentFail("HR_CraftsmanshipExperience_Default");
            return def?.For(quality) ?? 0f;
        }

        private static bool PrerequisitesComplete(ResearchProjectDef project)
        {
            if (project.prerequisites != null && project.prerequisites.Any(item => !item.IsFinished)) return false;
            return project.hiddenPrerequisites == null || project.hiddenPrerequisites.All(item => item.IsFinished);
        }

        private static int SaturatingAdd(int value, int amount) => value > int.MaxValue - amount ? int.MaxValue : value + amount;

        internal static void MarkFormalCompletion(ResearchProjectDef project)
        {
            if (project == null || !HasPracticeRequirements(project)) return;
            HistoricalPracticeProjectProgress state = Component?.GetOrCreate(project, true);
            if (state == null || state.Completed) return;
            state.Completed = true;
            state.CompletedTick = Find.TickManager?.TicksGame ?? 0;
            HistoricalDate date;
            if (HistoricalCalendarManager.TryGetCurrentDate(out date)) state.CompletedAstronomicalYear = date.AstronomicalYear;
            if (!state.BreakthroughNoticeSent)
            {
                state.BreakthroughNoticeSent = true;
                Messages.Message("HR_Practice_Breakthrough".Translate(project.LabelCap), MessageTypeDefOf.PositiveEvent, false);
                RecordChronicle("Breakthrough", project);
                HistoricalTechnologyCompleted?.Invoke(project);
            }
            HistoricalResearchTimelineUIState.Invalidate();
        }

        private static void RecordChronicle(string kind, ResearchProjectDef project)
        {
            if (!HistoricalChronicleManager.IsActive) return;
            HistoricalChronicleManager.TryAddLocalExperience("HistoricalPractice:" + kind + ":" + project.defName,
                kind == "Theory" ? "HR_Practice_Chronicle_TheoryTitle" : "HR_Practice_Chronicle_BreakthroughTitle",
                kind == "Theory" ? "HR_Practice_Chronicle_TheoryDescription" : "HR_Practice_Chronicle_BreakthroughDescription",
                null, null, HistoricalEvidenceLevel.Speculative,
                "Generated from player-owned crafting and research progress.", out _);
        }
    }
}
