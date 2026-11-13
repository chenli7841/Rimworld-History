using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalPoliticsGameComponent : GameComponent
    {
        public HistoricalPoliticsGameComponent(Game game) { }

        private string scenarioDefName;
        private string setupDefName;
        private List<HistoricalPolityState> polities = new List<HistoricalPolityState>();
        private List<HistoricalDomainState> domains = new List<HistoricalDomainState>();
        private List<HistoricalRegionPoliticalState> regions = new List<HistoricalRegionPoliticalState>();
        private List<HistoricalPoliticalRelation> relations = new List<HistoricalPoliticalRelation>();
        private List<HistoricalLocalCommunityState> communities = new List<HistoricalLocalCommunityState>();
        private List<HistoricalPoliticalChangeRecord> changes = new List<HistoricalPoliticalChangeRecord>();
        private List<HistoricalFactionMapping> factionMappings = new List<HistoricalFactionMapping>();
        private string politicalPeriodId = "WesternZhouLate";
        private string currentRulerCharacterId = "HR_Character_ZhouYouWang";
        private string successionStatus = "RoyalAuthorityActive";
        private string royalCapitalStatus = "HaojingRoyalAuthorityActive";
        private bool eastwardTransitionUnlocked;
        private bool eastwardMigrationCompleted;
        private List<string> successionClaimantCharacterIds = new List<string>();
        private List<string> appliedHistoricalOutcomeIds = new List<string>();
        private int nextChangeSequence = 1;
        private bool initialized;

        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public string SetupDefName => setupDefName;
        public IEnumerable<HistoricalPolityState> Polities => polities ?? Enumerable.Empty<HistoricalPolityState>();
        public IEnumerable<HistoricalDomainState> Domains => domains ?? Enumerable.Empty<HistoricalDomainState>();
        public IEnumerable<HistoricalRegionPoliticalState> Regions => regions ?? Enumerable.Empty<HistoricalRegionPoliticalState>();
        public IEnumerable<HistoricalPoliticalRelation> Relations => relations ?? Enumerable.Empty<HistoricalPoliticalRelation>();
        public IEnumerable<HistoricalLocalCommunityState> Communities => communities ?? Enumerable.Empty<HistoricalLocalCommunityState>();
        public IEnumerable<HistoricalPoliticalChangeRecord> Changes => changes ?? Enumerable.Empty<HistoricalPoliticalChangeRecord>();
        public IEnumerable<HistoricalFactionMapping> FactionMappings => factionMappings ?? Enumerable.Empty<HistoricalFactionMapping>();
        public string PoliticalPeriodId => politicalPeriodId;
        public string CurrentRulerCharacterId => currentRulerCharacterId;
        public string SuccessionStatus => successionStatus;
        public string RoyalCapitalStatus => royalCapitalStatus;
        public bool EastwardTransitionUnlocked => eastwardTransitionUnlocked;
        public bool EastwardMigrationCompleted => eastwardMigrationCompleted;
        public IEnumerable<string> SuccessionClaimantCharacterIds => successionClaimantCharacterIds ?? Enumerable.Empty<string>();
        public bool HasAppliedHistoricalOutcome(string id) => appliedHistoricalOutcomeIds?.Contains(id) == true;

        public HistoricalPolityState FindPolity(string id) =>
            polities?.FirstOrDefault(item => item != null && item.PolityId == id);

        public HistoricalDomainState FindDomain(string id) =>
            domains?.FirstOrDefault(item => item != null && item.DomainId == id);

        public HistoricalRegionPoliticalState FindRegion(string id) =>
            regions?.FirstOrDefault(item => item != null && item.RegionId == id);

        public HistoricalLocalCommunityState FindCommunity(string id) =>
            communities?.FirstOrDefault(item => item != null && item.CommunityId == id);

        public HistoricalPoliticalRelation FindRelation(
            string sourceId,
            string targetId,
            HistoricalPoliticalRelationType type,
            bool activeOnly)
        {
            return relations?.FirstOrDefault(item => item != null
                && (!activeOnly || item.IsActive)
                && HistoricalPoliticalRelationRules.HasSameIdentity(
                    item.SourcePolityId, item.TargetPolityId, item.RelationType,
                    sourceId, targetId, type));
        }

        internal bool ApplyInitializationPlan(HistoricalPoliticsInitializationPlan plan)
        {
            if (plan == null) return false;
            if (initialized) return string.Equals(scenarioDefName, plan.ScenarioDefName, StringComparison.Ordinal);

            scenarioDefName = plan.ScenarioDefName;
            setupDefName = plan.SetupDefName;
            polities = plan.Polities;
            regions = plan.Regions;
            domains = plan.Domains;
            relations = plan.Relations;
            communities = plan.Communities;
            changes = new List<HistoricalPoliticalChangeRecord>();
            factionMappings = new List<HistoricalFactionMapping>();
            nextChangeSequence = 1;
            initialized = true;
            return true;
        }

        internal void AddChange(HistoricalPoliticalChangeRecord change)
        {
            if (change == null) return;
            if (changes == null) changes = new List<HistoricalPoliticalChangeRecord>();
            changes.Add(change);
        }

        internal void AddRelation(HistoricalPoliticalRelation relation)
        {
            if (relation == null) return;
            if (relations == null) relations = new List<HistoricalPoliticalRelation>();
            relations.Add(relation);
        }

        internal string NextChangeId()
        {
            string id = "HR_Change_" + (scenarioDefName ?? "Unknown") + "_"
                + nextChangeSequence.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
            nextChangeSequence++;
            return id;
        }

        internal void AddFactionMapping(HistoricalFactionMapping mapping)
        {
            if (mapping == null) return;
            if (factionMappings == null) factionMappings = new List<HistoricalFactionMapping>();
            factionMappings.RemoveAll(item => item == null || item.HistoricalPolityId == mapping.HistoricalPolityId);
            factionMappings.Add(mapping);
        }

        internal bool ApplyHe001Outcome(string outcomeId)
        {
            if (!initialized || string.IsNullOrWhiteSpace(outcomeId)) return false;
            if (HasAppliedHistoricalOutcome(outcomeId)) return true;
            switch (outcomeId)
            {
                case "HE001_YouWangDead":
                    currentRulerCharacterId = null;
                    successionStatus = "VacantUnderCrisis";
                    break;
                case "HE001_WesternZhouCollapse":
                    politicalPeriodId = "WesternZhouCollapse";
                    royalCapitalStatus = "HaojingRoyalAuthorityDisrupted";
                    break;
                case "HE001_RoyalSuccessionCrisis":
                    successionStatus = "SuccessionCrisis";
                    if (successionClaimantCharacterIds == null) successionClaimantCharacterIds = new List<string>();
                    if (!successionClaimantCharacterIds.Contains("HR_Character_YiJiu")) successionClaimantCharacterIds.Add("HR_Character_YiJiu");
                    break;
                case "HE001_EastwardTransitionUnlocked":
                    eastwardTransitionUnlocked = true;
                    break;
                default: return false;
            }
            if (appliedHistoricalOutcomeIds == null) appliedHistoricalOutcomeIds = new List<string>();
            appliedHistoricalOutcomeIds.Add(outcomeId);
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref scenarioDefName, "politicsScenarioDefName");
            Scribe_Values.Look(ref setupDefName, "politicsSetupDefName");
            Scribe_Collections.Look(ref polities, "polities", LookMode.Deep);
            Scribe_Collections.Look(ref domains, "domains", LookMode.Deep);
            Scribe_Collections.Look(ref regions, "politicalRegions", LookMode.Deep);
            Scribe_Collections.Look(ref relations, "politicalRelations", LookMode.Deep);
            Scribe_Collections.Look(ref communities, "localCommunities", LookMode.Deep);
            Scribe_Collections.Look(ref changes, "politicalChanges", LookMode.Deep);
            Scribe_Collections.Look(ref factionMappings, "factionMappings", LookMode.Deep);
            Scribe_Values.Look(ref politicalPeriodId, "politicalPeriodId", "WesternZhouLate");
            Scribe_Values.Look(ref currentRulerCharacterId, "currentRulerCharacterId", "HR_Character_ZhouYouWang");
            Scribe_Values.Look(ref successionStatus, "successionStatus", "RoyalAuthorityActive");
            Scribe_Values.Look(ref royalCapitalStatus, "royalCapitalStatus", "HaojingRoyalAuthorityActive");
            Scribe_Values.Look(ref eastwardTransitionUnlocked, "eastwardTransitionUnlocked", false);
            Scribe_Values.Look(ref eastwardMigrationCompleted, "eastwardMigrationCompleted", false);
            Scribe_Collections.Look(ref successionClaimantCharacterIds, "successionClaimantCharacterIds", LookMode.Value);
            Scribe_Collections.Look(ref appliedHistoricalOutcomeIds, "appliedHistoricalOutcomeIds", LookMode.Value);
            Scribe_Values.Look(ref nextChangeSequence, "nextChangeSequence", 1);
            Scribe_Values.Look(ref initialized, "politicsInitialized", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                polities = polities ?? new List<HistoricalPolityState>();
                domains = domains ?? new List<HistoricalDomainState>();
                regions = regions ?? new List<HistoricalRegionPoliticalState>();
                relations = relations ?? new List<HistoricalPoliticalRelation>();
                communities = communities ?? new List<HistoricalLocalCommunityState>();
                changes = changes ?? new List<HistoricalPoliticalChangeRecord>();
                factionMappings = factionMappings ?? new List<HistoricalFactionMapping>();
                successionClaimantCharacterIds = successionClaimantCharacterIds ?? new List<string>();
                appliedHistoricalOutcomeIds = appliedHistoricalOutcomeIds ?? new List<string>();
                nextChangeSequence = Math.Max(nextChangeSequence, changes.Count + 1);

                if (initialized)
                {
                    if (string.IsNullOrWhiteSpace(scenarioDefName))
                    {
                        Log.Error("[Historical RimWorld] Political state is initialized but has no scenario ID; saved politics were preserved.");
                    }
                    else if (HistoricalScenarioRegistry.TryGetById(scenarioDefName, out _) == false)
                    {
                        Log.Warning("[Historical RimWorld] Political state references missing scenario Def " + scenarioDefName + "; saved IDs were preserved.");
                    }

                    WarnForMissingDefinitions();
                }
            }
        }

        private void WarnForMissingDefinitions()
        {
            foreach (HistoricalPolityState state in polities.Where(item => item != null))
            {
                if (DefDatabase<HistoricalStateDef>.GetNamedSilentFail(state.PolityId) == null)
                {
                    Log.Warning("[Historical RimWorld] Saved politics reference missing polity Def " + state.PolityId + "; the ID was preserved.");
                }
            }

            foreach (HistoricalPolityState state in polities.Where(item => item != null))
            {
                WarnForMissingPolityReference(state.NominalOverlordId, "nominal overlord of " + state.PolityId);
                WarnForMissingPolityReference(state.EffectiveOverlordId, "effective overlord of " + state.PolityId);
                foreach (string successorId in state.SuccessorPolityIds)
                {
                    WarnForMissingPolityReference(successorId, "successor of " + state.PolityId);
                }
            }

            foreach (HistoricalRegionPoliticalState region in regions.Where(item => item != null))
            {
                if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(region.RegionId) == null)
                {
                    Log.Warning("[Historical RimWorld] Saved politics reference missing region Def " + region.RegionId + "; the ID was preserved.");
                }
                WarnForMissingPolityReference(region.NominalOwnerId, "nominal owner of " + region.RegionId);
                WarnForMissingPolityReference(region.EffectiveControllerId, "controller of " + region.RegionId);
                WarnForMissingPolityReference(region.MilitaryOccupierId, "occupier of " + region.RegionId);
            }

            foreach (HistoricalDomainState domain in domains.Where(item => item != null))
            {
                if (DefDatabase<HistoricalDomainDef>.GetNamedSilentFail(domain.DomainId) == null)
                {
                    Log.Warning("[Historical RimWorld] Saved politics reference missing domain Def " + domain.DomainId + "; the ID was preserved.");
                }
                WarnForMissingPolityReference(domain.ParentPolityId, "parent of " + domain.DomainId);
                WarnForMissingPolityReference(domain.NominalOwnerId, "nominal owner of " + domain.DomainId);
                WarnForMissingPolityReference(domain.EffectiveControllerId, "controller of " + domain.DomainId);
            }

            foreach (HistoricalPoliticalRelation relation in relations.Where(item => item != null))
            {
                WarnForMissingPolityReference(relation.SourcePolityId, "relation source");
                WarnForMissingPolityReference(relation.TargetPolityId, "relation target");
            }

            foreach (HistoricalLocalCommunityState community in communities.Where(item => item != null))
            {
                if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(community.RegionId) == null)
                {
                    Log.Warning("[Historical RimWorld] Saved community references missing region Def " + community.RegionId + "; the ID was preserved.");
                }
                if (community.DomainId != null && DefDatabase<HistoricalDomainDef>.GetNamedSilentFail(community.DomainId) == null)
                {
                    Log.Warning("[Historical RimWorld] Saved community references missing domain Def " + community.DomainId + "; the ID was preserved.");
                }
                WarnForMissingPolityReference(community.NominalPolityId, "nominal polity of " + community.CommunityId);
                WarnForMissingPolityReference(community.AdministrativePolityId, "administrative polity of " + community.CommunityId);
                WarnForMissingPolityReference(community.EffectiveControllerId, "controller of " + community.CommunityId);
                WarnForMissingPolityReference(community.AllegiancePolityId, "allegiance of " + community.CommunityId);
            }
        }

        private static void WarnForMissingPolityReference(string id, string role)
        {
            if (id != null && DefDatabase<HistoricalStateDef>.GetNamedSilentFail(id) == null)
            {
                Log.Warning("[Historical RimWorld] Saved politics contain missing polity Def " + id + " as " + role + "; the ID was preserved.");
            }
        }
    }
}
