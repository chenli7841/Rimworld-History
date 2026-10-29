using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Production;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public static class HistoricalTechnologyManager
    {
        public static bool IsActive => Component?.Initialized == true && HistoricalScenarioManager.IsScenarioActive;
        public static IEnumerable<HistoricalTechnologyDef> Technologies => DefDatabase<HistoricalTechnologyDef>.AllDefsListForReading;
        public static IEnumerable<HistoricalTechnologyState> PlayerTechnologies => GetCommunityState(GetPlayerCommunityId())?.Technologies ?? Enumerable.Empty<HistoricalTechnologyState>();
        private static HistoricalTechnologyGameComponent Component => Current.Game?.GetComponent<HistoricalTechnologyGameComponent>();

        public static bool TryInitializeForScenario(HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (scenario == null || HistoricalScenarioManager.CurrentScenario?.defName != scenario.defName)
                return Fail("Scenario must be active before technology is initialized.", out error);
            HistoricalTechnologyGameComponent component = Component;
            if (component == null) return Fail("HistoricalTechnologyGameComponent is unavailable.", out error);
            if (component.Initialized)
                return component.ScenarioDefName == scenario.defName || Fail("Technology state belongs to another scenario.", out error);

            List<HistoricalTechnologySetupDef> setups = DefDatabase<HistoricalTechnologySetupDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == scenario.defName).ToList();
            if (setups.Count != 1) return Fail("Expected exactly one technology setup for " + scenario.defName + ".", out error);
            HistoricalTechnologySetupDef setup = setups[0];
            if (string.IsNullOrWhiteSpace(setup.communityId) || setup.technologies == null)
                return Fail("Technology setup requires communityId and technologies.", out error);

            HistoricalDate date = scenario.GetStartDate();
            List<HistoricalTechnologyInitialState> initialStates = new List<HistoricalTechnologyInitialState>(setup.technologies);
            foreach (HistoricalTechnologyDef technology in Technologies)
            {
                HistoricalTechnologyInitialState record = initialStates.FirstOrDefault(item => item?.technology?.defName == technology.defName);
                if (record != null) continue;
                HistoricalTechnologyAvailability availability = technology.earliestPossibleYear.HasValue && date.AstronomicalYear < technology.earliestPossibleYear.Value
                    ? HistoricalTechnologyAvailability.NotYetHistoricallyAvailable
                    : technology.initialKnownRegions != null && technology.initialKnownRegions.Contains(scenario.defaultStartingRegion)
                        ? HistoricalTechnologyAvailability.LocallyAccessible
                        : HistoricalTechnologyAvailability.ExistsElsewhere;
                initialStates.Add(new HistoricalTechnologyInitialState { technology = technology, availability = availability, adoption = HistoricalTechnologyAdoption.Unknown });
            }
            if (!component.Initialize(scenario.defName, setup.communityId, initialStates))
                return Fail("Could not initialize community technology records.", out error);
            return true;
        }

        public static string GetPlayerCommunityId()
        {
            HistoricalCommunityTechnologyState saved = Component?.Communities?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(saved?.CommunityId)) return saved.CommunityId;
            return HistoricalPoliticsManager.Communities.FirstOrDefault()?.CommunityId;
        }

        public static bool CanStudy(string communityId, string technologyId)
        {
            HistoricalTechnologyDef def = DefDatabase<HistoricalTechnologyDef>.GetNamedSilentFail(technologyId);
            if (!IsActive || def == null || string.IsNullOrWhiteSpace(communityId)) return false;
            HistoricalTechnologyState state = GetOrCreateState(communityId, def);
            if (state == null || state.Adoption == HistoricalTechnologyAdoption.Adopted
                || state.Availability == HistoricalTechnologyAvailability.NotYetHistoricallyAvailable
                || state.Availability != HistoricalTechnologyAvailability.LocallyAccessible) return false;
            return PrerequisitesAdopted(communityId, def);
        }

        public static bool TryMakeTechnologyAccessible(string communityId, string technologyId, HistoricalTechnologyAcquisitionSource source, out string error)
        {
            if (!TryGetUsableState(communityId, technologyId, out HistoricalTechnologyDef def, out HistoricalTechnologyState state, out error)) return false;
            if (!IsHistoricallyAvailable(def)) return Fail("Technology is not yet historically available.", out error);
            state.Availability = HistoricalTechnologyAvailability.LocallyAccessible;
            state.LastSource = source;
            state.AcquisitionGameTick = Find.TickManager?.TicksGame ?? -1;
            return true;
        }

        public static bool TryGrantTechnologyKnowledge(string communityId, string technologyId, HistoricalTechnologyAcquisitionSource source, out string error)
        {
            if (!TryGetUsableState(communityId, technologyId, out HistoricalTechnologyDef def, out HistoricalTechnologyState state, out error)) return false;
            if (!IsHistoricallyAvailable(def)) return Fail("Technology is not yet historically available.", out error);
            state.Availability = HistoricalTechnologyAvailability.LocallyAccessible;
            if (state.Adoption == HistoricalTechnologyAdoption.Unknown) state.Adoption = HistoricalTechnologyAdoption.Known;
            state.LastSource = source;
            state.AcquisitionGameTick = Find.TickManager?.TicksGame ?? -1;
            return true;
        }

        public static bool TryAdoptTechnology(string communityId, string technologyId, HistoricalTechnologyAcquisitionSource source, out string error)
        {
            if (!TryGetUsableState(communityId, technologyId, out HistoricalTechnologyDef def, out HistoricalTechnologyState state, out error)) return false;
            if (!IsHistoricallyAvailable(def)) return Fail("Technology is not yet historically available.", out error);
            if (state.Adoption == HistoricalTechnologyAdoption.Adopted) return Fail("Technology is already adopted.", out error);
            if (state.Availability != HistoricalTechnologyAvailability.LocallyAccessible) return Fail("Technology is not locally accessible.", out error);
            if (!PrerequisitesAdopted(communityId, def)) return Fail("Technology prerequisites have not been adopted.", out error);
            state.Adoption = HistoricalTechnologyAdoption.Adopted;
            state.LastSource = source;
            state.AdoptionGameTick = Find.TickManager?.TicksGame ?? -1;
            HistoricalDate date;
            if (HistoricalCalendarManager.TryGetCurrentDate(out date)) state.AdoptionAstronomicalYear = date.AstronomicalYear;
            HistoricalChronicleManager.TryAddLocalExperience("Technology:" + communityId + ":" + technologyId,
                "HR_Tech_Chronicle_Title", "HR_Tech_Chronicle_Description", null, null,
                def.evidenceLevel, def.sourceNotes, out _);
            return true;
        }

        public static bool TrySetUnderStudy(string communityId, string technologyId, out string error)
        {
            error = null;
            if (!CanStudy(communityId, technologyId)) return Fail("Technology cannot currently be studied.", out error);
            HistoricalTechnologyState state = Component.Find(communityId, technologyId);
            if (state.Adoption != HistoricalTechnologyAdoption.Adopted) state.Adoption = HistoricalTechnologyAdoption.UnderStudy;
            return true;
        }

        public static void RecordResearchProgress(ResearchProjectDef project, float progress)
        {
            HistoricalTechnologyDef def = Technologies.FirstOrDefault(item => item.researchProject == project);
            HistoricalTechnologyState state = def == null ? null : Component?.Find(GetPlayerCommunityId(), def.defName);
            if (state != null) state.StudyProgress = Math.Max(state.StudyProgress, progress);
        }

        public static bool IsTechnologyProject(ResearchProjectDef project) => project != null && Technologies.Any(def => def.researchProject == project);
        public static bool IsResearchAllowed(ResearchProjectDef project)
        {
            if (!IsActive || project == null) return true;
            HistoricalTechnologyDef tech = Technologies.FirstOrDefault(item => item.researchProject == project);
            if (tech != null) return CanStudy(GetPlayerCommunityId(), tech.defName);
            HistoricalTechnologyRequirementExtension extension = project.GetModExtension<HistoricalTechnologyRequirementExtension>();
            if (extension != null) return IsRequirementMet(extension);
            return project.techLevel == TechLevel.Animal || project.techLevel == TechLevel.Neolithic;
        }

        public static bool IsBuildAllowed(BuildableDef buildable)
        {
            if (!IsActive || buildable == null) return true;
            HistoricalTechnologyRequirementExtension extension = buildable.GetModExtension<HistoricalTechnologyRequirementExtension>();
            if (extension != null) return IsRequirementMet(extension);
            ThingDef thing = buildable as ThingDef;
            return thing == null || thing.techLevel == TechLevel.Animal || thing.techLevel == TechLevel.Neolithic;
        }

        public static bool IsRecipeAllowed(RecipeDef recipe)
        {
            if (!IsActive || recipe == null) return true;
            HistoricalTechnologyRequirementExtension extension = recipe.GetModExtension<HistoricalTechnologyRequirementExtension>();
            if (extension != null) return IsRequirementMet(extension);
            ThingDef productWithRequirement = recipe.products?.Select(item => item.thingDef).FirstOrDefault(item => item?.GetModExtension<HistoricalTechnologyRequirementExtension>() != null);
            if (productWithRequirement != null) return IsRequirementMet(productWithRequirement.GetModExtension<HistoricalTechnologyRequirementExtension>());
            if (recipe.products != null && recipe.products.Any(item => item.thingDef != null && !IsThingAllowed(item.thingDef))) return false;
            if (recipe.researchPrerequisite != null && !IsResearchAllowed(recipe.researchPrerequisite)) return false;
            return recipe.researchPrerequisites == null || recipe.researchPrerequisites.All(IsResearchAllowed);
        }

        public static bool IsThingAllowed(ThingDef thing)
        {
            if (!IsActive || thing == null) return true;
            HistoricalTechnologyRequirementExtension extension = thing.GetModExtension<HistoricalTechnologyRequirementExtension>();
            if (extension != null) return IsRequirementMet(extension);
            if (thing.techLevel == TechLevel.Animal || thing.techLevel == TechLevel.Neolithic) return true;
            // Natural life, plants and food remain available so world ecology and Biotech reproduction are not gated.
            return thing.race != null || thing.plant != null || (thing.ingestible != null && thing.techLevel == TechLevel.Undefined);
        }

        public static bool IsRequirementMet(HistoricalTechnologyRequirementExtension extension)
        {
            if (extension == null) return true;
            if (extension.policy == HistoricalContentPolicy.Forbidden) return false;
            if (extension.policy == HistoricalContentPolicy.Allowed && string.IsNullOrWhiteSpace(extension.requiredTechnology)) return true;
            return !string.IsNullOrWhiteSpace(extension.requiredTechnology)
                && Component?.Find(GetPlayerCommunityId(), extension.requiredTechnology)?.Adoption == HistoricalTechnologyAdoption.Adopted;
        }

        private static bool PrerequisitesAdopted(string communityId, HistoricalTechnologyDef def) => def.prerequisites == null
            || def.prerequisites.All(item => item != null && Component.Find(communityId, item.defName)?.Adoption == HistoricalTechnologyAdoption.Adopted);

        private static bool IsHistoricallyAvailable(HistoricalTechnologyDef def)
        {
            HistoricalDate date;
            return !def.earliestPossibleYear.HasValue || !HistoricalCalendarManager.TryGetCurrentDate(out date)
                || date.AstronomicalYear >= def.earliestPossibleYear.Value;
        }

        private static HistoricalTechnologyState GetOrCreateState(string communityId, HistoricalTechnologyDef def)
        {
            HistoricalTechnologyState state = Component?.Find(communityId, def.defName);
            if (state != null) return state;
            HistoricalCommunityTechnologyState community = Component?.Communities?.FirstOrDefault(item => item.CommunityId == communityId);
            if (community == null) return null;
            state = new HistoricalTechnologyState { TechnologyId = def.defName, Availability = HistoricalTechnologyAvailability.ExistsElsewhere, Adoption = HistoricalTechnologyAdoption.Unknown };
            // Runtime additions are exposed through the component's community record API.
            community.Add(state);
            return state;
        }

        private static HistoricalCommunityTechnologyState GetCommunityState(string id) => Component?.Communities?.FirstOrDefault(item => item.CommunityId == id);
        private static bool TryGetUsableState(string communityId, string technologyId, out HistoricalTechnologyDef def, out HistoricalTechnologyState state, out string error)
        {
            def = DefDatabase<HistoricalTechnologyDef>.GetNamedSilentFail(technologyId);
            state = null;
            if (!IsActive || def == null || string.IsNullOrWhiteSpace(communityId)) return Fail("Historical technology, active scenario, or community is missing.", out error);
            state = GetOrCreateState(communityId, def);
            if (state == null) return Fail("The requested community has no historical technology state.", out error);
            error = null;
            return true;
        }

        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
