using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Lifespan;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Legacy.Simulation
{
    public static class HistoricalDescendantSimulator
    {
        private const string SimulationNotes = "Deterministic family-interlude simulation; probabilities are configurable gameplay parameters, not historical demographic measurements.";

        public static LegacySimulationResult Simulate(HistoricalLegacyArchive archive, HistoricalDate targetDate, LegacySimulationOptions options)
        {
            if (!TrySimulate(archive, targetDate, options, out LegacySimulationResult result, out string error))
                throw new InvalidOperationException(error);
            return result;
        }

        public static bool TrySimulate(HistoricalLegacyArchive archive, HistoricalDate targetDate, LegacySimulationOptions options,
            out LegacySimulationResult result, out string error)
        {
            result = null; error = null;
            if (!HistoricalLegacyArchiveValidator.TryValidate(archive, out error)) return false;
            options = options ?? new LegacySimulationOptions();
            HistoricalScenarioDef targetScenario = DefDatabase<HistoricalScenarioDef>.GetNamedSilentFail(options.TargetScenarioId);
            if (targetScenario == null) return Fail("Target scenario is missing or unknown.", out error);
            if (!HistoricalLegacyArchiveValidator.TryDate(archive.SourceEndAstronomicalYear, archive.SourceEndMonth, archive.SourceEndDay, out HistoricalDate sourceDate))
                return Fail("Archive source end date is invalid.", out error);
            long elapsedDays;
            try { elapsedDays = HistoricalLegacyTimeUtility.GetElapsedHistoricalDays(sourceDate, targetDate); }
            catch (Exception exception) { return Fail("Target date precedes the archive: " + exception.Message, out error); }
            if (elapsedDays / HistoricalCalendarConstants.DaysPerYear > options.MaximumYears)
                return Fail("Interlude exceeds the configured simulation year limit.", out error);

            LegacyPopulationPolicyDef policy = DefDatabase<LegacyPopulationPolicyDef>.AllDefsListForReading
                .Where(item => string.IsNullOrWhiteSpace(item.targetScenarioId) || item.targetScenarioId == targetScenario.defName)
                .OrderBy(item => item.targetScenarioId == targetScenario.defName ? 0 : 1).ThenBy(item => item.defName, StringComparer.Ordinal).FirstOrDefault();
            if (policy != null)
            {
                if (!policy.IsValid(out error)) return false;
                options.AnnualBirthProbability = policy.annualBirthProbability;
                options.AnnualAdultMortality = policy.annualAdultMortality;
                options.AnnualChildMortality = policy.annualChildMortality;
                options.AnnualMarriageProbability = policy.annualMarriageProbability;
                options.MaximumGeneratedPersons = Math.Min(options.MaximumGeneratedPersons, policy.maximumGeneratedPersons);
                options.MaximumActiveDescendants = Math.Min(options.MaximumActiveDescendants, policy.maximumActiveDescendants);
                options.MaximumYears = Math.Min(options.MaximumYears, policy.maximumSimulationYears);
            }
            if (!ValidProbability(options.AnnualBirthProbability) || !ValidProbability(options.AnnualAdultMortality)
                || !ValidProbability(options.AnnualChildMortality) || !ValidProbability(options.AnnualMarriageProbability)
                || options.MaximumGeneratedPersons < 1 || options.MaximumActiveDescendants < 1)
                return Fail("Simulation options contain invalid probabilities or caps.", out error);

            HistoricalLegacyArchive simulated;
            try { simulated = HistoricalLegacyStorage.Clone(archive); }
            catch (Exception exception) { return Fail("Could not prepare an isolated simulation copy: " + exception.Message, out error); }
            var random = new DeterministicRandom(StableSeed(archive.ArchiveId + "|" + targetScenario.defName + "|" + options.Seed));
            var resultLocal = new LegacySimulationResult
            {
                SourceArchiveId = archive.ArchiveId, TargetScenarioId = targetScenario.defName,
                SimulationSeed = options.Seed, TargetDate = new HistoricalDateReference(targetDate.AstronomicalYear, targetDate.Month, targetDate.Day, HistoricalDatePrecision.Day),
                SimulatedArchive = simulated
            };
            simulated.SourceEndAstronomicalYear = targetDate.AstronomicalYear;
            simulated.SourceEndMonth = targetDate.Month;
            simulated.SourceEndDay = targetDate.Day;

            EnsureTargetRegions(simulated, targetScenario, sourceDate, resultLocal);
            int generatedIndex = 0;
            HistoricalDate current = sourceDate;
            int maxSteps = Math.Min(options.MaximumYears, 10000);
            for (int yearStep = 0; yearStep < maxSteps && current.AddYears(1) <= targetDate; yearStep++)
            {
                current = current.AddYears(1);
                ProcessDeaths(simulated, current, random, policy, resultLocal);
                SimulateMarriages(simulated, current, random, policy, resultLocal);
                SimulateBirths(simulated, current, random, policy, ref generatedIndex, options, resultLocal);
                if (options.AllowMigration && policy != null && random.NextDouble() < policy.annualMigrationProbability)
                    SimulateMigration(simulated, current, targetScenario, random, resultLocal);
            }
            if (current < targetDate) ProcessDeaths(simulated, targetDate, random, policy, resultLocal);

            simulated.ImportedMarkers = new List<LegacyImportMarkerData>();
            simulated.ArchiveName = (archive.ArchiveName ?? "Legacy") + " (simulated)";
            BuildCandidates(simulated, targetScenario, targetDate, resultLocal);
            result = resultLocal;
            return true;
        }

        private static void EnsureTargetRegions(HistoricalLegacyArchive archive, HistoricalScenarioDef scenario, HistoricalDate sourceDate, LegacySimulationResult result)
        {
            HashSet<string> regionIds = new HashSet<string>((scenario.regions ?? new List<HistoricalRegionDef>()).Where(item => item != null).Select(item => item.defName), StringComparer.Ordinal);
            string fallback = scenario.defaultStartingRegion?.defName ?? scenario.regions?.FirstOrDefault(item => item != null)?.defName;
            if (fallback == null) return;
            foreach (IGrouping<string, LegacyPersonData> family in archive.Persons.Where(item => item.IsAliveAtExport && !regionIds.Contains(item.LastKnownRegionId)).GroupBy(item => item.FamilyId, StringComparer.Ordinal))
            {
                string from = family.First().LastKnownRegionId;
                foreach (LegacyPersonData person in family) person.LastKnownRegionId = fallback;
                HistoricalRegionDef region = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(fallback);
                string cultureId = region?.associatedCultures?.FirstOrDefault()?.defName;
                foreach (LegacyPersonData person in family) ApplyMigrationCulture(person, cultureId);
                var migration = new LegacyFamilyMigrationData { FamilyId = family.Key, FromRegionId = from, ToRegionId = fallback,
                    AstronomicalYear = sourceDate.AstronomicalYear, Reason = "Mapped to an available target-scenario region for candidate placement.", IsSimulated = true };
                archive.Migrations.Add(migration); AddChronicle(archive, result, MigrationChronicle(archive.ArchiveId, migration));
            }
        }

        private static void ProcessDeaths(HistoricalLegacyArchive archive, HistoricalDate date, DeterministicRandom random,
            LegacyPopulationPolicyDef policy, LegacySimulationResult result)
        {
            foreach (LegacyPersonData person in archive.Persons.Where(item => item.IsAliveAtExport).ToList())
            {
                if (HasDate(person.NaturalDeathAstronomicalYear, person.NaturalDeathMonth, person.NaturalDeathDay)
                    && DateFrom(person.NaturalDeathAstronomicalYear.Value, person.NaturalDeathMonth.Value, person.NaturalDeathDay.Value) <= date)
                {
                    MarkSimulatedDeath(archive, person, DateFrom(person.NaturalDeathAstronomicalYear.Value, person.NaturalDeathMonth.Value, person.NaturalDeathDay.Value), HistoricalDeathCause.NaturalLifespan, result);
                    continue;
                }
                int age = Age(person, date);
                float chance = age < 5 ? policy?.annualChildMortality ?? 0.035f : policy?.annualAdultMortality ?? 0.012f;
                if (random.NextDouble() < chance) MarkSimulatedDeath(archive, person, date, age < 5 ? HistoricalDeathCause.Disease : HistoricalDeathCause.Other, result);
            }
        }

        private static void MarkSimulatedDeath(HistoricalLegacyArchive archive, LegacyPersonData person, HistoricalDate date,
            HistoricalDeathCause cause, LegacySimulationResult result)
        {
            person.IsAliveAtExport = false; person.DeathAstronomicalYear = date.AstronomicalYear; person.DeathMonth = date.Month; person.DeathDay = date.Day; person.DeathCause = cause;
            foreach (LegacyKinshipData relation in archive.Kinships.Where(item => item.Type == HistoricalKinshipType.Spouse && !item.EndAstronomicalYear.HasValue
                && (item.PersonIdA == person.PersonId || item.PersonIdB == person.PersonId)))
            { relation.EndAstronomicalYear = date.AstronomicalYear; relation.EndMonth = date.Month; relation.EndDay = date.Day; }
            AddChronicle(archive, result, EventChronicle(archive.ArchiveId, "death", person.PersonId, date, "HR_LegacySim_Death_Title", "HR_LegacySim_Death_Description"));
        }

        private static void SimulateMarriages(HistoricalLegacyArchive archive, HistoricalDate date, DeterministicRandom random,
            LegacyPopulationPolicyDef policy, LegacySimulationResult result)
        {
            int minimumAge = policy?.minimumMarriageAge ?? 18;
            var eligible = archive.Persons.Where(item => item.IsAliveAtExport && Age(item, date) >= minimumAge
                && !HasActiveSpouse(archive.Kinships, item.PersonId, date)
                && (item.Gender == "Male" || item.Gender == "Female")).OrderBy(item => item.PersonId, StringComparer.Ordinal).ToList();
            Shuffle(eligible, random);
            var paired = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyPersonData person in eligible)
            {
                if (paired.Contains(person.PersonId) || random.NextDouble() >= (policy?.annualMarriageProbability ?? 0.12f)) continue;
                LegacyPersonData spouse = eligible.FirstOrDefault(other => !paired.Contains(other.PersonId) && other.PersonId != person.PersonId
                    && other.Gender != person.Gender && !CloselyRelated(archive.Kinships, person.PersonId, other.PersonId));
                if (spouse == null) continue;
                archive.Kinships.Add(new LegacyKinshipData { PersonIdA = person.PersonId, PersonIdB = spouse.PersonId, Type = HistoricalKinshipType.Spouse,
                    StartAstronomicalYear = date.AstronomicalYear, StartMonth = date.Month, StartDay = date.Day });
                paired.Add(person.PersonId); paired.Add(spouse.PersonId);
                result.GeneratedKinships.Add(archive.Kinships[archive.Kinships.Count - 1]);
                AddChronicle(archive, result, EventChronicle(archive.ArchiveId, "marriage", person.PersonId + "|" + spouse.PersonId, date,
                    "HR_LegacySim_Marriage_Title", "HR_LegacySim_Marriage_Description"));
            }
        }

        private static void SimulateBirths(HistoricalLegacyArchive archive, HistoricalDate date, DeterministicRandom random,
            LegacyPopulationPolicyDef policy, ref int generatedIndex, LegacySimulationOptions options, LegacySimulationResult result)
        {
            if (generatedIndex >= options.MaximumGeneratedPersons) return;
            int maxAge = policy?.maximumChildbearingAge ?? 40;
            int activeLimit = options.MaximumActiveDescendants;
            int activeGenerated = archive.Persons.Count(item => item.Origin == HistoricalPersonOrigin.LegacySimulated && item.IsAliveAtExport);
            if (activeGenerated >= activeLimit) return;
            var couples = archive.Kinships.Where(item => item.Type == HistoricalKinshipType.Spouse && !item.EndAstronomicalYear.HasValue
                && item.StartAstronomicalYear.HasValue && item.StartAstronomicalYear.Value < date.AstronomicalYear)
                .OrderBy(item => item.PersonIdA, StringComparer.Ordinal).ToList();
            foreach (LegacyKinshipData couple in couples)
            {
                if (generatedIndex >= options.MaximumGeneratedPersons || activeGenerated >= activeLimit) break;
                LegacyPersonData a = archive.Persons.FirstOrDefault(item => item.PersonId == couple.PersonIdA);
                LegacyPersonData b = archive.Persons.FirstOrDefault(item => item.PersonId == couple.PersonIdB);
                LegacyPersonData mother = a?.Gender == "Female" ? a : b?.Gender == "Female" ? b : null;
                LegacyPersonData father = a?.Gender == "Male" ? a : b?.Gender == "Male" ? b : null;
                if (mother == null || father == null || !mother.IsAliveAtExport || !father.IsAliveAtExport || Age(mother, date) > maxAge || Age(mother, date) < 18) continue;
                if (random.NextDouble() >= (policy?.annualBirthProbability ?? options.AnnualBirthProbability)) continue;
                int childIndex = generatedIndex++;
                string id = "HR_LegacyPerson_" + StableHash(archive.ArchiveId + "|" + result.SimulationSeed + "|" + childIndex + "|" + mother.PersonId + "|" + father.PersonId);
                if (archive.Persons.Any(item => item.PersonId == id)) continue;
                HistoricalDate naturalDeath = date.AddYears(GenerateLifespan(policy, random));
                string regionId = mother.LastKnownRegionId;
                HistoricalRegionDef region = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId);
                string localCultureId = region?.associatedCultures?.FirstOrDefault()?.defName;
                string primaryCulture = mother.PrimaryCultureId ?? localCultureId ?? father.PrimaryCultureId;
                string childFamilyId = mother.FamilyId;
                if (!string.Equals(mother.FamilyId, father.FamilyId, StringComparison.Ordinal))
                {
                    childFamilyId = "HR_LegacyFamily_" + StableHash(archive.ArchiveId + "|" + result.SimulationSeed
                        + "|" + mother.FamilyId + "|" + father.FamilyId + "|" + mother.PersonId + "|" + father.PersonId);
                    if (!archive.Families.Any(item => item.FamilyId == childFamilyId))
                    {
                        LegacyFamilyData maternalFamily = archive.Families.FirstOrDefault(item => item.FamilyId == mother.FamilyId);
                        archive.Families.Add(new LegacyFamilyData
                        {
                            FamilyId = childFamilyId,
                            FamilyName = (maternalFamily?.FamilyName ?? mother.DisplayName ?? "Family") + " branch",
                            FounderPersonId = id,
                            OriginRegionId = regionId,
                            OriginPolityId = mother.LastKnownAllegiancePolityId,
                            PrimaryCultureId = primaryCulture,
                            PreservedTraditionIds = new List<string>(maternalFamily?.PreservedTraditionIds ?? new List<string>()),
                            ParentFamilyId = mother.FamilyId
                        });
                    }
                }
                var child = new LegacyPersonData
                {
                    PersonId = id, FamilyId = childFamilyId, DisplayName = "Descendant of " + (mother.DisplayName ?? mother.PersonId),
                    Origin = HistoricalPersonOrigin.LegacySimulated, BirthAstronomicalYear = date.AstronomicalYear, BirthMonth = date.Month, BirthDay = date.Day,
                    IsAliveAtExport = true, BirthRegionId = regionId, LastKnownRegionId = regionId,
                    BirthCultureId = localCultureId ?? primaryCulture, PrimaryCultureId = primaryCulture,
                    LastKnownAllegiancePolityId = null, BirthAllegiancePolityId = null, Gender = random.NextDouble() < 0.5 ? "Female" : "Male",
                    AssignedLifespanYears = naturalDeath.AstronomicalYear - date.AstronomicalYear,
                    NaturalDeathAstronomicalYear = naturalDeath.AstronomicalYear, NaturalDeathMonth = naturalDeath.Month, NaturalDeathDay = naturalDeath.Day,
                    Familiarities = InheritedFamiliarities(mother, father, localCultureId)
                };
                archive.Persons.Add(child); result.GeneratedPersons.Add(child); activeGenerated++;
                var parentA = new LegacyKinshipData { PersonIdA = mother.PersonId, PersonIdB = id, Type = HistoricalKinshipType.BiologicalParent, StartAstronomicalYear = date.AstronomicalYear, StartMonth = date.Month, StartDay = date.Day };
                var parentB = new LegacyKinshipData { PersonIdA = father.PersonId, PersonIdB = id, Type = HistoricalKinshipType.BiologicalParent, StartAstronomicalYear = date.AstronomicalYear, StartMonth = date.Month, StartDay = date.Day };
                archive.Kinships.Add(parentA); archive.Kinships.Add(parentB); result.GeneratedKinships.Add(parentA); result.GeneratedKinships.Add(parentB);
                AddChronicle(archive, result, EventChronicle(archive.ArchiveId, "birth", id, date, "HR_LegacySim_Birth_Title", "HR_LegacySim_Birth_Description"));
            }
        }

        private static void SimulateMigration(HistoricalLegacyArchive archive, HistoricalDate date, HistoricalScenarioDef scenario,
            DeterministicRandom random, LegacySimulationResult result)
        {
            List<HistoricalRegionDef> regions = (scenario.regions ?? new List<HistoricalRegionDef>()).Where(item => item != null).OrderBy(item => item.defName, StringComparer.Ordinal).ToList();
            if (regions.Count < 2) return;
            var families = archive.Persons.Where(item => item.IsAliveAtExport).GroupBy(item => item.FamilyId).OrderBy(group => group.Key, StringComparer.Ordinal).ToList();
            if (families.Count == 0) return;
            var family = families[random.Next(families.Count)];
            List<HistoricalRegionDef> destinations = regions.Where(item => item.defName != family.First().LastKnownRegionId).ToList();
            if (destinations.Count == 0) return;
            HistoricalRegionDef destination = destinations[random.Next(destinations.Count)];
            string from = family.First().LastKnownRegionId;
            string localCulture = destination.associatedCultures?.FirstOrDefault()?.defName;
            foreach (LegacyPersonData person in family) { person.LastKnownRegionId = destination.defName; ApplyMigrationCulture(person, localCulture); }
            var migration = new LegacyFamilyMigrationData { FamilyId = family.Key, FromRegionId = from, ToRegionId = destination.defName,
                AstronomicalYear = date.AstronomicalYear, Reason = "Scenario-level probabilistic migration event.", IsSimulated = true };
            archive.Migrations.Add(migration); AddChronicle(archive, result, MigrationChronicle(archive.ArchiveId, migration));
        }

        private static void BuildCandidates(HistoricalLegacyArchive archive, HistoricalScenarioDef scenario, HistoricalDate date, LegacySimulationResult result)
        {
            HashSet<string> regions = new HashSet<string>((scenario.regions ?? new List<HistoricalRegionDef>()).Where(item => item != null).Select(item => item.defName), StringComparer.Ordinal);
            foreach (LegacyPersonData person in archive.Persons.Where(item => item.IsAliveAtExport).OrderBy(item => item.FamilyId, StringComparer.Ordinal).ThenBy(item => item.PersonId, StringComparer.Ordinal))
            {
                int age = Age(person, date);
                bool naturalLife = !HasDate(person.NaturalDeathAstronomicalYear, person.NaturalDeathMonth, person.NaturalDeathDay)
                    || DateFrom(person.NaturalDeathAstronomicalYear.Value, person.NaturalDeathMonth.Value, person.NaturalDeathDay.Value) > date;
                bool validCulture = person.PrimaryCultureId == null || DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(person.PrimaryCultureId) != null;
                bool eligible = age >= 18 && age <= 70 && naturalLife && regions.Contains(person.LastKnownRegionId) && validCulture;
                string allegiance = HistoricalPoliticsManager.Regions.FirstOrDefault(item => item.RegionId == person.LastKnownRegionId)?.EffectiveControllerId;
                result.Candidates.Add(new LegacyDescendantCandidate
                {
                    PersonId = person.PersonId, FamilyId = person.FamilyId, DisplayName = person.DisplayName, Gender = person.Gender, HistoricalAgeYears = age,
                    CurrentRegionId = person.LastKnownRegionId, PrimaryCultureId = person.PrimaryCultureId,
                    AllegiancePolityId = allegiance, IsEligibleForStartingPawn = eligible
                });
            }
        }

        private static List<LegacyCulturalFamiliarityData> InheritedFamiliarities(LegacyPersonData mother, LegacyPersonData father, string localCultureId)
        {
            var values = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (LegacyPersonData parent in new[] { mother, father })
                foreach (LegacyCulturalFamiliarityData familiarity in parent.Familiarities ?? new List<LegacyCulturalFamiliarityData>())
                    if (familiarity != null && familiarity.CultureId != null)
                        values[familiarity.CultureId] = Math.Max(values.TryGetValue(familiarity.CultureId, out float current) ? current : 0f, familiarity.Familiarity * 0.55f);
            if (!string.IsNullOrWhiteSpace(localCultureId)) values[localCultureId] = Math.Max(values.TryGetValue(localCultureId, out float local) ? local : 0f, 0.7f);
            return values.Select(item => new LegacyCulturalFamiliarityData { CultureId = item.Key, Familiarity = item.Value }).ToList();
        }

        private static void ApplyMigrationCulture(LegacyPersonData person, string localCultureId)
        {
            if (string.IsNullOrWhiteSpace(localCultureId)) return;
            person.PrimaryCultureId = localCultureId;
            person.Familiarities = person.Familiarities ?? new List<LegacyCulturalFamiliarityData>();
            LegacyCulturalFamiliarityData familiarity = person.Familiarities.FirstOrDefault(item => item.CultureId == localCultureId);
            if (familiarity == null) person.Familiarities.Add(new LegacyCulturalFamiliarityData { CultureId = localCultureId, Familiarity = 0.45f });
            else familiarity.Familiarity = Math.Min(1f, familiarity.Familiarity + 0.1f);
        }

        private static bool HasActiveSpouse(List<LegacyKinshipData> relations, string id, HistoricalDate date) => relations.Any(item => item.Type == HistoricalKinshipType.Spouse
            && (item.PersonIdA == id || item.PersonIdB == id) && (!item.StartAstronomicalYear.HasValue || item.StartAstronomicalYear.Value <= date.AstronomicalYear)
            && (!item.EndAstronomicalYear.HasValue || DateFrom(item.EndAstronomicalYear.Value, item.EndMonth ?? 1, item.EndDay ?? 1) >= date));

        private static bool CloselyRelated(List<LegacyKinshipData> relations, string a, string b)
        {
            HashSet<string> ancestorsA = Ancestors(relations, a); HashSet<string> ancestorsB = Ancestors(relations, b);
            if (ancestorsA.Contains(b) || ancestorsB.Contains(a)) return true;
            return ancestorsA.Overlaps(ancestorsB);
        }
        private static HashSet<string> Ancestors(List<LegacyKinshipData> relations, string id)
        {
            var ancestors = new HashSet<string>(StringComparer.Ordinal); var pending = new Stack<string>(); pending.Push(id);
            while (pending.Count > 0)
            {
                string child = pending.Pop();
                foreach (LegacyKinshipData relation in relations.Where(item => (item.Type == HistoricalKinshipType.BiologicalParent || item.Type == HistoricalKinshipType.AdoptiveParent) && item.PersonIdB == child))
                    if (ancestors.Add(relation.PersonIdA)) pending.Push(relation.PersonIdA);
            }
            ancestors.Remove(id); return ancestors;
        }
        private static int Age(LegacyPersonData person, HistoricalDate date) => HistoricalAgeUtility.GetHistoricalAgeYears(new HistoricalDate(person.BirthAstronomicalYear, person.BirthMonth, person.BirthDay), date);
        private static int GenerateLifespan(LegacyPopulationPolicyDef policy, DeterministicRandom random)
        {
            int min = policy?.minimumNaturalLifespanYears ?? 50, max = policy?.maximumNaturalLifespanYears ?? 95;
            double mean = policy?.meanNaturalLifespanYears ?? 72d, sd = policy?.naturalLifespanStandardDeviation ?? 9d;
            double u1 = Math.Max(1e-12, random.NextDouble()), u2 = random.NextDouble();
            double gaussian = Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
            return Math.Max(min, Math.Min(max, (int)Math.Round(mean + gaussian * sd)));
        }
        private static bool HasDate(int? year, int? month, int? day) => year.HasValue && month.HasValue && day.HasValue;
        private static HistoricalDate DateFrom(int year, int month, int day) => new HistoricalDate(year, month, day);
        private static bool ValidProbability(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        private static void Shuffle<T>(IList<T> items, DeterministicRandom random)
        { for (int i = items.Count - 1; i > 0; i--) { int j = random.Next(i + 1); T value = items[i]; items[i] = items[j]; items[j] = value; } }
        private static LegacyChronicleData EventChronicle(string archiveId, string kind, string key, HistoricalDate date, string title, string description) => new LegacyChronicleData
        {
            EntryId = "Sim:" + StableHash(archiveId + "|" + kind + "|" + key + "|" + date.AstronomicalYear + "|" + date.Month + "|" + date.Day),
            Origin = LegacyChronicleOrigin.SimulatedInterlude, EntryType = HistoricalChronicleEntryType.InferredLegacy,
            AstronomicalYear = date.AstronomicalYear, Month = date.Month, Day = date.Day, Precision = HistoricalDatePrecision.Day,
            TitleKey = title, DescriptionKey = description, EvidenceLevel = HistoricalEvidenceLevel.Speculative, SourceNotes = SimulationNotes,
            Category = HistoricalEventCategory.Other, Scope = HistoricalEventScope.LocalCommunity, VisibleToPlayer = true
        };
        private static LegacyChronicleData MigrationChronicle(string archiveId, LegacyFamilyMigrationData migration) => EventChronicle(archiveId, "migration", migration.FamilyId + "|" + migration.ToRegionId,
            new HistoricalDate(migration.AstronomicalYear, 1, 1), "HR_LegacySim_Migration_Title", "HR_LegacySim_Migration_Description");
        private static void AddChronicle(HistoricalLegacyArchive archive, LegacySimulationResult result, LegacyChronicleData entry)
        { result.GeneratedChronicle.Add(entry); archive.Chronicle.Add(entry); }
        private static string StableHash(string value)
        { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).Substring(0, 24); }
        private static uint StableSeed(string value)
        { using (SHA256 sha = SHA256.Create()) { byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value)); return BitConverter.ToUInt32(bytes, 0); } }
        private static bool Fail(string message, out string error) { error = message; return false; }

        private sealed class DeterministicRandom
        {
            private uint state;
            public DeterministicRandom(uint seed) { state = seed == 0 ? 0x9E3779B9u : seed; }
            private uint NextUInt() { uint x = state; x ^= x << 13; x ^= x >> 17; x ^= x << 5; state = x; return x; }
            public double NextDouble() => NextUInt() / 4294967296d;
            public int Next(int max) => max <= 1 ? 0 : (int)(NextUInt() % (uint)max);
        }
    }
}
