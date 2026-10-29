using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Politics
{
    public static class HistoricalPoliticsManager
    {
        public static bool IsActive
        {
            get
            {
                HistoricalPoliticsGameComponent component = GetCurrentComponent();
                return component != null && component.Initialized;
            }
        }

        public static IEnumerable<HistoricalPolityState> Polities => GetCurrentComponent()?.Polities ?? Enumerable.Empty<HistoricalPolityState>();
        public static IEnumerable<HistoricalDomainState> Domains => GetCurrentComponent()?.Domains ?? Enumerable.Empty<HistoricalDomainState>();
        public static IEnumerable<HistoricalRegionPoliticalState> Regions => GetCurrentComponent()?.Regions ?? Enumerable.Empty<HistoricalRegionPoliticalState>();
        public static IEnumerable<HistoricalPoliticalRelation> Relations => GetCurrentComponent()?.Relations ?? Enumerable.Empty<HistoricalPoliticalRelation>();
        public static IEnumerable<HistoricalLocalCommunityState> Communities => GetCurrentComponent()?.Communities ?? Enumerable.Empty<HistoricalLocalCommunityState>();
        public static IEnumerable<HistoricalPoliticalChangeRecord> Changes => GetCurrentComponent()?.Changes ?? Enumerable.Empty<HistoricalPoliticalChangeRecord>();

        public static bool TryInitializeForScenario(HistoricalScenarioDef scenario, out string error)
        {
            if (scenario == null || HistoricalScenarioManager.CurrentScenario == null
                || HistoricalScenarioManager.CurrentScenario.defName != scenario.defName)
            {
                error = "The scenario must already be active in this save before its politics can be initialized.";
                return false;
            }

            HistoricalPoliticsInitializationPlan plan;
            if (!TryPrepareScenarioInitialization(scenario, out plan, out error)) return false;
            return TryCommitScenarioInitialization(plan, out error);
        }

        internal static bool TryPrepareScenarioInitialization(
            HistoricalScenarioDef scenario,
            out HistoricalPoliticsInitializationPlan plan,
            out string error)
        {
            plan = null;
            error = null;
            if (Current.Game == null)
            {
                error = "A game must be loaded before political state can be initialized.";
                return false;
            }

            HistoricalPoliticsGameComponent component = Current.Game.GetComponent<HistoricalPoliticsGameComponent>();
            if (component == null)
            {
                error = "HistoricalPoliticsGameComponent is unavailable.";
                return false;
            }

            if (component.Initialized)
            {
                if (component.ScenarioDefName == scenario.defName)
                {
                    return true;
                }
                error = "Political state is already initialized for another scenario.";
                return false;
            }

            List<HistoricalPoliticalSetupDef> matchingSetups = DefDatabase<HistoricalPoliticalSetupDef>.AllDefsListForReading
                .Where(item => item.scenario != null && item.scenario.defName == scenario.defName)
                .ToList();
            if (matchingSetups.Count != 1)
            {
                error = matchingSetups.Count == 0
                    ? "No HistoricalPoliticalSetupDef is registered for scenario " + scenario.defName + "."
                    : "More than one HistoricalPoliticalSetupDef is registered for scenario " + scenario.defName + ".";
                return false;
            }

            HistoricalPoliticalSetupDef setup = matchingSetups[0];
            string[] validationErrors = HistoricalPoliticsValidator.ValidateSetup(setup).ToArray();
            if (validationErrors.Length > 0)
            {
                error = string.Join(Environment.NewLine, validationErrors);
                return false;
            }

            plan = BuildInitializationPlan(setup);
            return true;
        }

        internal static bool TryCommitScenarioInitialization(
            HistoricalPoliticsInitializationPlan plan,
            out string error)
        {
            error = null;
            HistoricalPoliticsGameComponent component = GetCurrentComponent();
            if (component == null)
            {
                error = "HistoricalPoliticsGameComponent is unavailable.";
                return false;
            }

            if (plan == null) return component.Initialized;

            if (!component.ApplyInitializationPlan(plan))
            {
                error = "Political state is already initialized for another scenario.";
                return false;
            }

            Log.Message("[Historical RimWorld] Initialized political state for scenario " + plan.ScenarioDefName + ".");
            return true;
        }

        public static bool TryGetPolity(string polityId, out HistoricalPolityState polity)
        {
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            polity = component?.FindPolity(polityId);
            return polity != null;
        }

        public static bool TryGetDomain(string domainId, out HistoricalDomainState domain)
        {
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            domain = component?.FindDomain(domainId);
            return domain != null;
        }

        public static bool TryGetRegion(string regionId, out HistoricalRegionPoliticalState region)
        {
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            region = component?.FindRegion(regionId);
            return region != null;
        }

        public static bool TryGetCommunity(string communityId, out HistoricalLocalCommunityState community)
        {
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            community = component?.FindCommunity(communityId);
            return community != null;
        }

        public static IEnumerable<HistoricalPoliticalRelation> GetRelations(string polityId)
        {
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            return component == null
                ? Enumerable.Empty<HistoricalPoliticalRelation>()
                : component.Relations.Where(item => item != null && item.IsActive
                    && (item.SourcePolityId == polityId || item.TargetPolityId == polityId));
        }

        public static bool HasRelation(
            string sourceId,
            string targetId,
            HistoricalPoliticalRelationType relationType)
        {
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            return component != null && component.FindRelation(sourceId, targetId, relationType, true) != null;
        }

        public static bool TryChangeRegionController(
            string regionId,
            string newControllerId,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeWorldPolitics(source, out error)) return false;
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalRegionPoliticalState region = component?.FindRegion(regionId);
            if (region == null) return Reject("Unknown historical region " + regionId + ".", out error);
            if (!IsUsablePolity(component, newControllerId)) return Reject("New controller must be an active historical polity or null.", out error);
            if (region.EffectiveControllerId == newControllerId) return true;

            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, regionId, "RegionControllerChanged",
                "controller=" + Value(region.EffectiveControllerId), "controller=" + Value(newControllerId), source, out change, out error)) return false;
            region.SetEffectiveController(newControllerId, region.MilitaryOccupierId, region.IsContested);
            component.AddChange(change);
            return true;
        }

        public static bool TryChangeRegionNominalOwner(
            string regionId,
            string newOwnerId,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeWorldPolitics(source, out error)) return false;
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalRegionPoliticalState region = component?.FindRegion(regionId);
            if (region == null) return Reject("Unknown historical region " + regionId + ".", out error);
            if (!IsUsablePolity(component, newOwnerId)) return Reject("Nominal owner must be an active historical polity or null.", out error);
            if (region.NominalOwnerId == newOwnerId) return true;

            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, regionId, "RegionNominalOwnerChanged",
                "owner=" + Value(region.NominalOwnerId), "owner=" + Value(newOwnerId), source, out change, out error)) return false;
            region.SetNominalOwner(newOwnerId);
            component.AddChange(change);
            return true;
        }

        public static bool TryChangeDomainController(
            string domainId,
            string newControllerId,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeWorldPolitics(source, out error)) return false;
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalDomainState domain = component?.FindDomain(domainId);
            if (domain == null) return Reject("Unknown historical domain " + domainId + ".", out error);
            if (!IsUsablePolity(component, newControllerId)) return Reject("New controller must be an active historical polity or null.", out error);
            if (domain.EffectiveControllerId == newControllerId) return true;

            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, domainId, "DomainControllerChanged",
                "controller=" + Value(domain.EffectiveControllerId), "controller=" + Value(newControllerId), source, out change, out error)) return false;
            domain.SetEffectiveController(newControllerId, domain.IsDisputed);
            component.AddChange(change);
            return true;
        }

        public static bool TryChangeCommunityAllegiance(
            string communityId,
            string newAllegianceId,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeLocalPolitics(source, out error)) return false;
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalLocalCommunityState community = component?.FindCommunity(communityId);
            if (community == null) return Reject("Unknown local community " + communityId + ".", out error);
            if (!IsUsablePolity(component, newAllegianceId)) return Reject("Allegiance must reference an active historical polity or null.", out error);
            if (community.AllegiancePolityId == newAllegianceId) return true;

            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, communityId, "CommunityAllegianceChanged",
                "allegiance=" + Value(community.AllegiancePolityId), "allegiance=" + Value(newAllegianceId), source, out change, out error)) return false;
            community.SetAllegiance(newAllegianceId);
            component.AddChange(change);
            return true;
        }

        public static bool TrySubmitCommunity(
            string communityId,
            string submittingToPolityId,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeLocalPolitics(source, out error)) return false;
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalLocalCommunityState community = component?.FindCommunity(communityId);
            if (community == null) return Reject("Unknown local community " + communityId + ".", out error);
            if (!IsUsablePolity(component, submittingToPolityId)) return Reject("Submission target must be an active historical polity.", out error);
            if (source == HistoricalPoliticalChangeSource.PlayerDecision)
            {
                HistoricalRegionPoliticalState region = component.FindRegion(community.RegionId);
                if (region == null || (region.MilitaryOccupierId != submittingToPolityId
                    && region.EffectiveControllerId != submittingToPolityId))
                {
                    return Reject("A player decision can submit a community only to a polity controlling or occupying its region.", out error);
                }
            }

            string oldValue = CommunitySnapshot(community);
            string newValue = "controller=" + Value(submittingToPolityId)
                + "|allegiance=" + Value(submittingToPolityId)
                + "|posture=" + HistoricalCommunityPosture.Submitting;
            if (oldValue == newValue) return true;

            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, communityId, "CommunitySubmitted", oldValue, newValue, source, out change, out error)) return false;
            community.SubmitTo(submittingToPolityId);
            component.AddChange(change);
            return true;
        }

        public static bool TrySetCommunityPosture(
            string communityId,
            HistoricalCommunityPosture posture,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeLocalPolitics(source, out error)) return false;
            if (!Enum.IsDefined(typeof(HistoricalCommunityPosture), posture)) return Reject("Unknown community posture.", out error);
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalLocalCommunityState community = component?.FindCommunity(communityId);
            if (community == null) return Reject("Unknown local community " + communityId + ".", out error);
            if (posture == HistoricalCommunityPosture.Resisting)
            {
                HistoricalRegionPoliticalState region = component.FindRegion(community.RegionId);
                if (region == null || string.IsNullOrEmpty(community.AllegiancePolityId)
                    || region.EffectiveControllerId == community.AllegiancePolityId)
                {
                    return Reject("A community can resist only when it retains an allegiance different from the current regional controller.", out error);
                }
            }
            if (community.Posture == posture) return true;

            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, communityId, "CommunityPostureChanged",
                "posture=" + community.Posture, "posture=" + posture, source, out change, out error)) return false;
            community.SetPosture(posture);
            component.AddChange(change);
            return true;
        }

        public static bool TryMoveCommunity(
            string communityId,
            string newRegionId,
            string newDomainId,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeLocalPolitics(source, out error)) return false;
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalLocalCommunityState community = component?.FindCommunity(communityId);
            if (community == null) return Reject("Unknown local community " + communityId + ".", out error);
            if (component.FindRegion(newRegionId) == null) return Reject("Unknown destination region " + newRegionId + ".", out error);
            if (newDomainId != null)
            {
                HistoricalDomainState domain = component.FindDomain(newDomainId);
                HistoricalDomainDef domainDef = DefDatabase<HistoricalDomainDef>.GetNamedSilentFail(newDomainId);
                if (domain == null || domainDef == null || domainDef.region == null || domainDef.region.defName != newRegionId)
                {
                    return Reject("Destination domain must exist and belong to the destination region.", out error);
                }
            }
            if (community.RegionId == newRegionId && community.DomainId == newDomainId) return true;

            HistoricalPoliticalChangeRecord change;
            string oldValue = "region=" + Value(community.RegionId) + "|domain=" + Value(community.DomainId);
            string newValue = "region=" + Value(newRegionId) + "|domain=" + Value(newDomainId);
            if (!TryCreateChange(component, communityId, "CommunityMoved", oldValue, newValue, source, out change, out error)) return false;
            community.SetLocation(newRegionId, newDomainId);
            component.AddChange(change);
            return true;
        }

        public static bool TrySetPolityStatus(
            string polityId,
            HistoricalPolityStatus status,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeWorldPolitics(source, out error)) return false;
            if (!Enum.IsDefined(typeof(HistoricalPolityStatus), status)) return Reject("Unknown polity status.", out error);
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalPolityState polity = component?.FindPolity(polityId);
            if (polity == null) return Reject("Unknown historical polity " + polityId + ".", out error);
            if (polity.Status == status) return true;
            if (polity.Status == HistoricalPolityStatus.Dissolved && status != HistoricalPolityStatus.Dissolved
                && source != HistoricalPoliticalChangeSource.HistoricalEvent
                && source != HistoricalPoliticalChangeSource.Debug)
            {
                return Reject("A dissolved polity can be restored only by a historical event or developer action.", out error);
            }

            List<HistoricalPoliticalChangeRecord> pendingChanges = new List<HistoricalPoliticalChangeRecord>();
            List<Action> pendingMutations = new List<Action>();

            if (status == HistoricalPolityStatus.Dissolved)
            {
                foreach (HistoricalPoliticalRelation relation in component.Relations.Where(item => item != null
                    && item.IsActive && (item.SourcePolityId == polityId || item.TargetPolityId == polityId)))
                {
                    string targetId = relation.SourcePolityId + "->" + relation.TargetPolityId + ":" + relation.RelationType;
                    if (!QueueChange(component, pendingChanges, pendingMutations, targetId, "PoliticalRelationChanged",
                        "active", "inactive", source, () => relation.SetActive(false), out error)) return false;
                }

                foreach (HistoricalPolityState other in component.Polities.Where(item => item != null && item.PolityId != polityId))
                {
                    if (other.NominalOverlordId == polityId
                        && !QueueChange(component, pendingChanges, pendingMutations, other.PolityId, "PolityHierarchyChanged",
                            "nominalOverlord=" + polityId, "nominalOverlord=(none)", source,
                            () => other.SetNominalOverlord(null), out error)) return false;
                    if (other.EffectiveOverlordId == polityId
                        && !QueueChange(component, pendingChanges, pendingMutations, other.PolityId, "PolityHierarchyChanged",
                            "effectiveOverlord=" + polityId, "effectiveOverlord=(none)", source,
                            () => other.SetEffectiveOverlord(null), out error)) return false;
                }

                foreach (HistoricalRegionPoliticalState region in component.Regions.Where(item => item != null
                    && (item.EffectiveControllerId == polityId || item.MilitaryOccupierId == polityId)))
                {
                    string oldValue = "controller=" + Value(region.EffectiveControllerId)
                        + "|occupier=" + Value(region.MilitaryOccupierId) + "|contested=" + region.IsContested;
                    string newValue = "controller=" + Value(region.EffectiveControllerId == polityId ? null : region.EffectiveControllerId)
                        + "|occupier=" + Value(region.MilitaryOccupierId == polityId ? null : region.MilitaryOccupierId)
                        + "|contested=true";
                    if (!QueueChange(component, pendingChanges, pendingMutations, region.RegionId, "RegionControllerChanged",
                        oldValue, newValue, source,
                        () => region.SetEffectiveController(
                            region.EffectiveControllerId == polityId ? null : region.EffectiveControllerId,
                            region.MilitaryOccupierId == polityId ? null : region.MilitaryOccupierId, true), out error)) return false;
                }

                foreach (HistoricalDomainState domain in component.Domains.Where(item => item != null
                    && item.EffectiveControllerId == polityId))
                {
                    if (!QueueChange(component, pendingChanges, pendingMutations, domain.DomainId, "DomainControllerChanged",
                        "controller=" + polityId, "controller=(none)", source,
                        () => domain.SetEffectiveController(null, true), out error)) return false;
                }

                foreach (HistoricalLocalCommunityState community in component.Communities.Where(item => item != null
                    && item.EffectiveControllerId == polityId))
                {
                    string oldValue = CommunitySnapshot(community);
                    string newValue = "controller=(none)|allegiance=" + Value(community.AllegiancePolityId)
                        + "|posture=" + community.Posture;
                    if (!QueueChange(component, pendingChanges, pendingMutations, community.CommunityId, "CommunityControllerChanged",
                        oldValue, newValue, source, () => community.SetController(null), out error)) return false;
                }
            }

            if (!TryCreateChange(component, polityId, "PolityStatusChanged",
                "status=" + polity.Status, "status=" + status, source, out HistoricalPoliticalChangeRecord statusChange, out error)) return false;
            pendingChanges.Add(statusChange);
            foreach (Action mutation in pendingMutations) mutation();
            polity.SetStatus(status);
            foreach (HistoricalPoliticalChangeRecord change in pendingChanges) component.AddChange(change);
            return true;
        }

        public static bool TrySetPolitySuccessors(
            string polityId,
            IEnumerable<string> successorPolityIds,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeWorldPolitics(source, out error)) return false;
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalPolityState polity = component?.FindPolity(polityId);
            if (polity == null) return Reject("Unknown historical polity " + polityId + ".", out error);
            List<string> successors = successorPolityIds == null ? new List<string>() : successorPolityIds.ToList();
            if (successors.Any(id => id == polityId || component.FindPolity(id) == null))
            {
                return Reject("Successors must be other known historical polities.", out error);
            }
            if (successors.Distinct(StringComparer.Ordinal).Count() != successors.Count)
            {
                return Reject("Successor list contains duplicate polity IDs.", out error);
            }

            string oldValue = string.Join(",", polity.SuccessorPolityIds);
            string newValue = string.Join(",", successors);
            if (oldValue == newValue) return true;
            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, polityId, "PolitySuccessorsChanged", oldValue, newValue, source, out change, out error)) return false;
            polity.SetSuccessors(successors);
            component.AddChange(change);
            return true;
        }

        public static bool TryChangePoliticalRelation(
            string sourcePolityId,
            string targetPolityId,
            HistoricalPoliticalRelationType relationType,
            bool isActive,
            HistoricalPoliticalChangeSource source,
            out string error)
        {
            error = null;
            if (!CanChangeWorldPolitics(source, out error)) return false;
            if (!Enum.IsDefined(typeof(HistoricalPoliticalRelationType), relationType)) return Reject("Unknown political relation type.", out error);
            if (sourcePolityId == targetPolityId) return Reject("A political entity cannot form a relation with itself.", out error);

            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            HistoricalPolityState sourcePolity = component?.FindPolity(sourcePolityId);
            HistoricalPolityState targetPolity = component?.FindPolity(targetPolityId);
            if (sourcePolity == null || targetPolity == null) return Reject("Political relation references an unknown polity.", out error);
            if (isActive && (sourcePolity.Status == HistoricalPolityStatus.Dissolved || targetPolity.Status == HistoricalPolityStatus.Dissolved))
            {
                return Reject("A dissolved polity cannot enter a new active relation.", out error);
            }

            HistoricalPoliticalRelation relation = component.FindRelation(sourcePolityId, targetPolityId, relationType, false);
            if (relation != null && relation.IsActive == isActive) return true;
            if (relation == null && !isActive) return true;

            if (isActive && relationType == HistoricalPoliticalRelationType.NominalSuzerainty
                && WouldCreateCycle(component, sourcePolityId, targetPolityId, true))
            {
                return Reject("Nominal suzerainty would create a hierarchy cycle.", out error);
            }
            if (isActive && relationType == HistoricalPoliticalRelationType.EffectiveSubordination
                && WouldCreateCycle(component, sourcePolityId, targetPolityId, false))
            {
                return Reject("Effective subordination would create a hierarchy cycle.", out error);
            }

            HistoricalPoliticalChangeRecord change;
            string oldValue = relation == null ? "inactive" : relation.IsActive ? "active" : "inactive";
            string newValue = isActive ? "active" : "inactive";
            string target = sourcePolityId + "->" + targetPolityId + ":" + relationType;
            if (!TryCreateChange(component, target, "PoliticalRelationChanged", oldValue, newValue, source, out change, out error)) return false;

            if (relation == null)
            {
                relation = new HistoricalPoliticalRelation();
                relation.Initialize(sourcePolityId, targetPolityId, relationType, isActive);
                component.AddRelation(relation);
            }
            else
            {
                relation.SetActive(isActive);
            }

            if (relationType == HistoricalPoliticalRelationType.NominalSuzerainty)
            {
                sourcePolity.SetNominalOverlord(isActive ? targetPolityId : null);
            }
            else if (relationType == HistoricalPoliticalRelationType.EffectiveSubordination)
            {
                sourcePolity.SetEffectiveOverlord(isActive ? targetPolityId : null);
            }

            component.AddChange(change);
            return true;
        }

        public static bool TryGetMappedFaction(string polityId, out Faction faction)
        {
            faction = null;
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            if (component == null || component.FindPolity(polityId) == null) return false;
            HistoricalFactionMapping mapping = component.FactionMappings.FirstOrDefault(item =>
                item != null && item.HistoricalPolityId == polityId && item.Faction != null);
            if (mapping == null) return false;
            faction = mapping.Faction;
            return true;
        }

        public static bool TrySetFactionMapping(string polityId, Faction faction, out string error)
        {
            error = null;
            if (!Prefs.DevMode) return Reject("Faction mappings can only be edited in developer mode.", out error);
            HistoricalPoliticsGameComponent component = GetInitializedComponent();
            if (component == null || component.FindPolity(polityId) == null) return Reject("Unknown historical polity.", out error);
            if (faction == null) return Reject("Mapped Faction cannot be null.", out error);
            HistoricalFactionMapping oldMapping = component.FactionMappings.FirstOrDefault(item =>
                item != null && item.HistoricalPolityId == polityId);
            if (oldMapping != null && oldMapping.Faction == faction) return true;
            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, polityId, "FactionMappingChanged",
                oldMapping == null ? "unmapped" : "mapped", "mapped", HistoricalPoliticalChangeSource.Debug,
                out change, out error)) return false;
            HistoricalFactionMapping mapping = new HistoricalFactionMapping();
            mapping.Initialize(polityId, faction);
            component.AddFactionMapping(mapping);
            component.AddChange(change);
            return true;
        }

        private static HistoricalPoliticsInitializationPlan BuildInitializationPlan(HistoricalPoliticalSetupDef setup)
        {
            Dictionary<string, HistoricalInitialPolityRecord> polityRecords = setup.polities.ToDictionary(item => item.polityId, StringComparer.Ordinal);
            List<HistoricalPolityState> polities = new List<HistoricalPolityState>();
            foreach (HistoricalStateDef def in HistoricalDataQuery.GetStates(setup.scenario))
            {
                HistoricalInitialPolityRecord record = polityRecords[def.defName];
                HistoricalPolityState state = new HistoricalPolityState();
                state.Initialize(def.defName, record.status, record.nominalOverlordId, record.effectiveOverlordId,
                    record.successorPolityIds, record.politicalRevision);
                polities.Add(state);
            }

            List<HistoricalRegionPoliticalState> regions = setup.regions.Select(record =>
            {
                HistoricalRegionPoliticalState state = new HistoricalRegionPoliticalState();
                state.Initialize(record.regionId, record.nominalOwnerId, record.effectiveControllerId,
                    record.militaryOccupierId, record.isContested);
                return state;
            }).ToList();

            List<HistoricalDomainState> domains = setup.domains.Select(record =>
            {
                HistoricalDomainState state = new HistoricalDomainState();
                state.Initialize(record.domainId, record.parentPolityId, record.nominalOwnerId,
                    record.effectiveControllerId, record.localLordId, record.isDisputed);
                return state;
            }).ToList();

            List<HistoricalPoliticalRelation> relations = setup.relations.Select(record =>
            {
                HistoricalPoliticalRelation relation = new HistoricalPoliticalRelation();
                relation.Initialize(record.sourcePolityId, record.targetPolityId, record.relationType, record.isActive);
                return relation;
            }).ToList();

            List<HistoricalLocalCommunityState> communities = setup.communities.Select(record =>
            {
                HistoricalLocalCommunityState state = new HistoricalLocalCommunityState();
                state.Initialize(record.communityId, record.regionId, record.domainId, record.nominalPolityId,
                    record.administrativePolityId, record.effectiveControllerId, record.allegiancePolityId, record.posture);
                return state;
            }).ToList();

            return new HistoricalPoliticsInitializationPlan(setup.scenario.defName, setup.defName,
                polities, regions, domains, relations, communities);
        }

        private static bool WouldCreateCycle(
            HistoricalPoliticsGameComponent component,
            string sourceId,
            string targetId,
            bool nominal)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            string currentId = targetId;
            while (currentId != null)
            {
                if (currentId == sourceId) return true;
                if (!seen.Add(currentId)) return true;
                HistoricalPolityState current = component.FindPolity(currentId);
                if (current == null) return false;
                currentId = nominal ? current.NominalOverlordId : current.EffectiveOverlordId;
            }
            return false;
        }

        private static bool TryCreateChange(
            HistoricalPoliticsGameComponent component,
            string targetId,
            string changeType,
            string oldValue,
            string newValue,
            HistoricalPoliticalChangeSource source,
            out HistoricalPoliticalChangeRecord change,
            out string error)
        {
            change = null;
            error = null;
            HistoricalDate date;
            if (component == null || !HistoricalCalendarManager.TryGetCurrentDate(out date)
                || Find.TickManager == null)
            {
                return Reject("A valid historical clock and game tick are required to record political changes.", out error);
            }

            change = new HistoricalPoliticalChangeRecord();
            change.Initialize(component.NextChangeId(), targetId, changeType, oldValue, newValue, source,
                Find.TickManager.TicksGame, date.AstronomicalYear, date.Month, date.Day);
            return true;
        }

        private static bool QueueChange(
            HistoricalPoliticsGameComponent component,
            ICollection<HistoricalPoliticalChangeRecord> pendingChanges,
            ICollection<Action> pendingMutations,
            string targetId,
            string changeType,
            string oldValue,
            string newValue,
            HistoricalPoliticalChangeSource source,
            Action mutation,
            out string error)
        {
            HistoricalPoliticalChangeRecord change;
            if (!TryCreateChange(component, targetId, changeType, oldValue, newValue, source, out change, out error))
            {
                return false;
            }

            pendingChanges.Add(change);
            pendingMutations.Add(mutation);
            return true;
        }

        private static bool CanChangeWorldPolitics(HistoricalPoliticalChangeSource source, out string error)
        {
            error = null;
            if (source == HistoricalPoliticalChangeSource.HistoricalEvent
                || source == HistoricalPoliticalChangeSource.AdministrativeAction) return true;
            if (source == HistoricalPoliticalChangeSource.Debug && Prefs.DevMode) return true;
            return Reject("World-level political changes require a historical event, an administrative action, or developer mode.", out error);
        }

        private static bool CanChangeLocalPolitics(HistoricalPoliticalChangeSource source, out string error)
        {
            error = null;
            if (source == HistoricalPoliticalChangeSource.HistoricalEvent
                || source == HistoricalPoliticalChangeSource.PlayerDecision
                || source == HistoricalPoliticalChangeSource.LocalIncident
                || source == HistoricalPoliticalChangeSource.AdministrativeAction) return true;
            if (source == HistoricalPoliticalChangeSource.Debug && Prefs.DevMode) return true;
            return Reject("The requested source is not allowed to change local community politics.", out error);
        }

        private static bool IsUsablePolity(HistoricalPoliticsGameComponent component, string polityId)
        {
            if (polityId == null) return true;
            HistoricalPolityState polity = component?.FindPolity(polityId);
            return polity != null && polity.Status != HistoricalPolityStatus.Dissolved;
        }

        private static string CommunitySnapshot(HistoricalLocalCommunityState community)
        {
            return "controller=" + Value(community.EffectiveControllerId)
                + "|allegiance=" + Value(community.AllegiancePolityId)
                + "|posture=" + community.Posture;
        }

        private static string Value(string value)
        {
            return value ?? "(none)";
        }

        private static bool Reject(string message, out string error)
        {
            error = message;
            return false;
        }

        private static HistoricalPoliticsGameComponent GetCurrentComponent()
        {
            return Current.Game == null ? null : Current.Game.GetComponent<HistoricalPoliticsGameComponent>();
        }

        private static HistoricalPoliticsGameComponent GetInitializedComponent()
        {
            HistoricalPoliticsGameComponent component = GetCurrentComponent();
            return component != null && component.Initialized ? component : null;
        }
    }
}
