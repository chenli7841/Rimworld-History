using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Politics
{
    public static class HistoricalPoliticsValidator
    {
        public static IEnumerable<string> ValidateDomainDef(HistoricalDomainDef domain)
        {
            if (domain == null) yield break;
            if (!HasHistoricalPrefix(domain.defName)) yield return "defName must use the HR_ prefix.";
            if (domain.parentPolity == null) yield return "parentPolity is required.";
            if (domain.region == null) yield return "region is required.";
        }

        public static IEnumerable<string> ValidateSetup(HistoricalPoliticalSetupDef setup)
        {
            if (setup == null)
            {
                yield return "Political setup Def is null.";
                yield break;
            }

            if (!HasHistoricalPrefix(setup.defName)) yield return "defName must use the HR_ prefix.";
            if (setup.scenario == null)
            {
                yield return "scenario is required.";
                yield break;
            }

            if (setup.polities == null) yield return "polities list is required.";
            if (setup.regions == null) yield return "regions list is required.";
            if (setup.domains == null) yield return "domains list is required.";
            if (setup.relations == null) yield return "relations list is required.";
            if (setup.communities == null) yield return "communities list is required.";
            if (setup.polities == null || setup.regions == null || setup.domains == null
                || setup.relations == null || setup.communities == null) yield break;

            List<string> errors = new List<string>();
            Dictionary<string, HistoricalInitialPolityRecord> polities = BuildIndex(
                setup.polities, item => item.polityId, "polities", errors);
            Dictionary<string, HistoricalInitialRegionRecord> regions = BuildIndex(
                setup.regions, item => item.regionId, "regions", errors);
            Dictionary<string, HistoricalInitialDomainRecord> domains = BuildIndex(
                setup.domains, item => item.domainId, "domains", errors);
            BuildIndex(setup.communities, item => item.communityId, "communities", errors);

            HashSet<string> scenarioPolities = new HashSet<string>(
                HistoricalDataQuery.GetStates(setup.scenario).Where(item => item != null).Select(item => item.defName),
                StringComparer.Ordinal);
            HashSet<string> scenarioRegions = new HashSet<string>(
                HistoricalDataQuery.GetRegions(setup.scenario).Where(item => item != null).Select(item => item.defName),
                StringComparer.Ordinal);

            foreach (HistoricalStateDef stateDef in HistoricalDataQuery.GetStates(setup.scenario).Where(item => item != null))
            {
                if (!polities.ContainsKey(stateDef.defName))
                {
                    errors.Add("Missing initial polity record for " + stateDef.defName + ".");
                }
            }

            foreach (KeyValuePair<string, HistoricalInitialPolityRecord> pair in polities)
            {
                HistoricalInitialPolityRecord polity = pair.Value;
                if (!scenarioPolities.Contains(pair.Key)) errors.Add("Polity " + pair.Key + " is not listed by the scenario.");
                ValidatePolityReference(polities, polity.nominalOverlordId, pair.Key, "nominalOverlordId", errors);
                ValidatePolityReference(polities, polity.effectiveOverlordId, pair.Key, "effectiveOverlordId", errors);

                if (polity.successorPolityIds != null)
                {
                    HashSet<string> successors = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string successorId in polity.successorPolityIds)
                    {
                        ValidatePolityReference(polities, successorId, pair.Key, "successorPolityIds", errors);
                        if (!successors.Add(successorId)) errors.Add(pair.Key + " has duplicate successor " + successorId + ".");
                    }
                }
            }

            ValidateHierarchyCycles(polities, record => record.nominalOverlordId, "nominal", errors);
            ValidateHierarchyCycles(polities, record => record.effectiveOverlordId, "effective", errors);

            HashSet<string> relationKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (HistoricalInitialRelationRecord relation in setup.relations)
            {
                if (relation == null)
                {
                    errors.Add("relations contains an empty record.");
                    continue;
                }

                ValidatePolityReference(polities, relation.sourcePolityId, "relation", "sourcePolityId", errors);
                ValidatePolityReference(polities, relation.targetPolityId, "relation", "targetPolityId", errors);
                if (string.Equals(relation.sourcePolityId, relation.targetPolityId, StringComparison.Ordinal))
                {
                    errors.Add("A political relation cannot reference the same polity at both ends: " + relation.sourcePolityId + ".");
                }

                string first = relation.sourcePolityId ?? string.Empty;
                string second = relation.targetPolityId ?? string.Empty;
                if (HistoricalPoliticalRelationRules.IsSymmetricForLookup(relation.relationType)
                    && string.CompareOrdinal(first, second) > 0)
                {
                    string swap = first;
                    first = second;
                    second = swap;
                }

                string identity = ((int)relation.relationType) + "|" + first + "|" + second;
                if (relation.isActive && !relationKeys.Add(identity))
                {
                    errors.Add("Duplicate active political relation: " + identity + ".");
                }
            }

            foreach (KeyValuePair<string, HistoricalInitialPolityRecord> pair in polities)
            {
                if (pair.Value.nominalOverlordId != null && !HasActiveRelation(
                    setup.relations, pair.Key, pair.Value.nominalOverlordId, HistoricalPoliticalRelationType.NominalSuzerainty))
                {
                    errors.Add(pair.Key + " has a nominal overlord but no matching NominalSuzerainty relation record.");
                }

                if (pair.Value.effectiveOverlordId != null && !HasActiveRelation(
                    setup.relations, pair.Key, pair.Value.effectiveOverlordId, HistoricalPoliticalRelationType.EffectiveSubordination))
                {
                    errors.Add(pair.Key + " has an effective overlord but no matching EffectiveSubordination relation record.");
                }
            }

            foreach (HistoricalRegionDef regionDef in HistoricalDataQuery.GetRegions(setup.scenario).Where(item => item != null))
            {
                if (!regions.ContainsKey(regionDef.defName))
                {
                    errors.Add("Missing initial region record for " + regionDef.defName + ".");
                }
            }

            foreach (HistoricalInitialRelationRecord relation in setup.relations.Where(item => item != null && item.isActive))
            {
                if (relation.relationType == HistoricalPoliticalRelationType.NominalSuzerainty)
                {
                    HistoricalInitialPolityRecord source;
                    if (polities.TryGetValue(relation.sourcePolityId, out source)
                        && source.nominalOverlordId != relation.targetPolityId)
                    {
                        errors.Add("Active NominalSuzerainty relation does not match the polity's nominalOverlordId.");
                    }
                }
                else if (relation.relationType == HistoricalPoliticalRelationType.EffectiveSubordination)
                {
                    HistoricalInitialPolityRecord source;
                    if (polities.TryGetValue(relation.sourcePolityId, out source)
                        && source.effectiveOverlordId != relation.targetPolityId)
                    {
                        errors.Add("Active EffectiveSubordination relation does not match the polity's effectiveOverlordId.");
                    }
                }

                HistoricalInitialPolityRecord sourcePolity;
                HistoricalInitialPolityRecord targetPolity;
                if (polities.TryGetValue(relation.sourcePolityId, out sourcePolity)
                    && polities.TryGetValue(relation.targetPolityId, out targetPolity)
                    && (sourcePolity.status == HistoricalPolityStatus.Dissolved || targetPolity.status == HistoricalPolityStatus.Dissolved))
                {
                    errors.Add("An active political relation cannot reference a dissolved polity.");
                }
            }

            foreach (HistoricalStateDef state in HistoricalDataQuery.GetStates(setup.scenario).Where(item => item != null))
            {
                HistoricalInitialPolityRecord record;
                if (polities.TryGetValue(state.defName, out record) && record.status == HistoricalPolityStatus.Dissolved)
                {
                    if (polities.Values.Any(item => item.nominalOverlordId == state.defName || item.effectiveOverlordId == state.defName))
                    {
                        errors.Add("Dissolved polity " + state.defName + " cannot be an active polity's overlord.");
                    }
                    if (regions.Values.Any(item => item.nominalOwnerId == state.defName
                        || item.effectiveControllerId == state.defName || item.militaryOccupierId == state.defName))
                    {
                        errors.Add("Dissolved polity " + state.defName + " cannot be an initial region owner, controller, or occupier.");
                    }
                }
            }

            foreach (HistoricalStateDef stateDef in HistoricalDataQuery.GetStates(setup.scenario).Where(item => item != null))
            {
                HistoricalInitialPolityRecord record;
                string staticNominalOverlordId = stateDef.nominalOverlord == null ? null : stateDef.nominalOverlord.defName;
                if (polities.TryGetValue(stateDef.defName, out record)
                    && record.nominalOverlordId != staticNominalOverlordId)
                {
                    errors.Add(stateDef.defName + " initial nominal overlord does not match its static scenario definition.");
                }
            }

            foreach (KeyValuePair<string, HistoricalInitialRegionRecord> pair in regions)
            {
                if (!scenarioRegions.Contains(pair.Key)) errors.Add("Region " + pair.Key + " is not listed by the scenario.");
                ValidatePolityReference(polities, pair.Value.nominalOwnerId, pair.Key, "nominalOwnerId", errors);
                ValidatePolityReference(polities, pair.Value.effectiveControllerId, pair.Key, "effectiveControllerId", errors);
                ValidatePolityReference(polities, pair.Value.militaryOccupierId, pair.Key, "militaryOccupierId", errors);
            }

            foreach (KeyValuePair<string, HistoricalInitialDomainRecord> pair in domains)
            {
                HistoricalDomainDef domainDef = DefDatabase<HistoricalDomainDef>.GetNamedSilentFail(pair.Key);
                if (domainDef == null)
                {
                    errors.Add("No HistoricalDomainDef exists for " + pair.Key + ".");
                }
                else
                {
                    if (domainDef.parentPolity == null || domainDef.parentPolity.defName != pair.Value.parentPolityId)
                    {
                        errors.Add(pair.Key + " setup parent does not match its HistoricalDomainDef.");
                    }

                    if (domainDef.region != null && !scenarioRegions.Contains(domainDef.region.defName))
                    {
                        errors.Add(pair.Key + " region is not included in the scenario.");
                    }
                }

                ValidatePolityReference(polities, pair.Value.parentPolityId, pair.Key, "parentPolityId", errors);
                ValidatePolityReference(polities, pair.Value.nominalOwnerId, pair.Key, "nominalOwnerId", errors);
                ValidatePolityReference(polities, pair.Value.effectiveControllerId, pair.Key, "effectiveControllerId", errors);
            }

            foreach (HistoricalInitialCommunityRecord community in setup.communities)
            {
                if (community == null) continue;
                if (!scenarioRegions.Contains(community.regionId)) errors.Add(community.communityId + " references a region outside the scenario.");
                if (community.domainId != null && !domains.ContainsKey(community.domainId)) errors.Add(community.communityId + " references an undefined domain.");
                ValidatePolityReference(polities, community.nominalPolityId, community.communityId, "nominalPolityId", errors);
                ValidatePolityReference(polities, community.administrativePolityId, community.communityId, "administrativePolityId", errors);
                ValidatePolityReference(polities, community.effectiveControllerId, community.communityId, "effectiveControllerId", errors);
                ValidatePolityReference(polities, community.allegiancePolityId, community.communityId, "allegiancePolityId", errors);
            }

            HistoricalInitialCommunityRecord playerCommunity = setup.communities.FirstOrDefault(item =>
                item != null && item.communityId == "HR_Community_Player_001");
            if (playerCommunity == null)
            {
                errors.Add("A default HR_Community_Player_001 record is required for the scenario.");
            }
            else
            {
                if (playerCommunity.regionId != setup.scenario.defaultStartingRegion?.defName)
                {
                    errors.Add("The default player community must use the scenario's defaultStartingRegion.");
                }
                if (playerCommunity.nominalPolityId != setup.scenario.defaultStartingState?.defName)
                {
                    errors.Add("The default player community must use the scenario's defaultStartingState as its nominal polity.");
                }
            }

            foreach (string error in errors) yield return error;
        }

        private static Dictionary<string, TRecord> BuildIndex<TRecord>(
            IEnumerable<TRecord> records,
            Func<TRecord, string> getId,
            string listName,
            ICollection<string> errors)
        {
            Dictionary<string, TRecord> result = new Dictionary<string, TRecord>(StringComparer.Ordinal);
            foreach (TRecord record in records)
            {
                if (record == null)
                {
                    errors.Add(listName + " contains an empty record.");
                    continue;
                }

                string id = getId(record);
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add(listName + " contains a record with no stable ID.");
                    continue;
                }

                if (!HasHistoricalPrefix(id)) errors.Add(listName + " ID must use the HR_ prefix: " + id + ".");
                if (!result.ContainsKey(id)) result.Add(id, record);
                else errors.Add(listName + " contains duplicate ID " + id + ".");
            }
            return result;
        }

        private static void ValidatePolityReference(
            IDictionary<string, HistoricalInitialPolityRecord> polities,
            string targetId,
            string ownerId,
            string fieldName,
            ICollection<string> errors)
        {
            if (targetId != null && !polities.ContainsKey(targetId))
            {
                errors.Add(ownerId + " references missing polity " + targetId + " in " + fieldName + ".");
            }
            if (targetId != null && targetId == ownerId)
            {
                errors.Add(ownerId + " cannot reference itself in " + fieldName + ".");
            }
        }

        private static void ValidateHierarchyCycles(
            IDictionary<string, HistoricalInitialPolityRecord> polities,
            Func<HistoricalInitialPolityRecord, string> getParent,
            string hierarchyName,
            ICollection<string> errors)
        {
            foreach (string startId in polities.Keys)
            {
                HashSet<string> path = new HashSet<string>(StringComparer.Ordinal);
                string currentId = startId;
                while (currentId != null)
                {
                    if (!path.Add(currentId))
                    {
                        errors.Add("The " + hierarchyName + " polity hierarchy contains a cycle involving " + currentId + ".");
                        break;
                    }

                    HistoricalInitialPolityRecord current;
                    currentId = polities.TryGetValue(currentId, out current) ? getParent(current) : null;
                }
            }
        }

        private static bool HasActiveRelation(
            IEnumerable<HistoricalInitialRelationRecord> relations,
            string sourceId,
            string targetId,
            HistoricalPoliticalRelationType type)
        {
            return relations.Any(item => item != null && item.isActive
                && item.relationType == type && item.sourcePolityId == sourceId && item.targetPolityId == targetId);
        }

        private static bool HasHistoricalPrefix(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && id.StartsWith("HR_", StringComparison.Ordinal);
        }
    }
}
