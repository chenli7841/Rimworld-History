using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Relics;
using HistoricalRimWorld.Lifespan;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Culture;
using Verse;

namespace HistoricalRimWorld.Legacy
{
    public static class HistoricalLegacyArchiveValidator
    {
        public const int MaximumPersons = 20000;
        public const int MaximumFamilies = 10000;
        public const int MaximumKinships = 50000;
        public const int MaximumRelics = 5000;
        public const int MaximumChronicleEntries = 100000;
        public const int MaximumRelicEvents = 50000;
        public const int MaximumMigrations = 20000;
        public const int MaximumImportMarkers = 10000;

        public static bool TryValidate(HistoricalLegacyArchive archive, out string error)
        {
            List<string> errors = GetErrors(archive).Take(12).ToList();
            error = errors.Count == 0 ? null : string.Join(" ", errors);
            return errors.Count == 0;
        }

        public static IEnumerable<string> GetErrors(HistoricalLegacyArchive archive)
        {
            if (archive == null) { yield return "Archive is empty."; yield break; }
            if (archive.SchemaVersion != HistoricalLegacySchema.CurrentVersion) yield return "Archive schema version is not current.";
            if (!Guid.TryParse(archive.ArchiveId, out _)) yield return "ArchiveId must be a GUID.";
            if (string.IsNullOrWhiteSpace(archive.SourceScenarioId) || DefDatabase<HistoricalScenarioDef>.GetNamedSilentFail(archive.SourceScenarioId) == null)
                yield return "SourceScenarioId is missing or unknown.";
            if (string.IsNullOrWhiteSpace(archive.SourceSaveIdentifier)) yield return "SourceSaveIdentifier is required.";
            if (archive.Persons == null || archive.Families == null || archive.Kinships == null || archive.Relics == null
                || archive.RelicEvents == null || archive.Chronicle == null || archive.Migrations == null || archive.ImportedMarkers == null)
            { yield return "Archive collections cannot be null."; yield break; }
            if (archive.Persons.Count > MaximumPersons || archive.Families.Count > MaximumFamilies || archive.Kinships.Count > MaximumKinships
                || archive.Relics.Count > MaximumRelics || archive.Chronicle.Count > MaximumChronicleEntries
                || archive.RelicEvents.Count > MaximumRelicEvents || archive.Migrations.Count > MaximumMigrations
                || archive.ImportedMarkers.Count > MaximumImportMarkers)
            { yield return "Archive exceeds a collection size limit."; yield break; }
            if (!TryDate(archive.SourceEndAstronomicalYear, archive.SourceEndMonth, archive.SourceEndDay, out HistoricalDate sourceEnd))
                yield return "Source end date is invalid.";

            var persons = archive.Persons.Where(item => item != null).ToList();
            var families = archive.Families.Where(item => item != null).ToList();
            var personIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyPersonData person in persons)
            {
                if (!ValidId(person.PersonId) || !personIds.Add(person.PersonId)) yield return "PersonId is blank, too long, or duplicated.";
                if (!ValidId(person.FamilyId)) yield return "Person has no valid FamilyId.";
                if (!TryDate(person.BirthAstronomicalYear, person.BirthMonth, person.BirthDay, out HistoricalDate birth)) yield return "Person birth date is invalid.";
                else if (TryDate(archive.SourceEndAstronomicalYear, archive.SourceEndMonth, archive.SourceEndDay, out HistoricalDate dateAtExport) && birth > dateAtExport)
                    yield return "Person birth date is after the archive end date.";
                if (person.DeathAstronomicalYear.HasValue != person.DeathMonth.HasValue || person.DeathAstronomicalYear.HasValue != person.DeathDay.HasValue)
                    yield return "Person death date fields are incomplete.";
                else if (person.DeathAstronomicalYear.HasValue && TryDate(person.DeathAstronomicalYear.Value, person.DeathMonth.Value, person.DeathDay.Value, out HistoricalDate death)
                    && death < birth) yield return "Person death date precedes birth.";
                else if (person.DeathAstronomicalYear.HasValue && TryDate(person.DeathAstronomicalYear.Value, person.DeathMonth.Value, person.DeathDay.Value, out death)
                    && TryDate(archive.SourceEndAstronomicalYear, archive.SourceEndMonth, archive.SourceEndDay, out HistoricalDate deathEnd) && death > deathEnd)
                    yield return "Person death date is after the archive end date.";
                if (person.IsAliveAtExport && person.DeathAstronomicalYear.HasValue) yield return "Living person has a death date.";
                if (!person.IsAliveAtExport && !person.DeathAstronomicalYear.HasValue) yield return "Deceased person has no death date.";
                if (!person.AssignedLifespanYears.HasValue || person.AssignedLifespanYears.Value < 1) yield return "Assigned lifespan is missing or invalid.";
                if (!Enum.IsDefined(typeof(HistoricalDeathCause), person.DeathCause)) yield return "DeathCause is invalid.";
                if (person.NaturalDeathAstronomicalYear.HasValue != person.NaturalDeathMonth.HasValue || person.NaturalDeathAstronomicalYear.HasValue != person.NaturalDeathDay.HasValue)
                    yield return "Natural death date fields are incomplete.";
                else if (person.NaturalDeathAstronomicalYear.HasValue && !TryDate(person.NaturalDeathAstronomicalYear.Value, person.NaturalDeathMonth.Value, person.NaturalDeathDay.Value, out _))
                    yield return "Natural death date is invalid.";
                else if (!person.NaturalDeathAstronomicalYear.HasValue) yield return "Natural death date is required to preserve the HR-007 lifespan.";
                else if (TryDate(person.NaturalDeathAstronomicalYear.Value, person.NaturalDeathMonth.Value, person.NaturalDeathDay.Value, out HistoricalDate naturalDeath) && naturalDeath < birth)
                    yield return "Natural death date precedes birth.";
                if (person.Familiarities == null || person.Familiarities.Any(item => item == null || !ValidId(item.CultureId) || float.IsNaN(item.Familiarity) || float.IsInfinity(item.Familiarity) || item.Familiarity < 0f || item.Familiarity > 1f))
                    yield return "Person cultural familiarity data is invalid.";
                if ((!string.IsNullOrWhiteSpace(person.BirthCultureId) && DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(person.BirthCultureId) == null)
                    || (!string.IsNullOrWhiteSpace(person.PrimaryCultureId) && DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(person.PrimaryCultureId) == null)
                    || (person.Familiarities != null && person.Familiarities.Any(item => item != null && DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(item.CultureId) == null)))
                    yield return "Person references an unknown historical culture.";
            }
            var familyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyFamilyData family in families)
            {
                if (!ValidId(family.FamilyId) || !familyIds.Add(family.FamilyId)) yield return "FamilyId is blank, too long, or duplicated.";
                if (string.IsNullOrWhiteSpace(family.FamilyName)) yield return "FamilyName is required.";
                if (!string.IsNullOrWhiteSpace(family.PrimaryCultureId) && DefDatabase<HistoricalCultureDef>.GetNamedSilentFail(family.PrimaryCultureId) == null)
                    yield return "Family references an unknown historical culture.";
                if (family.PreservedTraditionIds == null || family.PreservedTraditionIds.Any(string.IsNullOrWhiteSpace)
                    || family.PreservedTraditionIds.Distinct(StringComparer.Ordinal).Count() != family.PreservedTraditionIds.Count)
                    yield return "Family tradition list is invalid.";
            }
            foreach (LegacyPersonData person in persons)
                if (ValidId(person.FamilyId) && !familyIds.Contains(person.FamilyId)) yield return "Person references a missing family.";
            foreach (LegacyFamilyData family in families)
            {
                if (!string.IsNullOrWhiteSpace(family.FounderPersonId) && !personIds.Contains(family.FounderPersonId)) yield return "Family founder is missing.";
                if (!string.IsNullOrWhiteSpace(family.ParentFamilyId) && !familyIds.Contains(family.ParentFamilyId)) yield return "Parent family reference is missing.";
            }
            if (HasFamilyCycle(familyIds, families)) yield return "Family branch references contain a cycle.";

