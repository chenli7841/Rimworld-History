using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Lifespan;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Legacy;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Genealogy
{
    public sealed class HistoricalGenealogyGameComponent : GameComponent
    {
        private const int CurrentDataVersion = 1;
        private string scenarioDefName;
        private int dataVersion;
        private bool initialized;
        private List<HistoricalPersonRecord> persons = new List<HistoricalPersonRecord>();
        private List<HistoricalFamilyRecord> families = new List<HistoricalFamilyRecord>();
        private List<HistoricalKinshipRelation> relations = new List<HistoricalKinshipRelation>();
        private List<HistoricalNaturalLifespanRecord> lifespans = new List<HistoricalNaturalLifespanRecord>();
        private List<HistoricalPawnIdentity> pawnIdentities = new List<HistoricalPawnIdentity>();
        private long lastLifespanProcessedAbsoluteDay = long.MinValue;
        private int lastCheckGameTick = -1;
        private Dictionary<string, HistoricalPersonRecord> personsById = new Dictionary<string, HistoricalPersonRecord>(StringComparer.Ordinal);
        private Dictionary<string, HistoricalFamilyRecord> familiesById = new Dictionary<string, HistoricalFamilyRecord>(StringComparer.Ordinal);
        private Dictionary<string, HistoricalNaturalLifespanRecord> lifespansByPersonId = new Dictionary<string, HistoricalNaturalLifespanRecord>(StringComparer.Ordinal);

        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public IEnumerable<HistoricalPersonRecord> Persons => persons ?? Enumerable.Empty<HistoricalPersonRecord>();
        public IEnumerable<HistoricalFamilyRecord> Families => families ?? Enumerable.Empty<HistoricalFamilyRecord>();
        public IEnumerable<HistoricalKinshipRelation> Relations => relations ?? Enumerable.Empty<HistoricalKinshipRelation>();
        public IEnumerable<HistoricalNaturalLifespanRecord> LifespanRecords => lifespans ?? Enumerable.Empty<HistoricalNaturalLifespanRecord>();

        public HistoricalPersonRecord FindPerson(string id) => id != null && personsById.TryGetValue(id, out HistoricalPersonRecord person) ? person : null;
        public HistoricalFamilyRecord FindFamily(string id) => id != null && familiesById.TryGetValue(id, out HistoricalFamilyRecord family) ? family : null;
        public HistoricalNaturalLifespanRecord FindLifespan(string id) => id != null && lifespansByPersonId.TryGetValue(id, out HistoricalNaturalLifespanRecord lifespan) ? lifespan : null;

        public Pawn FindPawn(string personId)
        {
            HistoricalPawnIdentity binding = pawnIdentities?.FirstOrDefault(item => item != null && item.PersonId == personId && item.Pawn != null && !item.Pawn.Dead);
            return binding?.Pawn ?? pawnIdentities?.FirstOrDefault(item => item != null && item.PersonId == personId && item.Pawn != null)?.Pawn;
        }

        public string FindPersonId(Pawn pawn) => pawn == null
            ? null
            : pawnIdentities?.FirstOrDefault(item => item != null && ReferenceEquals(item.Pawn, pawn))?.PersonId;

        internal bool InitializeForScenario(string scenarioId)
        {
            if (initialized) return string.Equals(scenarioDefName, scenarioId, StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(scenarioId)) return false;
            scenarioDefName = scenarioId;
            dataVersion = CurrentDataVersion;
            initialized = true;
            RebuildIndexes();
            Log.Message("[Historical RimWorld] Initialized historical genealogy for scenario " + scenarioId + ".");
            return true;
        }

        public bool TryRegisterPawn(Pawn pawn, HistoricalDate currentDate, out string personId, out string error)
        {
            personId = FindPersonId(pawn);
            error = null;
            if (pawn == null || pawn.Destroyed) { error = "A live Pawn is required."; return false; }
            if (!initialized) { error = "Historical genealogy is not initialized."; return false; }
            if (personId != null) return true;

            string name = pawn.Name == null ? "Unknown person" : pawn.Name.ToStringShort;
            do { personId = HistoricalPersonIdGenerator.Generate(); } while (personsById.ContainsKey(personId));
            HistoricalDate birthDate = EstimateBirthDate(pawn, currentDate, out HistoricalBirthDateSource birthSource);
            string familyId = FindRegisteredParentFamily(pawn);
            HistoricalPersonRecord person = new HistoricalPersonRecord();
            person.Initialize(personId, familyId, name, HistoricalPersonOrigin.Generated, birthDate, birthSource, null, null);
            persons.Add(person);
            personsById[personId] = person;

            if (string.IsNullOrWhiteSpace(familyId))
            {
                HistoricalFamilyRecord family = CreateFamily(person, name + " " + Translator.Translate("HR_Genealogy_FamilySuffix"));
                familyId = family.FamilyId;
                person.SetFamilyId(familyId);
                LogChronicle("HR_FamilyFounded_" + familyId, "HR_Genealogy_FamilyFoundedTitle",
                    "HR_Genealogy_FamilyFoundedDescription", currentDate);
            }
            else
            {
                HistoricalFamilyRecord family = FindFamily(familyId);
                if (family == null)
                {
                    family = new HistoricalFamilyRecord();
                    family.Initialize(familyId, name + " " + Translator.Translate("HR_Genealogy_FamilySuffix"), personId, null, null);
                    families.Add(family);
                    familiesById[familyId] = family;
                }
                family.AddMember(personId);
            }

            int lifespanYears = HistoricalLifespanGenerator.GenerateLifespanYears(person);
            HistoricalNaturalLifespanRecord lifespan = new HistoricalNaturalLifespanRecord();
            lifespan.Initialize(personId, lifespanYears, birthDate.AddYears(lifespanYears));
            lifespans.Add(lifespan);
            lifespansByPersonId[personId] = lifespan;
            HistoricalPawnIdentity identity = new HistoricalPawnIdentity();
            identity.Initialize(personId, pawn);
            pawnIdentities.Add(identity);
            HistoricalCultureManager.TryEnsurePerson(personId, out _);
            if (!string.IsNullOrWhiteSpace(familyId)) HistoricalCultureManager.TryEnsureFamily(familyId, personId, out _);

            if (birthSource == HistoricalBirthDateSource.ObservedInGame)
                LogChronicle("HR_PersonBorn_" + personId, "HR_Genealogy_PersonBornTitle",
                    "HR_Genealogy_PersonBornDescription", birthDate);
            return true;
        }

        public bool TryAddKinshipRelation(string personIdA, string personIdB, HistoricalKinshipType type,
            HistoricalDate startDate, out string error)
        {
            if (!Enum.IsDefined(typeof(HistoricalKinshipType), type)) { error = "Unknown kinship type."; return false; }
            if (!HistoricalGenealogyValidator.CanAddRelation(personIdA, personIdB, type, Persons, Relations, out error)) return false;
            HistoricalKinshipRelation relation = new HistoricalKinshipRelation();
            if (type == HistoricalKinshipType.Spouse && string.CompareOrdinal(personIdA, personIdB) > 0)
            {
                string swap = personIdA; personIdA = personIdB; personIdB = swap;
            }
            relation.Initialize(personIdA, personIdB, type, startDate);
            relations.Add(relation);
            if (type == HistoricalKinshipType.BiologicalParent || type == HistoricalKinshipType.AdoptiveParent)
                InheritFamily(personIdA, personIdB);
            if (type == HistoricalKinshipType.Spouse)
            {
                HistoricalCultureManager.ExchangeByMarriage(personIdA, personIdB);
                LogChronicle("HR_PersonMarried_" + Guid.NewGuid().ToString("N"), "HR_Genealogy_PersonMarriedTitle",
                    "HR_Genealogy_PersonMarriedDescription", startDate);
            }
            return true;
        }

        public bool TrySetDisplayName(string personId, string displayName, out string error)
        {
            error = null;
            HistoricalPersonRecord person = FindPerson(personId);
            if (person == null) { error = "Historical person was not found."; return false; }
            if (string.IsNullOrWhiteSpace(displayName)) { error = "A non-empty display name is required."; return false; }
            person.SetDisplayName(displayName.Trim());
            return true;
        }

        internal bool TryImportLegacyRecords(HistoricalLegacyArchive archive, IDictionary<string, Pawn> bindings, out string error)
        {
            error = null;
            if (!initialized) { error = "Historical genealogy must be initialized first."; return false; }
            if (archive == null) { error = "Legacy archive is empty."; return false; }
            if (archive.Persons.Any(item => personsById.ContainsKey(item.PersonId)) || archive.Families.Any(item => familiesById.ContainsKey(item.FamilyId)))
            { error = "One or more imported PersonId or FamilyId values already exist in this save."; return false; }

            foreach (LegacyFamilyData data in archive.Families)
            {
                var family = new HistoricalFamilyRecord();
                family.Initialize(data.FamilyId, data.FamilyName, data.FounderPersonId, data.OriginRegionId, data.OriginPolityId);
                family.SetParentFamilyId(data.ParentFamilyId);
                families.Add(family); familiesById[data.FamilyId] = family;
            }
            foreach (LegacyPersonData data in archive.Persons)
            {
                HistoricalDate birth = new HistoricalDate(data.BirthAstronomicalYear, data.BirthMonth, data.BirthDay);
                var person = new HistoricalPersonRecord();
                person.Initialize(data.PersonId, data.FamilyId, data.DisplayName, data.Origin, birth,
                    data.Origin == HistoricalPersonOrigin.LegacySimulated
                        ? HistoricalBirthDateSource.LegacySimulation : HistoricalBirthDateSource.HistoricalRecord,
                    data.BirthRegionId, data.BirthAllegiancePolityId);
                person.SetLegacyCurrentLocation(data.LastKnownRegionId, data.LastKnownAllegiancePolityId);
                if (!data.IsAliveAtExport && data.DeathAstronomicalYear.HasValue)
                    person.MarkDeath(new HistoricalDate(data.DeathAstronomicalYear.Value, data.DeathMonth.Value, data.DeathDay.Value), data.DeathCause);
                persons.Add(person);
                familiesById[data.FamilyId].AddMember(data.PersonId);
                HistoricalDate naturalDeath = new HistoricalDate(data.NaturalDeathAstronomicalYear.Value, data.NaturalDeathMonth.Value, data.NaturalDeathDay.Value);
                var lifespan = new HistoricalNaturalLifespanRecord(); lifespan.Initialize(data.PersonId, data.AssignedLifespanYears.Value, naturalDeath);
                if (!data.IsAliveAtExport) lifespan.SetNaturalDeathProcessed(true);
                lifespans.Add(lifespan);
            }
            foreach (LegacyKinshipData data in archive.Kinships)
            {
                var relation = new HistoricalKinshipRelation();
                relation.Initialize(data.PersonIdA, data.PersonIdB, data.Type,
                    new HistoricalDate(data.StartAstronomicalYear.Value, data.StartMonth.Value, data.StartDay.Value));
                if (data.EndAstronomicalYear.HasValue) relation.Close(new HistoricalDate(data.EndAstronomicalYear.Value, data.EndMonth.Value, data.EndDay.Value));
                relations.Add(relation);
            }
            foreach (var binding in bindings ?? new Dictionary<string, Pawn>())
            {
                if (!personsById.ContainsKey(binding.Key) || binding.Value == null) continue;
                var identity = new HistoricalPawnIdentity(); identity.Initialize(binding.Key, binding.Value); pawnIdentities.Add(identity);
            }
            RebuildIndexes();
            return true;
        }

        internal void RollbackLegacyRecords(IEnumerable<string> personIds, IEnumerable<string> familyIds)
        {
            var people = new HashSet<string>(personIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var familiesToRemove = new HashSet<string>(familyIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            persons.RemoveAll(item => item != null && people.Contains(item.PersonId));
            families.RemoveAll(item => item != null && familiesToRemove.Contains(item.FamilyId));
            relations.RemoveAll(item => item != null && (people.Contains(item.PersonIdA) || people.Contains(item.PersonIdB)));
            lifespans.RemoveAll(item => item != null && people.Contains(item.PersonId));
            pawnIdentities.RemoveAll(item => item != null && people.Contains(item.PersonId));
            RebuildIndexes();
        }

        public bool TryEndKinshipRelation(string personIdA, string personIdB, HistoricalKinshipType type,
            HistoricalDate endDate, out string error)
        {
            error = null;
            if (type == HistoricalKinshipType.Spouse && string.CompareOrdinal(personIdA, personIdB) > 0)
            { string swap = personIdA; personIdA = personIdB; personIdB = swap; }
            HistoricalKinshipRelation relation = relations.FirstOrDefault(item => item != null && item.IsActive
                && item.Type == type && item.PersonIdA == personIdA && item.PersonIdB == personIdB);
            if (relation == null) { error = "No active relationship with these IDs exists."; return false; }
            relation.Close(endDate);
            return true;
        }

        internal void RecordPawnDeath(string personId, HistoricalDate date, HistoricalDeathCause cause)
        {
            HistoricalPersonRecord person = FindPerson(personId);
            if (person == null || !person.IsAlive) return;
            person.MarkDeath(date, cause);
            HistoricalNaturalLifespanRecord lifespan = FindLifespan(personId);
            if (cause == HistoricalDeathCause.NaturalLifespan) lifespan?.SetNaturalDeathProcessed(true);
            LogChronicle("HR_PersonDied_" + personId + "_" + person.DeathCount,
                "HR_Genealogy_PersonDiedTitle", "HR_Genealogy_PersonDiedDescription", date);
        }

        internal void RecordPawnResurrection(string personId, HistoricalDate date)
        {
            HistoricalPersonRecord person = FindPerson(personId);
            if (person == null || person.IsAlive) return;
            person.MarkResurrected();
            FindLifespan(personId)?.SetNaturalDeathProcessed(false);
            LogChronicle("HR_PersonResurrected_" + personId + "_" + person.ResurrectionCount,
                "HR_Genealogy_PersonResurrectedTitle", "HR_Genealogy_PersonResurrectedDescription", date);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.TickManager.TicksGame % 250 != 0
                || lastCheckGameTick == Find.TickManager.TicksGame) return;
            lastCheckGameTick = Find.TickManager.TicksGame;
            if (!HistoricalScenarioManager.IsScenarioActive) return;
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate)) return;
            if (!InitializeForScenario(HistoricalScenarioManager.ActiveScenarioDefName)) return;

            List<Pawn> activePawns = PawnsFinder.AllMapsWorldAndTemporary_Alive
                .Where(IsPlayerHumanlikePawn).Distinct().ToList();
            foreach (Pawn pawn in activePawns)
            {
                if (!TryRegisterPawn(pawn, currentDate, out _, out string error))
                    Log.Warning("[Historical RimWorld] Pawn identity registration was skipped: " + error);
            }

            foreach (Pawn pawn in activePawns) SyncPawnRelations(pawn, currentDate);
            CloseEndedSpouseRelations(currentDate);
            SyncPawnLifecycles(currentDate);

            if (lastLifespanProcessedAbsoluteDay != currentDate.AbsoluteDay)
            {
                HistoricalNaturalDeathService.ProcessDueDeaths(this, currentDate);
                lastLifespanProcessedAbsoluteDay = currentDate.AbsoluteDay;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalGenealogyScenarioDefName");
            Scribe_Values.Look(ref dataVersion, "historicalGenealogyDataVersion", 0);
            Scribe_Values.Look(ref initialized, "historicalGenealogyInitialized", false);
            Scribe_Collections.Look(ref persons, "historicalPersons", LookMode.Deep);
            Scribe_Collections.Look(ref families, "historicalFamilies", LookMode.Deep);
            Scribe_Collections.Look(ref relations, "historicalKinshipRelations", LookMode.Deep);
            Scribe_Collections.Look(ref lifespans, "historicalNaturalLifespans", LookMode.Deep);
            Scribe_Collections.Look(ref pawnIdentities, "historicalPawnIdentities", LookMode.Deep);
            Scribe_Values.Look(ref lastLifespanProcessedAbsoluteDay, "historicalLastLifespanProcessedAbsoluteDay", long.MinValue);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                persons = persons ?? new List<HistoricalPersonRecord>();
                families = families ?? new List<HistoricalFamilyRecord>();
                relations = relations ?? new List<HistoricalKinshipRelation>();
                lifespans = lifespans ?? new List<HistoricalNaturalLifespanRecord>();
                pawnIdentities = pawnIdentities ?? new List<HistoricalPawnIdentity>();
                pawnIdentities = pawnIdentities.Where(item => item != null && !string.IsNullOrWhiteSpace(item.PersonId) && item.Pawn != null)
                    .GroupBy(item => item.PersonId, StringComparer.Ordinal)
                    .Select(group => group.OrderBy(item => item.Pawn.Dead).First())
                    .GroupBy(item => item.Pawn).Select(group => group.First()).ToList();
                foreach (HistoricalNaturalLifespanRecord invalid in lifespans.Where(item => item != null && !item.IsValid()))
                    Log.Warning("[Historical RimWorld] Invalid lifespan data was retained but excluded from processing.");
                lastCheckGameTick = -1;
                if (initialized && dataVersion != CurrentDataVersion)
                {
                    Log.Warning("[Historical RimWorld] Genealogy data version " + dataVersion + " differs from " + CurrentDataVersion + "; person IDs and lifespan deadlines were preserved.");
                    dataVersion = CurrentDataVersion;
                }
                RebuildIndexes();
                foreach (string missingRegion in personsById.Values.Select(item => item.CurrentRegionId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)
                    .Where(id => DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(id) == null))
                    Log.Warning("[Historical RimWorld] A saved person references missing historical region Def " + missingRegion + "; the ID was preserved.");
                foreach (string missingPolity in personsById.Values.Select(item => item.CurrentAllegiancePolityId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)
                    .Where(id => DefDatabase<HistoricalStateDef>.GetNamedSilentFail(id) == null))
                    Log.Warning("[Historical RimWorld] A saved person references missing historical polity Def " + missingPolity + "; the ID was preserved.");
            }
        }

        private static bool IsPlayerHumanlikePawn(Pawn pawn) => pawn != null && !pawn.Destroyed && !pawn.Dead
            && pawn.RaceProps != null && pawn.RaceProps.Humanlike && pawn.Faction == Faction.OfPlayer;

        private static HistoricalDate EstimateBirthDate(Pawn pawn, HistoricalDate currentDate, out HistoricalBirthDateSource source)
        {
            long ageTicks = Math.Max(0L, pawn.ageTracker?.AgeChronologicalTicks ?? 0L);
            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby && ageTicks <= HistoricalCalendarConstants.TicksPerDay)
            {
                source = HistoricalBirthDateSource.ObservedInGame;
                return currentDate;
            }
            source = HistoricalBirthDateSource.EstimatedFromInitialAge;
            long ageDays = ageTicks / HistoricalCalendarConstants.TicksPerDay;
            return currentDate.AddDays(-ageDays);
        }

        private string FindRegisteredParentFamily(Pawn pawn)
        {
            if (pawn.relations == null) return null;
            foreach (DirectPawnRelation relation in pawn.relations.DirectRelations)
            {
                if (relation == null || relation.otherPawn == null || !IsParentRelation(relation.def)) continue;
                string parentId = FindPersonId(relation.otherPawn);
                string familyId = FindPerson(parentId)?.FamilyId;
                if (!string.IsNullOrWhiteSpace(familyId)) return familyId;
            }
            return null;
        }

        private void SyncPawnRelations(Pawn pawn, HistoricalDate currentDate)
        {
            string personId = FindPersonId(pawn);
            if (personId == null || pawn.relations == null) return;
            foreach (DirectPawnRelation relation in pawn.relations.DirectRelations.ToList())
            {
                if (relation?.otherPawn == null) continue;
                string otherId = FindPersonId(relation.otherPawn);
                if (otherId == null || otherId == personId) continue;
                HistoricalKinshipType? type = null;
                string parentId = null;
                string childId = null;
                if (IsParentRelation(relation.def)) { type = HistoricalKinshipType.BiologicalParent; parentId = otherId; childId = personId; }
                else if (relation.def == PawnRelationDefOf.Child) { type = HistoricalKinshipType.BiologicalParent; parentId = personId; childId = otherId; }
                else if (relation.def == PawnRelationDefOf.Spouse) type = HistoricalKinshipType.Spouse;
                if (!type.HasValue) continue;
                string a = type == HistoricalKinshipType.BiologicalParent ? parentId : personId;
                string b = type == HistoricalKinshipType.BiologicalParent ? childId : otherId;
                if (!TryAddKinshipRelation(a, b, type.Value, currentDate, out string error)
                    && !error.Contains("already exists"))
                    Log.Warning("[Historical RimWorld] Could not synchronize relation " + relation.def.defName + ": " + error);
            }
        }

        private static bool IsParentRelation(PawnRelationDef def) => def == PawnRelationDefOf.Parent || def == PawnRelationDefOf.ParentBirth;

        private void SyncPawnLifecycles(HistoricalDate currentDate)
        {
            foreach (HistoricalPawnIdentity identity in pawnIdentities ?? Enumerable.Empty<HistoricalPawnIdentity>())
            {
                if (identity?.Pawn == null) continue;
                HistoricalPersonRecord person = FindPerson(identity.PersonId);
                if (person == null) continue;
                if (identity.Pawn.Dead && person.IsAlive) RecordPawnDeath(identity.PersonId, currentDate, HistoricalDeathCause.Unknown);
                else if (!identity.Pawn.Dead && !person.IsAlive) RecordPawnResurrection(identity.PersonId, currentDate);
            }
        }

        private void CloseEndedSpouseRelations(HistoricalDate currentDate)
        {
            foreach (HistoricalKinshipRelation relation in relations.Where(item => item != null
                && item.IsActive && item.Type == HistoricalKinshipType.Spouse).ToList())
            {
                Pawn pawnA = FindPawn(relation.PersonIdA);
                Pawn pawnB = FindPawn(relation.PersonIdB);
                if (pawnA == null || pawnB == null || pawnA.Dead || pawnB.Dead) continue;
                bool stillMarried = pawnA.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, pawnB) == true
                    || pawnB.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, pawnA) == true;
                if (!stillMarried) relation.Close(currentDate);
            }
        }

        private HistoricalFamilyRecord CreateFamily(HistoricalPersonRecord founder, string name)
        {
            string id;
            do { id = HistoricalFamilyIdGenerator.Generate(); } while (familiesById.ContainsKey(id));
            HistoricalFamilyRecord family = new HistoricalFamilyRecord();
            family.Initialize(id, name, founder.PersonId, founder.BirthRegionId, founder.BirthPolityId);
            family.AddMember(founder.PersonId);
            families.Add(family);
            familiesById[id] = family;
            return family;
        }

        private void InheritFamily(string parentId, string childId)
        {
            HistoricalPersonRecord parent = FindPerson(parentId);
            HistoricalPersonRecord child = FindPerson(childId);
            if (parent == null || child == null || string.IsNullOrWhiteSpace(parent.FamilyId)
                || parent.FamilyId == child.FamilyId) return;
            FindFamily(child.FamilyId)?.RemoveMember(childId);
            child.SetFamilyId(parent.FamilyId);
            FindFamily(parent.FamilyId)?.AddMember(childId);
            HistoricalCultureManager.InheritFamilyCulture(parentId, childId);
        }

        private void LogChronicle(string entryId, string titleKey, string descriptionKey, HistoricalDate date)
        {
            if (!HistoricalChronicleManager.TryAddLocalExperience(entryId, titleKey, descriptionKey,
                null, null, HistoricalEvidenceLevel.Speculative, "HR_Genealogy_Source", out string error))
                Log.Warning("[Historical RimWorld] A genealogy chronicle entry was not recorded: " + error);
        }

        private void RebuildIndexes()
        {
            personsById = new Dictionary<string, HistoricalPersonRecord>(StringComparer.Ordinal);
            familiesById = new Dictionary<string, HistoricalFamilyRecord>(StringComparer.Ordinal);
            lifespansByPersonId = new Dictionary<string, HistoricalNaturalLifespanRecord>(StringComparer.Ordinal);
            foreach (HistoricalPersonRecord person in persons ?? Enumerable.Empty<HistoricalPersonRecord>())
                if (person != null && person.IsValid() && !personsById.ContainsKey(person.PersonId)) personsById.Add(person.PersonId, person);
            foreach (HistoricalFamilyRecord family in families ?? Enumerable.Empty<HistoricalFamilyRecord>())
                if (family != null && !string.IsNullOrWhiteSpace(family.FamilyId) && !familiesById.ContainsKey(family.FamilyId)) familiesById.Add(family.FamilyId, family);
            foreach (HistoricalNaturalLifespanRecord lifespan in lifespans ?? Enumerable.Empty<HistoricalNaturalLifespanRecord>())
                if (lifespan != null && lifespan.IsValid() && !lifespansByPersonId.ContainsKey(lifespan.PersonId)) lifespansByPersonId.Add(lifespan.PersonId, lifespan);
        }
    }
}
