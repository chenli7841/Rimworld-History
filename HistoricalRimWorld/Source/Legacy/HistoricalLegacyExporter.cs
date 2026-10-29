using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Lifespan;
using HistoricalRimWorld.Relics;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Legacy
{
    public static class HistoricalLegacyExporter
    {
        public static bool TryExport(string archiveName, out string outputPath, out string error) => TryExportCore(archiveName, null, out outputPath, out error);
        public static bool TryExportFamily(string archiveName, string familyId, out string outputPath, out string error)
        {
            if (HistoricalGenealogyManager.FindFamily(familyId) == null) { outputPath = null; error = "Selected family was not found."; return false; }
            return TryExportCore(archiveName, familyId, out outputPath, out error);
        }

        private static bool TryExportCore(string archiveName, string familyId, out string outputPath, out string error)
        {
            outputPath = null; error = null;
            HistoricalScenarioDef scenario = HistoricalScenarioManager.CurrentScenario;
            if (!HistoricalScenarioManager.IsScenarioActive || scenario == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate endDate))
                return Fail("Initialize a historical scenario before exporting.", out error);
            HistoricalLegacyGameComponent legacy = Current.Game?.GetComponent<HistoricalLegacyGameComponent>();
            if (legacy == null) return Fail("Legacy save identity component is unavailable.", out error);

            List<HistoricalPersonRecord> allPeople = HistoricalGenealogyManager.Persons.Where(item => item != null).ToList();
            List<HistoricalFamilyRecord> allFamilies = HistoricalGenealogyManager.Families.Where(item => item != null).ToList();
            HashSet<string> selectedPeople = SelectPeople(familyId, allPeople, HistoricalGenealogyManager.Relations);
            List<HistoricalPersonRecord> people = allPeople.Where(item => selectedPeople.Contains(item.PersonId)).ToList();
            HashSet<string> familyIds = new HashSet<string>(people.Select(item => item.FamilyId).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
            if (familyId != null) familyIds.Add(familyId);
            List<HistoricalFamilyRecord> families = allFamilies.Where(item => familyIds.Contains(item.FamilyId)).ToList();
            var archive = new HistoricalLegacyArchive
            {
                SchemaVersion = HistoricalLegacySchema.CurrentVersion,
                ArchiveId = Guid.NewGuid().ToString("D"),
                ArchiveName = archiveName,
                SourceScenarioId = scenario.defName,
                SourceEndAstronomicalYear = endDate.AstronomicalYear,
                SourceEndMonth = endDate.Month,
                SourceEndDay = endDate.Day,
                SourceSaveIdentifier = legacy.SourceSaveIdentifier
            };
            var states = HistoricalCultureManager.Persons.ToDictionary(item => item.PersonId, StringComparer.Ordinal);
            foreach (HistoricalPersonRecord person in people)
            {
                HistoricalNaturalLifespanRecord lifespan = HistoricalLifespanManager.Find(person.PersonId);
                states.TryGetValue(person.PersonId, out HistoricalPersonCultureState culture);
                Pawn pawn = HistoricalGenealogyManager.TryGetPawn(person.PersonId, out Pawn linked) ? linked : null;
                archive.Persons.Add(new LegacyPersonData
                {
                    PersonId = person.PersonId, FamilyId = person.FamilyId, DisplayName = person.DisplayName, Origin = person.Origin,
                    BirthAstronomicalYear = person.BirthDate.AstronomicalYear, BirthMonth = person.BirthDate.Month, BirthDay = person.BirthDate.Day,
                    IsAliveAtExport = person.IsAlive, DeathAstronomicalYear = person.DeathDate?.AstronomicalYear,
                    DeathMonth = person.DeathDate?.Month, DeathDay = person.DeathDate?.Day, BirthRegionId = person.BirthRegionId,
                    LastKnownRegionId = person.CurrentRegionId, BirthCultureId = culture?.BirthCultureId, PrimaryCultureId = culture?.PrimaryCultureId,
                    LastKnownAllegiancePolityId = person.CurrentAllegiancePolityId, BirthAllegiancePolityId = person.BirthPolityId,
                    DeathCause = person.LastDeathCause,
                    AssignedLifespanYears = lifespan?.AssignedLifespanYears,
                    NaturalDeathAstronomicalYear = lifespan?.NaturalDeathDate.AstronomicalYear,
                    NaturalDeathMonth = lifespan?.NaturalDeathDate.Month, NaturalDeathDay = lifespan?.NaturalDeathDate.Day,
                    Gender = pawn?.gender.ToString(),
                    Familiarities = culture?.Familiarities?.Where(item => item != null).Select(item => new LegacyCulturalFamiliarityData { CultureId = item.CultureId, Familiarity = item.Familiarity }).ToList() ?? new List<LegacyCulturalFamiliarityData>()
                });
            }
            var familyStates = HistoricalCultureManager.Families.ToDictionary(item => item.FamilyId, StringComparer.Ordinal);
            foreach (HistoricalFamilyRecord family in families)
            {
                familyStates.TryGetValue(family.FamilyId, out HistoricalFamilyCultureState culture);
                archive.Families.Add(new LegacyFamilyData
                {
                    FamilyId = family.FamilyId, FamilyName = family.FamilyName, FounderPersonId = family.FounderPersonId,
                    OriginRegionId = family.OriginRegionId, OriginPolityId = family.OriginPolityId,
                    PrimaryCultureId = culture?.PrimaryCultureId,
                    PreservedTraditionIds = culture?.PreservedTraditionIds?.ToList() ?? new List<string>(), ParentFamilyId = family.ParentFamilyId
                });
            }
            foreach (HistoricalKinshipRelation relation in HistoricalGenealogyManager.Relations.Where(item => item != null
                && selectedPeople.Contains(item.PersonIdA) && selectedPeople.Contains(item.PersonIdB)))
                archive.Kinships.Add(new LegacyKinshipData
                {
                    PersonIdA = relation.PersonIdA, PersonIdB = relation.PersonIdB, Type = relation.Type,
                    StartAstronomicalYear = relation.StartDate.AstronomicalYear, StartMonth = relation.StartDate.Month, StartDay = relation.StartDate.Day,
                    EndAstronomicalYear = relation.EndDate?.AstronomicalYear, EndMonth = relation.EndDate?.Month, EndDay = relation.EndDate?.Day
                });

            List<HistoricalRelicRecord> relicRecords = HistoricalRelicManager.Records.Where(item => familyId == null
                || familyIds.Contains(item.CurrentFamilyId) || selectedPeople.Contains(item.CreatorPersonId) || selectedPeople.Contains(item.CurrentOwnerPersonId)).ToList();
            var relicIds = new HashSet<string>(relicRecords.Select(item => item.RelicId), StringComparer.Ordinal);
            foreach (HistoricalRelicRecord relic in relicRecords)
                archive.Relics.Add(new LegacyRelicData
                {
                    RelicId = relic.RelicId, RelicDefName = relic.RelicDefName, DisplayName = relic.DisplayName,
                    OriginCultureId = relic.OriginCultureId, OriginRegionId = relic.OriginRegionId, CreatorPersonId = relic.CreatorPersonId,
                    OriginalOwnerPersonId = relic.OriginalOwnerPersonId, CurrentOwnerPersonId = relic.CurrentOwnerPersonId,
                    CurrentFamilyId = relic.CurrentFamilyId, CreationAstronomicalYear = relic.CreationAstronomicalYear,
                    IsLost = relic.IsLost, IsDestroyed = relic.IsDestroyed
                });
            foreach (HistoricalRelicEventRecord evt in HistoricalRelicManager.Events.Where(item => item != null && relicIds.Contains(item.RelicId)
                && item.AstronomicalYear <= endDate.AstronomicalYear))
                archive.RelicEvents.Add(new LegacyRelicEventData
                { EventId = evt.EventId, RelicId = evt.RelicId, EventType = evt.EventType.ToString(), FromPersonId = evt.FromPersonId, ToPersonId = evt.ToPersonId, RegionId = evt.RegionId, AstronomicalYear = evt.AstronomicalYear, SourceNotes = evt.SourceNotes });

            foreach (HistoricalChronicleEntry entry in HistoricalChronicleManager.Entries.Where(item => item != null && item.AbsoluteDay <= endDate.AbsoluteDay))
                archive.Chronicle.Add(new LegacyChronicleData
                {
                    EntryId = entry.EntryId, Origin = entry.EntryType == HistoricalChronicleEntryType.AttestedHistory ? LegacyChronicleOrigin.AttestedWorldHistory
                        : entry.EntryType == HistoricalChronicleEntryType.InferredLegacy ? LegacyChronicleOrigin.SimulatedInterlude : LegacyChronicleOrigin.RecordedGameplay,
                    EntryType = entry.EntryType, AstronomicalYear = entry.AstronomicalYear, Month = entry.Month, Day = entry.Day,
                    Precision = entry.Precision, TitleKey = entry.TitleKey, DescriptionKey = entry.DescriptionKey, RegionId = entry.RegionId,
                    PolityId = entry.PolityId, EvidenceLevel = entry.EvidenceLevel, SourceNotes = entry.SourceNotes,
                    Category = entry.Category, Scope = entry.Scope, VisibleToPlayer = entry.VisibleToPlayer
                });
            foreach (HistoricalLegacyMigrationRecord migration in legacy.Migrations.Where(item => item != null
                && familyIds.Contains(item.FamilyId) && item.AstronomicalYear <= endDate.AstronomicalYear))
                archive.Migrations.Add(new LegacyFamilyMigrationData { FamilyId = migration.FamilyId, FromRegionId = migration.FromRegionId,
                    ToRegionId = migration.ToRegionId, AstronomicalYear = migration.AstronomicalYear, Reason = migration.Reason, IsSimulated = migration.IsSimulated });
            return HistoricalLegacyStorage.TryWriteAtomic(archiveName, archive, out outputPath, out error);
        }

        private static HashSet<string> SelectPeople(string familyId, List<HistoricalPersonRecord> people, IEnumerable<HistoricalKinshipRelation> relations)
        {
            if (familyId == null) return new HashSet<string>(people.Select(item => item.PersonId), StringComparer.Ordinal);
            var selected = new HashSet<string>(people.Where(item => item.FamilyId == familyId).Select(item => item.PersonId), StringComparer.Ordinal);
            var relationList = relations.Where(item => item != null).ToList();
            bool changed;
            do
            {
                changed = false;
                foreach (HistoricalKinshipRelation relation in relationList)
                {
                    if (!selected.Contains(relation.PersonIdA) && !selected.Contains(relation.PersonIdB)) continue;
                    changed |= selected.Add(relation.PersonIdA); changed |= selected.Add(relation.PersonIdB);
                }
                HashSet<string> linkedFamilies = new HashSet<string>(people.Where(item => selected.Contains(item.PersonId)).Select(item => item.FamilyId), StringComparer.Ordinal);
                foreach (HistoricalPersonRecord person in people.Where(item => linkedFamilies.Contains(item.FamilyId))) changed |= selected.Add(person.PersonId);
            } while (changed);
            return selected;
        }
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
