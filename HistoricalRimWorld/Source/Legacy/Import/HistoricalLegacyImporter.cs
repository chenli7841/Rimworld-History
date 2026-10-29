using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Legacy.Simulation;
using HistoricalRimWorld.Relics;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Legacy.Import
{
    /// <summary>Validates and commits a user-confirmed archive import to the active historical scenario.</summary>
    public static class HistoricalLegacyImporter
    {
        public const int MaximumStartingPawns = 8;

        public static bool TryCreatePlan(HistoricalLegacyArchive archive, LegacySimulationResult simulation,
            string familyId, IEnumerable<string> personIds, IEnumerable<string> relicIds,
            out HistoricalLegacyImportPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (!HistoricalLegacyArchiveValidator.TryValidate(archive, out error)) return false;
            if (simulation?.SimulatedArchive == null || simulation.SourceArchiveId != archive.ArchiveId)
                return Fail("A matching deterministic simulation preview is required.", out error);
            if (!HistoricalScenarioManager.IsScenarioActive
                || !string.Equals(HistoricalScenarioManager.ActiveScenarioDefName, simulation.TargetScenarioId, StringComparison.Ordinal))
                return Fail("The preview target does not match the active historical scenario.", out error);
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate)
                || currentDate.AstronomicalYear != simulation.TargetDate.AstronomicalYear
                || currentDate.Month != simulation.TargetDate.Month || currentDate.Day != simulation.TargetDate.Day)
                return Fail("The preview date is stale. Run the simulation preview again for the current historical date.", out error);
            if (Find.CurrentMap == null || Find.CurrentMap.IsPlayerHome == false)
                return Fail("The active map must be a player home map before importing starting Pawns.", out error);
            HistoricalLegacyGameComponent legacyState = Current.Game?.GetComponent<HistoricalLegacyGameComponent>();
            if (legacyState == null) return Fail("The save has no Historical Legacy game component.", out error);
            if (legacyState.HasImport(archive.ArchiveId, simulation.TargetScenarioId))
                return Fail("This archive has already been imported into this scenario.", out error);
            if (!HistoricalLegacyArchiveValidator.TryValidate(simulation.SimulatedArchive, out error)) return false;
            HistoricalGenealogyGameComponent genealogy = Current.Game.GetComponent<HistoricalGenealogyGameComponent>();
            if (genealogy == null || !genealogy.Initialized)
                return Fail("Target scenario genealogy is not initialized.", out error);
            if (archive.Persons.Any(item => genealogy.FindPerson(item.PersonId) != null)
                || archive.Families.Any(item => genealogy.FindFamily(item.FamilyId) != null))
                return Fail("One or more PersonId or FamilyId values already exist in this save.", out error);
            HistoricalCultureGameComponent cultureState = Current.Game.GetComponent<HistoricalCultureGameComponent>();
            if (cultureState != null && (archive.Persons.Any(item => cultureState.FindPerson(item.PersonId) != null)
                || archive.Families.Any(item => cultureState.FindFamily(item.FamilyId) != null)))
                return Fail("Target culture state already contains one of the imported person or family IDs.", out error);
            HistoricalChronicleGameComponent chronicleState = Current.Game.GetComponent<HistoricalChronicleGameComponent>();
            if (chronicleState != null && archive.Chronicle.Any(item => chronicleState.Entries.Any(existing =>
                existing.EntryId == "Legacy:" + archive.ArchiveId + ":" + item.EntryId)))
                return Fail("Target chronicle already contains entries from this archive.", out error);

            string[] people = (personIds ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            string[] relics = (relicIds ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            if (people.Length == 0 || people.Length > MaximumStartingPawns)
                return Fail("Select between 1 and " + MaximumStartingPawns + " eligible descendants.", out error);
            if (string.IsNullOrWhiteSpace(familyId)) return Fail("Select a family line first.", out error);
            Dictionary<string, LegacyDescendantCandidate> candidates = simulation.Candidates
                .Where(item => item != null).GroupBy(item => item.PersonId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (string personId in people)
            {
                if (!candidates.TryGetValue(personId, out LegacyDescendantCandidate candidate)
                    || !candidate.IsEligibleForStartingPawn || candidate.FamilyId != familyId)
                    return Fail("A selected person is no longer eligible in the chosen family.", out error);
            }
            HashSet<string> selectedPeople = new HashSet<string>(people, StringComparer.Ordinal);
            Dictionary<string, LegacyRelicData> archiveRelics = archive.Relics.Where(item => item != null)
                .GroupBy(item => item.RelicId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (string relicId in relics)
            {
                if (!archiveRelics.TryGetValue(relicId, out LegacyRelicData relic) || relic.IsLost || relic.IsDestroyed
                    || (!IsFamilyInLineage(familyId, relic.CurrentFamilyId, archive.Families) && !selectedPeople.Contains(relic.CurrentOwnerPersonId)))
                    return Fail("A selected relic is unavailable to the selected family or descendants.", out error);
                if (Current.Game.GetComponent<HistoricalRelicGameComponent>()?.Find(relicId) != null)
                    return Fail("RelicId already exists in this save: " + relicId, out error);
                if (DefDatabase<HistoricalRelicDef>.GetNamedSilentFail(relic.RelicDefName)?.baseThingDef == null)
                    return Fail("Relic definition is unavailable: " + relic.RelicDefName, out error);
            }
            plan = new HistoricalLegacyImportPlan(archive, simulation, familyId, people, relics);
            return true;
        }

        public static bool TryCommit(HistoricalLegacyImportPlan plan, out string error)
        {
            error = null;
            if (plan == null || !TryCreatePlan(plan.Archive, plan.Simulation, plan.FamilyId, plan.PersonIds, plan.RelicIds,
                out _, out error)) return false;
            HistoricalLegacyArchive archive;
            try { archive = HistoricalLegacyStorage.Clone(plan.Simulation.SimulatedArchive); }
            catch (Exception exception) { return Fail("Cannot prepare isolated import data: " + exception.Message, out error); }
            string targetScenarioId = plan.Simulation.TargetScenarioId;
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate targetDate)) return Fail("Current historical date is unavailable.", out error);
            HashSet<string> selected = new HashSet<string>(plan.PersonIds, StringComparer.Ordinal);
            Dictionary<string, LegacyDescendantCandidate> candidates = plan.Simulation.Candidates.ToDictionary(item => item.PersonId, StringComparer.Ordinal);
            foreach (LegacyPersonData person in archive.Persons.Where(item => selected.Contains(item.PersonId)))
            {
                LegacyDescendantCandidate candidate = candidates[person.PersonId];
                // Preserve original/birth allegiance. Resolve current allegiance from the target save's politics.
                person.LastKnownAllegiancePolityId = candidate.AllegiancePolityId;
            }

            Map map = Find.CurrentMap;
            Dictionary<string, Pawn> pawns = new Dictionary<string, Pawn>(StringComparer.Ordinal);
            List<Pawn> spawned = new List<Pawn>();
            List<Thing> relicThings = new List<Thing>();
            List<HistoricalRelicRecord> relicRecords = new List<HistoricalRelicRecord>();
            List<HistoricalRelicEventRecord> relicEvents = new List<HistoricalRelicEventRecord>();
            List<HistoricalChronicleEntry> chronicleEntries = new List<HistoricalChronicleEntry>();
            string importId = MakeImportId(plan.Archive.ArchiveId, targetScenarioId, plan.Simulation.SimulationSeed);
            bool importMarkerRecorded = false;
            try
            {
                HashSet<IntVec3> reservedCells = new HashSet<IntVec3>();
                foreach (string id in plan.PersonIds)
                {
                    LegacyDescendantCandidate candidate = candidates[id];
                    if (!HistoricalLegacyPawnImporter.TryCreatePawn(candidate, out Pawn pawn, out error)) throw new InvalidOperationException(error);
                    if (!CellFinder.TryFindRandomSpawnCellForPawnNear(map.Center, map, out IntVec3 cell, 12,
                        position => !reservedCells.Contains(position)))
                    { pawn.Destroy(DestroyMode.Vanish); throw new InvalidOperationException("No safe spawn cell was found for " + candidate.DisplayName + "."); }
                    reservedCells.Add(cell);
                    GenSpawn.Spawn(pawn, cell, map, WipeMode.VanishOrMoveAside);
                    pawns.Add(id, pawn);
                    spawned.Add(pawn);
                }

                foreach (string relicId in plan.RelicIds)
                {
                    LegacyRelicData data = archive.Relics.First(item => item.RelicId == relicId);
                    string ownerId = selected.Contains(data.CurrentOwnerPersonId) ? data.CurrentOwnerPersonId : plan.PersonIds[0];
                    LegacyPersonData owner = archive.Persons.First(item => item.PersonId == ownerId);
                    if (!HistoricalLegacyRelicImporter.TryCreateThing(data, ownerId, owner.FamilyId,
                        out Thing thing, out HistoricalRelicRecord record, out error)) throw new InvalidOperationException(error);
                    if (pawns[ownerId].inventory == null || !pawns[ownerId].inventory.innerContainer.TryAdd(thing))
                    { if (thing != null && !thing.Destroyed) thing.Destroy(DestroyMode.Vanish); throw new InvalidOperationException("A selected heir could not carry relic " + relicId + "."); }
                    relicThings.Add(thing);
                    relicRecords.Add(record);
                    foreach (LegacyRelicEventData sourceEvent in archive.RelicEvents.Where(item => item.RelicId == relicId))
                    {
                        if (!Enum.TryParse(sourceEvent.EventType, true, out HistoricalRelicEventType eventType)) continue;
                        relicEvents.Add(new HistoricalRelicEventRecord { EventId = "Legacy:" + archive.ArchiveId + ":" + sourceEvent.EventId,
                            RelicId = relicId, EventType = eventType, FromPersonId = sourceEvent.FromPersonId, ToPersonId = sourceEvent.ToPersonId,
                            RegionId = sourceEvent.RegionId, AstronomicalYear = sourceEvent.AstronomicalYear, GameTick = -1,
                            SourceNotes = PrefixSource(archive, "Relic history. " + sourceEvent.SourceNotes) });
                        record.EventIds.Add("Legacy:" + archive.ArchiveId + ":" + sourceEvent.EventId);
                    }
                    string inheritedId = "Legacy:" + archive.ArchiveId + ":" + relicId + ":import-inherited";
                    relicEvents.Add(new HistoricalRelicEventRecord { EventId = inheritedId, RelicId = relicId,
                        EventType = HistoricalRelicEventType.Inherited, FromPersonId = data.CurrentOwnerPersonId,
                        ToPersonId = ownerId, RegionId = owner.LastKnownRegionId, AstronomicalYear = targetDate.AstronomicalYear,
                        GameTick = Find.TickManager?.TicksGame ?? -1, SourceNotes = PrefixSource(archive, "Selected heir received this relic on confirmed import.") });
                    record.EventIds.Add(inheritedId);
                }

                AddPawnRelations(archive, selected, pawns, targetDate);
                HistoricalGenealogyGameComponent genealogy = Current.Game.GetComponent<HistoricalGenealogyGameComponent>();
                if (genealogy == null || !genealogy.TryImportLegacyRecords(archive, pawns, out error)) throw new InvalidOperationException(error ?? "Genealogy component is unavailable.");
                AddCultureData(archive);
                HistoricalRelicGameComponent relicComponent = Current.Game.GetComponent<HistoricalRelicGameComponent>();
                foreach (HistoricalRelicRecord record in relicRecords) relicComponent.Add(record);
                foreach (HistoricalRelicEventRecord record in relicEvents) relicComponent.Add(record);
                HistoricalChronicleGameComponent chronicle = Current.Game.GetComponent<HistoricalChronicleGameComponent>();
                foreach (LegacyChronicleData item in archive.Chronicle)
                {
                    var entry = new HistoricalChronicleEntry();
                    HistoricalChronicleEntryType type = item.Origin == LegacyChronicleOrigin.AttestedWorldHistory
                        ? HistoricalChronicleEntryType.AttestedHistory
                        : item.Origin == LegacyChronicleOrigin.SimulatedInterlude
                            ? HistoricalChronicleEntryType.InferredLegacy : HistoricalChronicleEntryType.LocalExperience;
                    entry.Initialize("Legacy:" + archive.ArchiveId + ":" + item.EntryId, item.EntryId, type,
                        new HistoricalDateReference(item.AstronomicalYear, item.Month, item.Day, item.Precision),
                        item.TitleKey, item.DescriptionKey, item.RegionId, item.PolityId, item.EvidenceLevel,
                        PrefixSource(archive, "Chronicle provenance: " + item.Origin + ". " + item.SourceNotes),
                        item.Category, item.Scope, item.VisibleToPlayer);
                    chronicleEntries.Add(entry);
                    chronicle.AddEntry(entry);
                }
                HistoricalLegacyGameComponent legacy = Current.Game.GetComponent<HistoricalLegacyGameComponent>();
                foreach (LegacyFamilyMigrationData migration in archive.Migrations)
                    legacy.RecordMigration(migration.FamilyId, migration.FromRegionId, migration.ToRegionId,
                        migration.AstronomicalYear, "Import " + importId + ": " + migration.Reason, migration.IsSimulated);
                if (!legacy.RecordImport(plan.Archive.ArchiveId, targetScenarioId, importId, plan.Simulation.SimulationSeed))
                    throw new InvalidOperationException("This archive was imported by another action while this plan was open.");
                importMarkerRecorded = true;
                return true;
            }
            catch (Exception exception)
            {
                string failure = error;
                foreach (Pawn pawn in spawned.Where(item => item != null && !item.Destroyed).ToList()) pawn.Destroy(DestroyMode.Vanish);
                foreach (Thing thing in relicThings.Where(item => item != null && !item.Destroyed).ToList()) thing.Destroy(DestroyMode.Vanish);
                Current.Game.GetComponent<HistoricalGenealogyGameComponent>()?.RollbackLegacyRecords(archive.Persons.Select(item => item.PersonId), archive.Families.Select(item => item.FamilyId));
                Current.Game.GetComponent<HistoricalCultureGameComponent>()?.RollbackLegacyData(archive.Persons.Select(item => item.PersonId), archive.Families.Select(item => item.FamilyId));
                Current.Game.GetComponent<HistoricalRelicGameComponent>()?.RollbackLegacyData(plan.RelicIds,
                    relicEvents.Select(item => item.EventId));
                Current.Game.GetComponent<HistoricalChronicleGameComponent>()?.RollbackLegacyEntries(chronicleEntries.Select(item => item.EntryId));
                Current.Game.GetComponent<HistoricalLegacyGameComponent>()?.RollbackImport(importId, importMarkerRecorded);
                error = "Legacy import was rolled back: " + (failure ?? exception.Message);
                return false;
            }
        }

        private static void AddCultureData(HistoricalLegacyArchive archive)
        {
            HistoricalCultureGameComponent culture = Current.Game.GetComponent<HistoricalCultureGameComponent>();
            foreach (LegacyPersonData person in archive.Persons)
            {
                culture.Add(new HistoricalPersonCultureState { PersonId = person.PersonId, BirthCultureId = person.BirthCultureId,
                    PrimaryCultureId = person.PrimaryCultureId, Familiarities = person.Familiarities.Where(item => item != null)
                        .Select(item => new HistoricalCulturalFamiliarity { CultureId = item.CultureId, Familiarity = item.Familiarity }).ToList() });
            }
            foreach (LegacyFamilyData family in archive.Families)
                culture.Add(new HistoricalFamilyCultureState { FamilyId = family.FamilyId, PrimaryCultureId = family.PrimaryCultureId,
                    PreservedTraditionIds = new List<string>(family.PreservedTraditionIds ?? new List<string>()) });
        }

        public static bool IsFamilyInLineage(string selectedFamilyId, string relicFamilyId, IEnumerable<LegacyFamilyData> families)
        {
            if (string.IsNullOrWhiteSpace(relicFamilyId)) return false;
            var parents = (families ?? Enumerable.Empty<LegacyFamilyData>()).Where(item => item != null)
                .GroupBy(item => item.FamilyId, StringComparer.Ordinal).ToDictionary(group => group.Key,
                    group => group.First().ParentFamilyId, StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string current = selectedFamilyId;
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current) && parents.TryGetValue(current, out string parent))
            {
                if (string.Equals(current, relicFamilyId, StringComparison.Ordinal)) return true;
                current = parent;
            }
            return string.Equals(current, relicFamilyId, StringComparison.Ordinal);
        }

        private static void AddPawnRelations(HistoricalLegacyArchive archive, HashSet<string> selected,
            IDictionary<string, Pawn> pawns, HistoricalDate targetDate)
        {
            foreach (LegacyKinshipData relation in archive.Kinships.Where(item => selected.Contains(item.PersonIdA) && selected.Contains(item.PersonIdB)))
            {
                if (relation.Type == HistoricalKinshipType.Spouse && relation.EndAstronomicalYear.HasValue
                    && new HistoricalDate(relation.EndAstronomicalYear.Value, relation.EndMonth.Value, relation.EndDay.Value) <= targetDate)
                    continue;
                Pawn a = pawns[relation.PersonIdA], b = pawns[relation.PersonIdB];
                PawnRelationDef def = relation.Type == HistoricalKinshipType.Spouse ? PawnRelationDefOf.Spouse : PawnRelationDefOf.Parent;
                a.relations.AddDirectRelation(def, b);
            }
        }

        private static string PrefixSource(HistoricalLegacyArchive archive, string notes) =>
            "Imported from archive " + archive.ArchiveId + " (" + archive.SourceScenarioId + ", " + archive.SourceSaveIdentifier + "). " + notes;

        private static string MakeImportId(string archiveId, string scenarioId, int seed)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(archiveId + "|" + scenarioId + "|" + seed));
                return "import-" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 24);
            }
        }

        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
