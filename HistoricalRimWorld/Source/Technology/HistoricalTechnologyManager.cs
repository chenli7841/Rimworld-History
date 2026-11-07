using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Production;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Factions;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public static class HistoricalTechnologyManager
    {
        public static bool IsActive => Component?.Initialized == true && HistoricalScenarioManager.IsScenarioActive;
        public static IEnumerable<HistoricalTechnologyDef> Technologies => DefDatabase<HistoricalTechnologyDef>.AllDefsListForReading;
        public static IEnumerable<HistoricalTechnologyState> PlayerTechnologies => GetCommunityState(GetPlayerCommunityId())?.Technologies ?? Enumerable.Empty<HistoricalTechnologyState>();
        public static IEnumerable<HistoricalFactionTechnologyState> ComponentFactionStates => Component?.FactionStates ?? Enumerable.Empty<HistoricalFactionTechnologyState>();
        private static HistoricalTechnologyGameComponent Component => Current.Game?.GetComponent<HistoricalTechnologyGameComponent>();

        public static bool TryInitializeForScenario(HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (scenario == null || HistoricalScenarioManager.CurrentScenario?.defName != scenario.defName)
                return Fail("Scenario must be active before technology is initialized.", out error);
            HistoricalTechnologyGameComponent component = Component;
            if (component == null) return Fail("HistoricalTechnologyGameComponent is unavailable.", out error);
            if (component.Initialized)
            {
                if (component.ScenarioDefName != scenario.defName) return Fail("Technology state belongs to another scenario.", out error);
                if (!EnsureFactionStates(component, scenario, out error)) return false;
                return true;
            }

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
                HistoricalTechnologyAvailability availability = technology.earliestPossibleYear.HasValue
                    && date.AstronomicalYear < HistoricalTechnologyTimelineLayout.ConvertHistoricalYearToAstronomical(technology.earliestPossibleYear.Value)
                    ? HistoricalTechnologyAvailability.NotYetHistoricallyAvailable
                    : technology.initialKnownRegions != null && technology.initialKnownRegions.Contains(scenario.defaultStartingRegion)
                        ? HistoricalTechnologyAvailability.LocallyAccessible
                        : HistoricalTechnologyAvailability.ExistsElsewhere;
                initialStates.Add(new HistoricalTechnologyInitialState { technology = technology, availability = availability, adoption = HistoricalTechnologyAdoption.Unknown });
            }
            if (!component.Initialize(scenario.defName, setup.communityId, initialStates))
                return Fail("Could not initialize community technology records.", out error);
            return EnsureFactionStates(component, scenario, out error);
        }

        public static bool RebuildMissingFactionStates(out string error)
        {
            HistoricalTechnologyGameComponent component = Component;
            HistoricalScenarioDef scenario = HistoricalScenarioManager.CurrentScenario;
            return component != null && component.Initialized && scenario != null
                ? EnsureFactionStates(component, scenario, out error)
                : Fail("An initialized historical scenario is required.", out error);
        }

        private static bool EnsureFactionStates(HistoricalTechnologyGameComponent component, HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (component == null || scenario == null || Find.FactionManager == null
                || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate))
                return Fail("Faction manager or historical date is unavailable for world technology initialization.", out error);
            List<HistoricalFactionTechnologyProfileDef> profiles = DefDatabase<HistoricalFactionTechnologyProfileDef>.AllDefsListForReading
                .Where(item => item?.scenario?.defName == scenario.defName).ToList();
            if (profiles.Count == 0) return Fail("No HistoricalFactionTechnologyProfileDef is configured for " + scenario.defName + ".", out error);

            List<Faction> factions = Find.FactionManager.AllFactionsListForReading
                .Where(item => item != null && (item == Faction.OfPlayerSilentFail || HistoricalFactionManager.TryGetHistoricalState(item, out _)
                    || profiles.Any(profile => profile.factionDef == item.def)))
                .Distinct().ToList();
            Faction player = Faction.OfPlayerSilentFail;
            if (player != null && !factions.Contains(player)) factions.Add(player);
            foreach (Faction faction in factions)
            {
                HistoricalStateDef historicalState = null;
                HistoricalFactionManager.TryGetHistoricalState(faction, out historicalState);
                HistoricalFactionTechnologyProfileDef profile = profiles.FirstOrDefault(item =>
                    (historicalState != null && item.historicalState?.defName == historicalState.defName)
                    || (item.factionDef != null && item.factionDef == faction.def));
                string stableId = historicalState?.defName ?? faction.GetUniqueLoadID();
                HistoricalFactionTechnologyState factionState = component.GetOrAddFactionState(stableId,
                    historicalState?.defName, faction.def?.defName, faction == player);
                if (factionState == null) continue;
                if (profile != null) InitializeProfileRecords(factionState, profile, currentDate.AstronomicalYear);
                if (faction == player) SynchronizePlayerRecords(component, factionState, currentDate.AstronomicalYear);
            }

            if (component.FactionStates.Count() == 0) return Fail("No faction technology states could be created.", out error);
            component.InitializeScheduler(currentDate.AbsoluteDay);
            Log.Message("[Historical RimWorld] Initialized faction technology profiles: "
                + component.FactionStates.Count() + " factions; scenario=" + scenario.defName + ".");
            return true;
        }

        private static void InitializeProfileRecords(HistoricalFactionTechnologyState factionState,
            HistoricalFactionTechnologyProfileDef profile, int year)
        {
            foreach (HistoricalTechnologyDef technology in profile.initialMasteredTechnologies ?? new List<HistoricalTechnologyDef>())
                if (technology?.researchProject != null)
                    factionState.GetOrAdd(technology.researchProject.defName).SetLevel(FactionTechKnowledgeLevel.Mastered, int.MinValue,
                        "ScenarioProfile:" + profile.defName);
            foreach (HistoricalTechnologyDef technology in profile.initialHeardOfTechnologies ?? new List<HistoricalTechnologyDef>())
                if (technology?.researchProject != null)
                    factionState.GetOrAdd(technology.researchProject.defName).SetLevel(FactionTechKnowledgeLevel.HeardOf, int.MinValue,
                        "ScenarioProfile:" + profile.defName);
        }

        private static void SynchronizePlayerRecords(HistoricalTechnologyGameComponent component,
            HistoricalFactionTechnologyState factionState, int year)
        {
            foreach (HistoricalTechnologyState state in PlayerTechnologies)
            {
                HistoricalTechnologyDef technology = DefDatabase<HistoricalTechnologyDef>.GetNamedSilentFail(state.TechnologyId);
                ResearchProjectDef project = technology?.researchProject;
                if (project == null) continue;
                if (state.Adoption == HistoricalTechnologyAdoption.Adopted || project.IsFinished)
                    factionState.GetOrAdd(project.defName).SetLevel(FactionTechKnowledgeLevel.Mastered, int.MinValue, "PlayerResearchState");
                else if (state.Adoption == HistoricalTechnologyAdoption.Known || state.Adoption == HistoricalTechnologyAdoption.UnderStudy)
                    factionState.GetOrAdd(project.defName).SetLevel(FactionTechKnowledgeLevel.HeardOf, int.MinValue, "PlayerScenarioKnowledge");

                if (state.Adoption != HistoricalTechnologyAdoption.Unknown || project.IsFinished)
                    component.GetOrAddAwareness(project.defName).Raise(PlayerHistoricalTechAwareness.Confirmed, year);
            }
        }

        public static bool HasFactionMasteredTechnology(Faction faction, ResearchProjectDef research)
        {
            HistoricalFactionTechnologyState state = FindFactionState(faction);
            return state?.Find(research?.defName)?.Level == FactionTechKnowledgeLevel.Mastered;
        }

        public static bool HasTechnologyEmerged(ResearchProjectDef research) => GetWorldStatus(research) != HistoricalWorldTechStatus.NotEmerged;

        public static bool IsTechnologyKnownToPlayer(ResearchProjectDef research) =>
            (Component?.FindAwareness(research?.defName)?.Awareness ?? PlayerHistoricalTechAwareness.Unknown) != PlayerHistoricalTechAwareness.Unknown;

        public static PlayerHistoricalTechAwareness ComponentAwareness(ResearchProjectDef research) =>
            Component?.FindAwareness(research?.defName)?.Awareness ?? PlayerHistoricalTechAwareness.Unknown;

        public static PlayerHistoricalTechAwarenessState ComponentAwarenessRecord(ResearchProjectDef research) =>
            Component?.FindAwareness(research?.defName);

        public static IReadOnlyList<Faction> GetKnownTechnologySources(ResearchProjectDef research)
        {
            HistoricalTechnologyGameComponent component = Component;
            HashSet<string> known = new HashSet<string>(component?.FindAwareness(research?.defName)?.KnownSourceFactionIds ?? new List<string>(), StringComparer.Ordinal);
            return component == null ? (IReadOnlyList<Faction>)new List<Faction>()
                : component.FactionStates.Where(state => state != null && known.Contains(state.StableFactionId))
                    .Select(ResolveFaction).Where(faction => faction != null).ToList();
        }

        public static bool CanFactionSupplyTechnology(Faction faction, ResearchProjectDef research) =>
            HasFactionMasteredTechnology(faction, research);

        public static HistoricalWorldTechStatus GetWorldStatus(ResearchProjectDef research)
        {
            if (research == null) return HistoricalWorldTechStatus.NotEmerged;
            HistoricalTechnologyGameComponent component = Component;
            if (component == null) return HistoricalWorldTechStatus.NotEmerged;
            // Retain extinct/deleted factions in the denominator and history; their records remain valid evidence.
            List<HistoricalFactionTechnologyState> eligible = component.FactionStates
                .Where(item => item != null && !item.IsPlayerFaction).ToList();
            List<HistoricalFactionTechnologyState> mastered = eligible.Where(item =>
                item.Find(research.defName)?.Level == FactionTechKnowledgeLevel.Mastered).ToList();
            if (mastered.Count == 0) return HistoricalWorldTechStatus.NotEmerged;
            if (mastered.Count == 1) return HistoricalWorldTechStatus.Emerged;
            float threshold = HistoricalTechnologyTimelineLayout.GetExtension(research)?.WidespreadThreshold ?? 0.7f;
            if (eligible.Count > 0 && mastered.Count / (float)eligible.Count >= Mathf.Clamp01(threshold))
                return HistoricalWorldTechStatus.Widespread;
            return HistoricalWorldTechStatus.Spreading;
        }

        internal static HistoricalFactionTechnologyState FindFactionState(Faction faction)
        {
            if (faction == null) return null;
            HistoricalTechnologyGameComponent component = Component;
            if (component == null) return null;
            HistoricalStateDef historicalState;
            string stableId = HistoricalFactionManager.TryGetHistoricalState(faction, out historicalState)
                ? historicalState.defName : faction.GetUniqueLoadID();
            return component.FindFactionState(stableId);
        }

        internal static Faction ResolveFaction(HistoricalFactionTechnologyState state)
        {
            if (state == null || Find.FactionManager == null) return null;
            if (state.IsPlayerFaction) return Faction.OfPlayerSilentFail;
            if (!string.IsNullOrWhiteSpace(state.HistoricalStateId)
                && HistoricalFactionManager.TryGetFaction(state.HistoricalStateId, out Faction mapped)) return mapped;
            return Find.FactionManager.AllFactionsListForReading.FirstOrDefault(item => item != null
                && item.GetUniqueLoadID() == state.StableFactionId);
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
            if (state == null || state.Adoption == HistoricalTechnologyAdoption.Adopted) return false;
            if (def.researchProject != null && HistoricalBlueprintService.GetMode(def.researchProject) == HistoricalResearchMode.Basic)
                return PrerequisitesAdopted(communityId, def);
            if (state.Availability != HistoricalTechnologyAvailability.LocallyAccessible) return false;
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
            bool basicResearch = def.researchProject != null && HistoricalBlueprintService.GetMode(def.researchProject) == HistoricalResearchMode.Basic;
            if (!basicResearch && !IsHistoricallyAvailable(def)) return Fail("Technology is not yet historically available.", out error);
            if (state.Adoption == HistoricalTechnologyAdoption.Adopted) return Fail("Technology is already adopted.", out error);
            if (basicResearch) state.Availability = HistoricalTechnologyAvailability.LocallyAccessible;
            if (state.Availability != HistoricalTechnologyAvailability.LocallyAccessible) return Fail("Technology is not locally accessible.", out error);
            if (!PrerequisitesAdopted(communityId, def)) return Fail("Technology prerequisites have not been adopted.", out error);
            state.Adoption = HistoricalTechnologyAdoption.Adopted;
            state.LastSource = source;
            state.AdoptionGameTick = Find.TickManager?.TicksGame ?? -1;
            HistoricalDate date;
            if (HistoricalCalendarManager.TryGetCurrentDate(out date)) state.AdoptionAstronomicalYear = date.AstronomicalYear;
            int year = HistoricalCalendarManager.TryGetCurrentDate(out date) ? date.AstronomicalYear : int.MinValue;
            HistoricalFactionTechnologyState factionState = FindFactionState(Faction.OfPlayerSilentFail);
            factionState?.GetOrAdd(def.researchProject?.defName)?.SetLevel(FactionTechKnowledgeLevel.Mastered, year, "PlayerResearch");
            Component?.GetOrAddAwareness(def.researchProject?.defName)?.Raise(PlayerHistoricalTechAwareness.Confirmed, year);
            HistoricalRimWorld.Patches.Technology.HistoricalResearchTimelineUIState.Invalidate();
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
            // Saves from before Phase 3 keep completed research permanently unlocked.
            if (project.IsFinished) return true;
            HistoricalTechnologyDef tech = Technologies.FirstOrDefault(item => item.researchProject == project);
            if (tech != null)
            {
                if (HistoricalBlueprintService.GetMode(project) == HistoricalResearchMode.Basic)
                    return HistoricalBlueprintService.PrerequisitesComplete(project);
                return HistoricalBlueprintService.CanStartHistoricalResearch(project, out _);
            }
            HistoricalTechnologyRequirementExtension extension = project.GetModExtension<HistoricalTechnologyRequirementExtension>();
            if (extension != null) return IsRequirementMet(extension);
            return project.techLevel == TechLevel.Animal || project.techLevel == TechLevel.Neolithic;
        }

        public static bool CanStartHistoricalResearch(ResearchProjectDef project, out string reason) =>
            HistoricalBlueprintService.CanStartHistoricalResearch(project, out reason);

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
                || date.AstronomicalYear >= HistoricalTechnologyTimelineLayout.ConvertHistoricalYearToAstronomical(def.earliestPossibleYear.Value);
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
