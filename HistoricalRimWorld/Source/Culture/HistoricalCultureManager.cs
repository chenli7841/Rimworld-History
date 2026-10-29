using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Culture
{
    public static class HistoricalCultureManager
    {
        private static HistoricalCultureGameComponent Component => Current.Game?.GetComponent<HistoricalCultureGameComponent>();
        public static bool IsActive => Component?.Initialized == true && HistoricalScenarioManager.IsScenarioActive;
        public static IEnumerable<HistoricalPersonCultureState> Persons => Component?.Persons ?? Enumerable.Empty<HistoricalPersonCultureState>();
        public static IEnumerable<HistoricalFamilyCultureState> Families => Component?.Families ?? Enumerable.Empty<HistoricalFamilyCultureState>();
        public static IEnumerable<HistoricalCommunityCultureState> Communities => Component?.Communities ?? Enumerable.Empty<HistoricalCommunityCultureState>();
        public static HistoricalPersonCultureState FindPerson(string id) => Component?.FindPerson(id);
        public static HistoricalFamilyCultureState FindFamily(string id) => Component?.FindFamily(id);
        public static HistoricalCommunityCultureState FindCommunity(string id) => Component?.FindCommunity(id);

        public static bool TryInitializeForScenario(HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (scenario == null || HistoricalScenarioManager.CurrentScenario?.defName != scenario.defName)
                return Fail("Scenario must be active before cultural state is initialized.", out error);
            HistoricalCultureGameComponent component = Component;
            if (component == null) return Fail("HistoricalCultureGameComponent is unavailable.", out error);
            if (component.Initialized && component.ScenarioDefName != scenario.defName) return Fail("Cultural state belongs to another scenario.", out error);
            if (!component.Initialized)
            {
                List<HistoricalCultureSetupDef> setups = DefDatabase<HistoricalCultureSetupDef>.AllDefsListForReading
                    .Where(item => item.scenario?.defName == scenario.defName).ToList();
                if (setups.Count != 1) return Fail("Expected exactly one HistoricalCultureSetupDef for " + scenario.defName + ".", out error);
                HistoricalCultureSetupDef setup = setups[0];
                if (setup.culturalPresence != null && setup.culturalPresence.Any(item => item?.culture == null
                    || float.IsNaN(item.familiarity) || item.familiarity < 0f || item.familiarity > 1f))
                    return Fail("Culture setup contains an invalid presence record.", out error);
                if (setup.activeCustomIds != null && setup.activeCustomIds.Any(id => DefDatabase<HistoricalCustomDef>.GetNamedSilentFail(id) == null))
                    return Fail("Culture setup references an unknown custom.", out error);
                if (!component.Initialize(scenario.defName, setup)) return Fail("Could not seed community culture.", out error);
            }

            foreach (HistoricalPersonRecord person in HistoricalGenealogyManager.Persons) TryEnsurePerson(person?.PersonId, out _);
            foreach (HistoricalFamilyRecord family in HistoricalGenealogyManager.Families) TryEnsureFamily(family?.FamilyId, family?.FounderPersonId, out _);
            return true;
        }

        public static bool TryEnsurePerson(string personId, out string error)
        {
            error = null;
            if (!IsActive) return Fail("Historical culture is inactive.", out error);
            if (string.IsNullOrWhiteSpace(personId)) return Fail("PersonId is required.", out error);
            if (Component.FindPerson(personId) != null) return true;
            HistoricalPersonRecord person = HistoricalGenealogyManager.FindPerson(personId);
            if (person == null) return Fail("Historical person was not found.", out error);
            HistoricalCommunityCultureState community = Communities.FirstOrDefault();
            string birthCultureId = null;
            if (!string.IsNullOrWhiteSpace(person.BirthRegionId))
                birthCultureId = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(person.BirthRegionId)?.associatedCultures?.FirstOrDefault()?.defName;
            birthCultureId = birthCultureId ?? community?.DominantCultureId;
            if (DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(birthCultureId) == null)
                return Fail("No valid culture can be resolved for the historical person.", out error);
            HistoricalFamilyCultureState family = Component.FindFamily(person.FamilyId);
            string primaryCultureId = family?.PrimaryCultureId ?? birthCultureId;
            HistoricalPersonCultureState state = new HistoricalPersonCultureState
            {
                PersonId = personId,
                BirthCultureId = birthCultureId,
                PrimaryCultureId = primaryCultureId,
                Familiarities = new List<HistoricalCulturalFamiliarity>
                {
                    NewFamiliarity(birthCultureId, 0.75f, HistoricalCulturalTransmissionSource.Family),
                    NewFamiliarity(primaryCultureId, 0.75f, HistoricalCulturalTransmissionSource.Community)
                }.GroupBy(item => item.CultureId).Select(group => group.First()).ToList()
            };
            Component.Add(state);
            if (!string.IsNullOrWhiteSpace(person.FamilyId)) TryEnsureFamily(person.FamilyId, personId, out _);
            return true;
        }

        public static bool TryEnsureFamily(string familyId, string founderPersonId, out string error)
        {
            error = null;
            if (!IsActive) return Fail("Historical culture is inactive.", out error);
            if (string.IsNullOrWhiteSpace(familyId)) return Fail("FamilyId is required.", out error);
            if (Component.FindFamily(familyId) != null) return true;
            HistoricalPersonCultureState founder = FindPerson(founderPersonId);
            if (founder == null && !string.IsNullOrWhiteSpace(founderPersonId)) TryEnsurePerson(founderPersonId, out _);
            founder = FindPerson(founderPersonId);
            string primary = founder?.PrimaryCultureId ?? Communities.FirstOrDefault()?.DominantCultureId;
            if (DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(primary) == null) return Fail("Family has no valid primary culture.", out error);
            Component.Add(new HistoricalFamilyCultureState { FamilyId = familyId, PrimaryCultureId = primary, PreservedTraditionIds = new List<string>() });
            return true;
        }

        public static bool TryIncreaseFamiliarity(string personId, string cultureId, float amount, HistoricalCulturalTransmissionSource source, out string error)
        {
            error = null;
            if (!TryEnsurePerson(personId, out error)) return false;
            if (DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(cultureId) == null) return Fail("Unknown culture " + cultureId + ".", out error);
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return Fail("Familiarity increase must be positive and finite.", out error);
            HistoricalPersonCultureState person = FindPerson(personId);
            HistoricalCulturalFamiliarity familiarity = person.FindFamiliarity(cultureId);
            if (familiarity == null)
            {
                familiarity = NewFamiliarity(cultureId, 0f, source);
                person.Familiarities.Add(familiarity);
            }
            familiarity.Familiarity = Math.Min(1f, Math.Max(0f, familiarity.Familiarity) + amount);
            familiarity.LastSource = source;
            familiarity.LastChangedGameTick = Find.TickManager?.TicksGame ?? -1;
            return true;
        }

        public static bool TrySetPrimaryCulture(string personId, string cultureId, out string error)
        {
            error = null;
            if (!TryEnsurePerson(personId, out error)) return false;
            if (DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(cultureId) == null) return Fail("Unknown culture " + cultureId + ".", out error);
            HistoricalPersonCultureState person = FindPerson(personId);
            person.PrimaryCultureId = cultureId;
            if (person.FindFamiliarity(cultureId) == null) person.Familiarities.Add(NewFamiliarity(cultureId, 0.2f, HistoricalCulturalTransmissionSource.Migration));
            return true;
        }

        public static bool TryAddPreservedTradition(string familyId, string traditionId, out string error)
        {
            error = null;
            HistoricalFamilyCultureState family = FindFamily(familyId);
            if (!IsActive || family == null) return Fail("Historical family culture was not found.", out error);
            if (string.IsNullOrWhiteSpace(traditionId)) return Fail("Tradition ID is required.", out error);
            HistoricalCultureDef culture = DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(family.PrimaryCultureId);
            if (culture == null || culture.traditionIds == null || !culture.traditionIds.Contains(traditionId))
                return Fail("Tradition is not defined by the family's primary culture.", out error);
            if (!family.PreservedTraditionIds.Contains(traditionId)) family.PreservedTraditionIds.Add(traditionId);
            return true;
        }

        public static void InheritFamilyCulture(string parentPersonId, string childPersonId)
        {
            if (!IsActive || !TryEnsurePerson(parentPersonId, out _) || !TryEnsurePerson(childPersonId, out _)) return;
            HistoricalPersonCultureState parent = FindPerson(parentPersonId);
            HistoricalPersonCultureState child = FindPerson(childPersonId);
            HistoricalFamilyCultureState family = FindFamily(HistoricalGenealogyManager.FindPerson(childPersonId)?.FamilyId);
            if (parent == null || child == null || family == null) return;
            family.PrimaryCultureId = family.PrimaryCultureId ?? parent.PrimaryCultureId;
            if (DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(family.PrimaryCultureId) != null)
            {
                child.PrimaryCultureId = family.PrimaryCultureId;
                if (child.FindFamiliarity(family.PrimaryCultureId) == null)
                    child.Familiarities.Add(NewFamiliarity(family.PrimaryCultureId, 0.35f, HistoricalCulturalTransmissionSource.Family));
            }
        }

        public static void ExchangeByMarriage(string personIdA, string personIdB)
        {
            if (!IsActive || !TryEnsurePerson(personIdA, out _) || !TryEnsurePerson(personIdB, out _)) return;
            HistoricalPersonCultureState a = FindPerson(personIdA), b = FindPerson(personIdB);
            if (a == null || b == null || a.PrimaryCultureId == b.PrimaryCultureId) return;
            TryIncreaseFamiliarity(personIdA, b.PrimaryCultureId, 0.05f, HistoricalCulturalTransmissionSource.Marriage, out _);
            TryIncreaseFamiliarity(personIdB, a.PrimaryCultureId, 0.05f, HistoricalCulturalTransmissionSource.Marriage, out _);
        }

        public static bool TryIncreaseCommunityPresence(string communityId, string cultureId, float amount, out string error)
        {
            error = null;
            HistoricalCommunityCultureState community = FindCommunity(communityId);
            if (!IsActive || community == null) return Fail("Historical community culture was not found.", out error);
            if (DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(cultureId) == null) return Fail("Unknown culture " + cultureId + ".", out error);
            if (float.IsNaN(amount) || amount <= 0f) return Fail("Presence increase must be positive.", out error);
            HistoricalCulturalFamiliarity presence = community.CulturalPresence.FirstOrDefault(item => item.CultureId == cultureId);
            if (presence == null) { presence = NewFamiliarity(cultureId, 0f, HistoricalCulturalTransmissionSource.Community); community.CulturalPresence.Add(presence); }
            presence.Familiarity = Math.Min(1f, presence.Familiarity + amount);
            presence.LastSource = HistoricalCulturalTransmissionSource.Community;
            presence.LastChangedGameTick = Find.TickManager?.TicksGame ?? -1;
            return true;
        }

        public static bool IsCustomActive(string communityId, string customId) => FindCommunity(communityId)?.ActiveCustomIds?.Contains(customId) == true;
        public static HistoricalCulturalFamiliarity GetFamiliarity(string personId, string cultureId) => FindPerson(personId)?.FindFamiliarity(cultureId);
        public static bool TryRecordCultureExperience(string entryId, string titleKey, string descriptionKey, HistoricalEvidenceLevel evidence, string sourceNotes, out string error) =>
            HistoricalChronicleManager.TryAddLocalExperience(entryId, titleKey, descriptionKey, null, null, evidence, sourceNotes, out error);

        private static HistoricalCulturalFamiliarity NewFamiliarity(string id, float value, HistoricalCulturalTransmissionSource source) => new HistoricalCulturalFamiliarity
        { CultureId = id, Familiarity = Math.Max(0f, Math.Min(1f, value)), LastSource = source, LastChangedGameTick = Find.TickManager?.TicksGame ?? -1 };
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