            var relationKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyKinshipData relation in archive.Kinships.Where(item => item != null))
            {
                if (!personIds.Contains(relation.PersonIdA) || !personIds.Contains(relation.PersonIdB) || relation.PersonIdA == relation.PersonIdB)
                    yield return "Kinship references a missing or identical person.";
                if (!Enum.IsDefined(typeof(HistoricalKinshipType), relation.Type)) yield return "Kinship type is invalid.";
                string a = relation.PersonIdA, b = relation.PersonIdB;
                if (relation.Type == HistoricalKinshipType.Spouse && string.CompareOrdinal(a, b) > 0) { string swap = a; a = b; b = swap; }
                if (!relationKeys.Add(relation.Type + "|" + a + "|" + b + "|" + relation.StartAstronomicalYear + "|" + relation.StartMonth + "|" + relation.StartDay))
                    yield return "Duplicate kinship relationship.";
                if (!relation.StartAstronomicalYear.HasValue || !TryDate(relation.StartAstronomicalYear.Value, relation.StartMonth ?? 1, relation.StartDay ?? 1, out HistoricalDate start))
                    yield return "Kinship start date is invalid.";
                if (relation.EndAstronomicalYear.HasValue != relation.EndMonth.HasValue || relation.EndAstronomicalYear.HasValue != relation.EndDay.HasValue)
                    yield return "Kinship end date fields are incomplete.";
                else if (relation.EndAstronomicalYear.HasValue && TryDate(relation.EndAstronomicalYear.Value, relation.EndMonth.Value, relation.EndDay.Value, out HistoricalDate end)
                    && relation.StartAstronomicalYear.HasValue && TryDate(relation.StartAstronomicalYear.Value, relation.StartMonth ?? 1, relation.StartDay ?? 1, out start) && end < start)
                    yield return "Kinship ends before it starts.";
            }
            if (HasParentCycle(personIds, archive.Kinships)) yield return "Biological/adoptive parent relations contain a cycle.";

            var relicIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyRelicData relic in archive.Relics.Where(item => item != null))
            {
                if (!ValidId(relic.RelicId) || !relicIds.Add(relic.RelicId)) yield return "RelicId is blank, too long, or duplicated.";
                if (DefDatabase<HistoricalRelicDef>.GetNamedSilentFail(relic.RelicDefName) == null) yield return "Relic references an unknown RelicDef.";
                if (relic.IsLost && relic.IsDestroyed) { /* destroyed records remain historical and are valid */ }
                if (relic.CreatorPersonId != null && !personIds.Contains(relic.CreatorPersonId)) yield return "Relic creator is missing.";
                if (relic.CurrentOwnerPersonId != null && !personIds.Contains(relic.CurrentOwnerPersonId)) yield return "Relic owner is missing.";
            }
            var relicEventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyRelicEventData item in archive.RelicEvents.Where(evt => evt != null))
            {
                if (!ValidId(item.EventId) || !relicEventIds.Add(item.EventId)) yield return "Relic event ID is blank or duplicated.";
                if (!relicIds.Contains(item.RelicId)) yield return "Relic event references an unknown relic.";
                if (!Enum.TryParse(item.EventType, out HistoricalRelicEventType _)) yield return "Relic event type is invalid.";
                if (TryDate(archive.SourceEndAstronomicalYear, archive.SourceEndMonth, archive.SourceEndDay, out HistoricalDate relicEnd)
                    && item.AstronomicalYear > relicEnd.AstronomicalYear) yield return "Relic event occurs after the archive end date.";
            }
            var chronicleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyChronicleData item in archive.Chronicle.Where(entry => entry != null))
            {
                if (!ValidId(item.EntryId) || !chronicleIds.Add(item.EntryId)) yield return "Chronicle EntryId is blank, too long, or duplicated.";
                bool invalidDate = item.Precision == HistoricalDatePrecision.Year
                    ? item.Month.HasValue || item.Day.HasValue
                    : item.Precision == HistoricalDatePrecision.Month
                        ? !item.Month.HasValue || item.Day.HasValue || item.Month.Value < 1 || item.Month.Value > HistoricalCalendarConstants.MonthsPerYear
                        : item.Precision == HistoricalDatePrecision.Day
                            ? !item.Month.HasValue || !item.Day.HasValue || !TryDate(item.AstronomicalYear, item.Month.Value, item.Day.Value, out _)
                            : true;
                if (invalidDate)
                    yield return "Chronicle date is invalid.";
                else if (TryDate(item.AstronomicalYear, item.Month ?? 1, item.Day ?? 1, out HistoricalDate chronicleDate)
                    && TryDate(archive.SourceEndAstronomicalYear, archive.SourceEndMonth, archive.SourceEndDay, out HistoricalDate chronicleEnd)
                    && chronicleDate > chronicleEnd) yield return "Chronicle entry is after the archive end date.";
            }
            foreach (LegacyFamilyMigrationData migration in archive.Migrations.Where(item => item != null))
                if (string.IsNullOrWhiteSpace(migration.FamilyId) || !familyIds.Contains(migration.FamilyId)
                    || (TryDate(archive.SourceEndAstronomicalYear, archive.SourceEndMonth, archive.SourceEndDay, out HistoricalDate migrationEnd)
                        && migration.AstronomicalYear > migrationEnd.AstronomicalYear))
                    yield return "Family migration has an invalid family or date.";
        }

        private static bool HasParentCycle(HashSet<string> personIds, List<LegacyKinshipData> relations)
        {
            var children = personIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (LegacyKinshipData relation in relations.Where(item => item != null
                && (item.Type == HistoricalKinshipType.BiologicalParent || item.Type == HistoricalKinshipType.AdoptiveParent)
                && children.ContainsKey(item.PersonIdA) && children.ContainsKey(item.PersonIdB))) children[relation.PersonIdB].Add(relation.PersonIdA);
            var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
            Func<string, bool> visit = null;
            visit = id => { if (visiting.Contains(id)) return true; if (!visited.Add(id)) return false; visiting.Add(id); foreach (string parent in children[id]) if (visit(parent)) return true; visiting.Remove(id); return false; };
            return children.Keys.Any(visit);
        }
        private static bool HasFamilyCycle(HashSet<string> familyIds, List<LegacyFamilyData> families)
        {
            var parents = families.ToDictionary(item => item.FamilyId, item => item.ParentFamilyId, StringComparer.Ordinal);
            foreach (string id in familyIds)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal); string current = id;
                while (!string.IsNullOrWhiteSpace(current) && parents.TryGetValue(current, out string parent))
                { if (!seen.Add(current)) return true; current = parent; }
            }
            return false;
        }
        internal static bool TryDate(int year, int month, int day, out HistoricalDate date)
        { try { date = new HistoricalDate(year, month, day); return true; } catch { date = default(HistoricalDate); return false; } }
        internal static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160;
    }
}
