using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Events
{
    public enum HistoricalBattleSideQuestType { Witness, Refugees, FieldMedical, RoyalRecords }
    public enum HistoricalWitnessGrade { None, PartialWitness, BattleWitness, FullHistoricalWitness }

    public sealed class HistoricalEventQuestDef : Def
    {
        public string historicalEventId;
        public string activeStageId = "MilitaryCrisis";
        public string expirationStageId = "Completed";
        public string landmarkId = "HR_Landmark_Lishan";
        public HistoricalBattleSideQuestType questType;
        public int observationTicks = 1800;
        public int partialWitnessTicks = 600;
        public int battleWitnessTicks = 1200;
        public int refugeeCount = 5;
        public int minimumRescueCount = 3;
        public int minimumTreatedCount = 2;
        public string refugeePawnKindDefName = "HR_PawnKind_Farmer";
        public string refugeeFactionDefName = "HR_Faction_NeutralSmallTribes";
        public string refugeeSpawnZoneId = "OpposingArmyApproachZone";
        public string escapeZoneId = "CivilianEscapeZone";
        public string documentThingDefName = "HR_Thing_RoyalRecordBundle";
        public string documentSpawnZoneId = "RoyalArmyApproachZone";

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (DefDatabase<HistoricalEventDef>.AllDefsListForReading.All(item => item == null || item.eventId != historicalEventId))
                yield return "historicalEventId must resolve to an existing HistoricalEventDef.";
            if (string.IsNullOrWhiteSpace(activeStageId) || string.IsNullOrWhiteSpace(expirationStageId))
                yield return "activeStageId and expirationStageId are required.";
            if (observationTicks < 250 || partialWitnessTicks < 1 || battleWitnessTicks < partialWitnessTicks)
                yield return "Witness durations must be positive and ordered.";
            if (refugeeCount < 1 || minimumRescueCount < 1 || minimumRescueCount > refugeeCount)
                yield return "Refugee count and rescue threshold are invalid.";
            if (minimumTreatedCount < 1) yield return "minimumTreatedCount must be positive.";
            if (questType == HistoricalBattleSideQuestType.Refugees
                && (DefDatabase<PawnKindDef>.GetNamedSilentFail(refugeePawnKindDefName) == null
                    || DefDatabase<FactionDef>.GetNamedSilentFail(refugeeFactionDefName) == null))
                yield return "Refugee pawn kind and faction must resolve.";
            if (questType == HistoricalBattleSideQuestType.RoyalRecords
                && DefDatabase<ThingDef>.GetNamedSilentFail(documentThingDefName) == null)
                yield return "documentThingDefName must resolve to a ThingDef.";
        }
    }

    public sealed class HistoricalBattleSideQuestState : IExposable
    {
        private string defName;
        private Quest quest;
        private Map map;
        private int mapId = -1;
        private bool objectsCreated;
        private bool dangerWarningShown;
        private bool adoptionChoiceMade;
        private bool adoptionPromptOpen;
        private bool acceptedRefugees;
        private int observationTicks;
        private int rescuedCount;
        private int treatedCount;
        private HistoricalWitnessGrade witnessGrade;
        private List<Pawn> pawns = new List<Pawn>();
        private List<Pawn> rescuedPawns = new List<Pawn>();
        private List<Pawn> treatedPawns = new List<Pawn>();
        private Thing document;

        public string DefName => defName;
        public Quest Quest => quest;
        public Map Map => map;
        public int ObservationTicks => observationTicks;
        public int RescuedCount => rescuedCount;
        public int TreatedCount => treatedCount;
        public HistoricalWitnessGrade WitnessGrade => witnessGrade;
        public IEnumerable<Pawn> Pawns => pawns ?? Enumerable.Empty<Pawn>();
        public Thing Document => document;
        public bool ObjectsCreated => objectsCreated;
        public bool DangerWarningShown => dangerWarningShown;
        public bool AdoptionChoiceMade => adoptionChoiceMade;
        public bool AdoptionPromptOpen => adoptionPromptOpen;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_References.Look(ref quest, "quest");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref objectsCreated, "objectsCreated", false);
            Scribe_Values.Look(ref dangerWarningShown, "dangerWarningShown", false);
            Scribe_Values.Look(ref adoptionChoiceMade, "adoptionChoiceMade", false);
            Scribe_Values.Look(ref adoptionPromptOpen, "adoptionPromptOpen", false);
            Scribe_Values.Look(ref acceptedRefugees, "acceptedRefugees", false);
            Scribe_Values.Look(ref observationTicks, "observationTicks", 0);
            Scribe_Values.Look(ref rescuedCount, "rescuedCount", 0);
            Scribe_Values.Look(ref treatedCount, "treatedCount", 0);
            Scribe_Values.Look(ref witnessGrade, "witnessGrade", HistoricalWitnessGrade.None);
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
            Scribe_Collections.Look(ref rescuedPawns, "rescuedPawns", LookMode.Reference);
            Scribe_Collections.Look(ref treatedPawns, "treatedPawns", LookMode.Reference);
            Scribe_References.Look(ref document, "document");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns = pawns ?? new List<Pawn>();
                rescuedPawns = rescuedPawns ?? new List<Pawn>();
                treatedPawns = treatedPawns ?? new List<Pawn>();
                adoptionPromptOpen = false;
                if (map == null && mapId >= 0) map = Verse.Find.Maps?.FirstOrDefault(item => item != null && item.uniqueID == mapId);
            }
        }
        internal void Initialize(HistoricalEventQuestDef def, Quest nativeQuest) { defName = def?.defName; quest = nativeQuest; }
        internal void SetMap(Map value) { map = value; mapId = value?.uniqueID ?? -1; }
        internal void MarkObjectsCreated() => objectsCreated = true;
        internal void AdoptExistingQuest(HistoricalEventQuestDef def, Quest nativeQuest)
        {
            Initialize(def, nativeQuest);
            // The native Quest/Thing/Pawn objects are already owned by RimWorld. If the companion
            // integration record was missing in an older or interrupted save, never fabricate them again.
            objectsCreated = true;
        }
        internal void MarkDangerWarning() => dangerWarningShown = true;
        internal void MarkAdoptionChoice(bool accepted) { adoptionChoiceMade = true; acceptedRefugees = accepted; }
        internal void SetAdoptionPromptOpen(bool value) => adoptionPromptOpen = value;
        internal bool AcceptedRefugees => acceptedRefugees;
        internal void AddPawn(Pawn pawn) { if (pawn != null && pawns != null && !pawns.Contains(pawn)) pawns.Add(pawn); }
        internal bool ContainsPawn(Pawn pawn) => pawn != null && pawns?.Contains(pawn) == true;
        internal void AddTreatedPawn(Pawn pawn)
        { if (pawn != null && treatedPawns != null && !treatedPawns.Contains(pawn)) { treatedPawns.Add(pawn); treatedCount++; } }
        internal bool ContainsTreatedPawn(Pawn pawn) => pawn != null && treatedPawns?.Contains(pawn) == true;
        internal void SetDocument(Thing thing) => document = thing;
        internal void AddObservation(int ticks, HistoricalWitnessGrade grade)
        { observationTicks += Math.Max(0, ticks); if (grade > witnessGrade) witnessGrade = grade; }
        internal void AddRescued(Pawn pawn)
        {
            if (pawn != null && rescuedPawns != null && !rescuedPawns.Contains(pawn))
            { rescuedPawns.Add(pawn); rescuedCount++; }
        }
    }

    /// <summary>Owns one-shot HE-001 side quests. Its progress never advances or edits the historical event.</summary>
    public sealed class HistoricalBattleSideQuestGameComponent : GameComponent
    {
        private const string EventId = "HE_001_LishanCrisis";
        private const int DataVersion = 1;
        private int dataVersion;
        private int lastUpdateTick = -1;
        private List<HistoricalBattleSideQuestState> states = new List<HistoricalBattleSideQuestState>();

        public HistoricalBattleSideQuestGameComponent(Game game) { }
        public IEnumerable<HistoricalBattleSideQuestState> States => states ?? Enumerable.Empty<HistoricalBattleSideQuestState>();
        public HistoricalBattleSideQuestState Find(string id) => states?.FirstOrDefault(item => item?.DefName == id);

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive
                || HistoricalScenarioManager.ActiveScenarioDefName != "HR_Scenario_EasternZhou_770BCE"
                || Verse.Find.TickManager == null || Verse.Find.TickManager.TicksGame % 250 != 0
                || lastUpdateTick == Verse.Find.TickManager.TicksGame) return;
            lastUpdateTick = Verse.Find.TickManager.TicksGame;
            TickQuests();
        }

        private void TickQuests()
        {
            foreach (HistoricalEventQuestDef def in DefDatabase<HistoricalEventQuestDef>.AllDefsListForReading
                         .Where(item => item != null && item.historicalEventId == EventId))
            {
                HistoricalBattleSideQuestState state = Find(def.defName);
                HistoricalEventRuntimeState timeline = HistoricalEventManager.GetEvent(EventId);
                if (state == null && timeline?.TimelineStatus == HistoricalEventRuntimeStatus.Active
                    && HistoricalEventManager.HasReachedStage(EventId, def.activeStageId)
                    && !HistoricalEventManager.HasReachedStage(EventId, def.expirationStageId))
                    state = CreateQuest(def);
                if (state == null || state.Quest == null || IsEnded(state.Quest)) continue;
                if (def.questType == HistoricalBattleSideQuestType.RoyalRecords
                    && state.Document != null && IsDocumentStored(state.Document))
                    RecordDocumentSecured(state.Document);
                TickState(def, state);
                if (HistoricalEventManager.HasReachedStage(EventId, def.expirationStageId))
                    End(state, QuestEndOutcome.Fail);
            }
        }

        private HistoricalBattleSideQuestState CreateQuest(HistoricalEventQuestDef def)
        {
            if (Verse.Find.QuestManager == null) return null;
            if (states == null) states = new List<HistoricalBattleSideQuestState>();
            if (Find(def.defName) != null) return Find(def.defName);
            Quest existing = Verse.Find.QuestManager.QuestsListForReading.FirstOrDefault(quest => quest != null
                && quest.tags != null && quest.tags.Contains(EventId) && quest.tags.Contains(def.defName));
            if (existing != null)
            {
                HistoricalEventLongTermQuestBridge.EnsureQuestRoot(existing);
                HistoricalBattleSideQuestState recovered = new HistoricalBattleSideQuestState();
                recovered.AdoptExistingQuest(def, existing);
                states.Add(recovered);
                Log.Warning("[Historical RimWorld] Reconnected saved native Quest " + def.defName
                    + " to HE-001 without regenerating quest targets.");
                return recovered;
            }
            Quest quest = new Quest
            {
                name = ("HE001_SideQuest_" + def.questType + "_Name").Translate(),
                description = ("HE001_SideQuest_" + def.questType + "_Description").Translate(),
                initiallyAccepted = true,
                tags = new List<string> { "HistoricalRimWorld", EventId, def.defName }
            };
            HistoricalEventLongTermQuestBridge.EnsureQuestRoot(quest);
            QuestPart_DescriptionPart descriptionPart = quest.AddPart<QuestPart_DescriptionPart>();
            descriptionPart.descriptionPart = ("HE001_SideQuest_" + def.questType + "_Objective").Translate();
            Verse.Find.QuestManager?.Add(quest);
            HistoricalBattleSideQuestState state = new HistoricalBattleSideQuestState();
            state.Initialize(def, quest);
            states.Add(state);
            Log.Message("[Historical RimWorld] Started side quest " + def.defName + "; HE-001 remains authoritative.");
            return state;
        }

        private static void TickState(HistoricalEventQuestDef def, HistoricalBattleSideQuestState state)
        {
            Map map = Verse.Find.CurrentMap;
            if (map == null || !HistoricalEventLocationService.TryGetScene(map, out HistoricalMapSceneComponent scene)
                || scene.TemplateId != "HR_Map_LishanBattlefield") return;
            state.SetMap(map);
            if (def.questType == HistoricalBattleSideQuestType.Witness) WarnAtBattle(state, map, scene);
            switch (def.questType)
            {
                case HistoricalBattleSideQuestType.Witness: TickWitness(def, state, map, scene); break;
                case HistoricalBattleSideQuestType.Refugees: TickRefugees(def, state, map, scene); break;
                case HistoricalBattleSideQuestType.FieldMedical: RefreshWoundedTargets(state); break;
                case HistoricalBattleSideQuestType.RoyalRecords: EnsureDocument(def, state, map, scene); break;
            }
        }

        private static void WarnAtBattle(HistoricalBattleSideQuestState state, Map map, HistoricalMapSceneComponent scene)
        {
            if (state.DangerWarningShown || !scene.TryGetZone("MainBattleZone", out CellRect danger)) return;
            Pawn colonist = map.mapPawns?.FreeColonists?.FirstOrDefault(pawn => pawn != null && pawn.Spawned
                && !pawn.Dead && danger.ExpandedBy(8).Contains(pawn.Position));
            if (colonist == null) return;
            state.MarkDangerWarning();
            Messages.Message("HE001_BattleDangerWarning".Translate(), colonist, MessageTypeDefOf.CautionInput, false);
        }

        private static void TickWitness(HistoricalEventQuestDef def, HistoricalBattleSideQuestState state, Map map,
            HistoricalMapSceneComponent scene)
        {
            if (!scene.TryGetZone("ObserverOuterZone", out CellRect observer)) return;
            HistoricalBattleDef battleDef = DefDatabase<HistoricalBattleDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.historicalEventId == EventId);
            HistoricalArmyGameComponent armies = Current.Game?.GetComponent<HistoricalArmyGameComponent>();
            IEnumerable<HistoricalArmyMemberState> allMembers = (armies?.FindArmy(battleDef?.royalArmyId)?.Members
                ?? Enumerable.Empty<HistoricalArmyMemberState>()).Concat(armies?.FindArmy(battleDef?.opposingArmyId)?.Members
                ?? Enumerable.Empty<HistoricalArmyMemberState>());
            HistoricalArmyRuntime royalArmy = armies?.FindArmy(battleDef?.royalArmyId);
            HistoricalArmyRuntime opposingArmy = armies?.FindArmy(battleDef?.opposingArmyId);
            List<Pawn> royal = (royalArmy?.Members ?? Enumerable.Empty<HistoricalArmyMemberState>())
                .Select(item => item?.Pawn).Where(pawn => pawn != null && pawn.Spawned && pawn.Map == map
                    && pawn.Faction?.def?.defName == royalArmy.FactionId
                    && !pawn.Dead && !pawn.Downed).ToList();
            List<Pawn> opposing = (opposingArmy?.Members ?? Enumerable.Empty<HistoricalArmyMemberState>())
                .Select(item => item?.Pawn).Where(pawn => pawn != null && pawn.Spawned && pawn.Map == map
                    && pawn.Faction?.def?.defName == opposingArmy.FactionId
                    && !pawn.Dead && !pawn.Downed).ToList();
            HistoricalBattleInstance battle = Current.Game?.GetComponent<HistoricalBattleGameComponent>()
                ?.FindBattle(battleDef?.battleId);
            if (battle?.BattleStarted != true && battle?.RoyalRetreatStarted != true) return;
            List<Pawn> observers = (map.mapPawns?.FreeColonists ?? Enumerable.Empty<Pawn>()).Where(pawn => pawn != null && pawn.Spawned
                && !pawn.Dead && !pawn.Downed && pawn.Awake() && observer.Contains(pawn.Position)
                && royal.Any(target => GenSight.LineOfSight(pawn.Position, target.Position, map))
                && opposing.Any(target => GenSight.LineOfSight(pawn.Position, target.Position, map))).ToList();
            if (observers.Count == 0) return;
            int accumulated = state.ObservationTicks + 250;
            HistoricalWitnessGrade grade = accumulated >= def.observationTicks ? HistoricalWitnessGrade.FullHistoricalWitness
                : accumulated >= def.battleWitnessTicks ? HistoricalWitnessGrade.BattleWitness
                : accumulated >= def.partialWitnessTicks ? HistoricalWitnessGrade.PartialWitness : HistoricalWitnessGrade.None;
            state.AddObservation(250, grade);
            string observedStage = battle.Phase == HistoricalBattlePhase.RoyalRetreat ? "RoyalRetreatObserved"
                : battle.Phase == HistoricalBattlePhase.DeathScene ? "HistoricalDeathSceneObserved"
                : "BattleEngagementObserved";
            foreach (Pawn witness in observers)
            {
                HistoricalWitnessArchiveService.RecordObservation(witness, observedStage);
                if (battle.Phase == HistoricalBattlePhase.DeathScene)
                {
                    Pawn king = HistoricalCharacterRegistry.GetPawn("HR_Character_ZhouYouWang");
                    if (king != null && king.Spawned && king.Map == map && !king.Dead
                        && GenSight.LineOfSight(witness.Position, king.Position, map))
                        HistoricalWitnessArchiveService.RecordObservation(witness, "KingYouDeathSceneObserved");
                }
            }
            if (grade == HistoricalWitnessGrade.FullHistoricalWitness) End(state, QuestEndOutcome.Success);
        }

        private static void TickRefugees(HistoricalEventQuestDef def, HistoricalBattleSideQuestState state, Map map,
            HistoricalMapSceneComponent scene)
        {
            if (!state.ObjectsCreated)
            {
                if (SpawnRefugees(def, state, map, scene)) state.MarkObjectsCreated();
            }
            if (!scene.TryGetZone(def.escapeZoneId, out CellRect exit)) return;
            foreach (Pawn refugee in state.Pawns.Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead))
            {
                if (exit.Contains(refugee.Position))
                {
                    bool assisted = map.mapPawns.FreeColonists.Any(colonist => colonist != null && colonist.Spawned
                        && !colonist.Dead && colonist.Position.DistanceToSquared(refugee.Position) <= 12 * 12);
                    if (assisted) state.AddRescued(refugee);
                }
                else if (refugee.jobs != null && (refugee.CurJob == null || refugee.CurJob.def != JobDefOf.Goto))
                {
                    IntVec3 cell = FindSafeCell(map, exit);
                    if (cell.IsValid) refugee.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobTag.Misc);
                }
            }
            int total = state.Pawns.Count(pawn => pawn != null);
            if (!state.AdoptionChoiceMade && !state.AdoptionPromptOpen && total > 0
                && state.RescuedCount >= Math.Max(def.minimumRescueCount, (total + 1) / 2))
                OfferRefugeeChoice(state);
            else if (state.AdoptionChoiceMade) End(state, QuestEndOutcome.Success);
        }

        private static bool SpawnRefugees(HistoricalEventQuestDef def, HistoricalBattleSideQuestState state, Map map,
            HistoricalMapSceneComponent scene)
        {
            if (!scene.TryGetZone(def.refugeeSpawnZoneId, out CellRect spawnZone)) return false;
            Faction faction = Verse.Find.FactionManager?.AllFactions?.FirstOrDefault(item => item?.def?.defName == def.refugeeFactionDefName);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(def.refugeePawnKindDefName);
            if (faction == null || kind == null) return false;
            int count = Math.Max(1, Math.Min(8, def.refugeeCount));
            // Commit the one-shot attempt before creating the first Pawn. Partial failures remain partial
            // rather than creating a second refugee group on the next tick or after save recovery.
            state.MarkObjectsCreated();
            for (int i = state.Pawns.Count(pawn => pawn != null); i < count; i++)
            {
                IntVec3 spawn = FindSafeCell(map, spawnZone);
                if (!spawn.IsValid) return false;
                Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false, forceNoIdeo: true));
                if (pawn == null) return false;
                GenSpawn.Spawn(pawn, spawn, map, WipeMode.Vanish);
                state.AddPawn(pawn);
                if (scene.TryGetZone(def.escapeZoneId, out CellRect exit))
                {
                    IntVec3 destination = FindSafeCell(map, exit);
                    if (destination.IsValid) pawn.jobs?.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, destination), JobTag.Misc);
                }
            }
            return state.Pawns.Count(pawn => pawn != null) >= count;
        }

        private static void OfferRefugeeChoice(HistoricalBattleSideQuestState state)
        {
            state.SetAdoptionPromptOpen(true);
            Verse.Find.WindowStack.Add(new Dialog_MessageBox("HE001_AdoptRefugeesPrompt".Translate(),
                "HE001_AdoptRefugees".Translate(), () =>
                {
                    state.MarkAdoptionChoice(true);
                    state.SetAdoptionPromptOpen(false);
                    foreach (Pawn pawn in state.Pawns.Where(item => item != null && !item.Dead && !item.Destroyed))
                        pawn.SetFaction(Faction.OfPlayer);
                    End(state, QuestEndOutcome.Success);
                },
                "HE001_ReleaseRefugees".Translate(), () =>
                {
                    state.MarkAdoptionChoice(false);
                    state.SetAdoptionPromptOpen(false);
                    End(state, QuestEndOutcome.Success);
                },
                "HE001_AdoptRefugeesTitle".Translate(), false));
        }

        private static void RefreshWoundedTargets(HistoricalBattleSideQuestState state)
        {
            HistoricalBattleDef battleDef = DefDatabase<HistoricalBattleDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.historicalEventId == EventId);
            HistoricalArmyGameComponent armies = Current.Game?.GetComponent<HistoricalArmyGameComponent>();
            IEnumerable<HistoricalArmyMemberState> members = (armies?.FindArmy(battleDef?.royalArmyId)?.Members
                ?? Enumerable.Empty<HistoricalArmyMemberState>()).Concat(armies?.FindArmy(battleDef?.opposingArmyId)?.Members
                ?? Enumerable.Empty<HistoricalArmyMemberState>());
            foreach (Pawn pawn in members.Select(item => item?.Pawn).Where(pawn => pawn != null && pawn.Spawned
                && !pawn.Dead && pawn.health?.hediffSet?.HasTendableHediff(false) == true
                && !HistoricalCharacterProtectionService.IsProtected(pawn)
                && IsRegisteredArmyPawn(pawn)))
                state.AddPawn(pawn);
        }

        private static void EnsureDocument(HistoricalEventQuestDef def, HistoricalBattleSideQuestState state, Map map,
            HistoricalMapSceneComponent scene)
        {
            if (state.Document != null && !state.Document.Destroyed)
            {
                if (IsDocumentStored(state.Document)) RecordDocumentSecured(state.Document);
                return;
            }
            if (state.ObjectsCreated || !scene.TryGetZone(def.documentSpawnZoneId, out CellRect zone)) return;
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(def.documentThingDefName);
            IntVec3 cell = FindSafeCell(map, zone);
            if (thingDef == null || !cell.IsValid) return;
            Thing document = ThingMaker.MakeThing(thingDef);
            GenSpawn.Spawn(document, cell, map, WipeMode.Vanish);
            state.SetDocument(document);
            state.MarkObjectsCreated();
        }

        internal static bool RecordTreatment(Pawn patient)
        {
            if (patient == null || HistoricalCharacterProtectionService.IsProtected(patient)) return false;
            bool treatedByPlayer = patient.Map?.mapPawns?.FreeColonists?.Any(doctor => doctor != null
                && doctor.Faction == Faction.OfPlayer && doctor.CurJob?.def == JobDefOf.TendPatient
                && doctor.CurJob.targetA.Thing == patient) == true;
            if (!treatedByPlayer) return false;
            HistoricalBattleSideQuestGameComponent component = Current.Game?.GetComponent<HistoricalBattleSideQuestGameComponent>();
            HistoricalBattleSideQuestState state = component?.States.FirstOrDefault(item => item?.Quest != null
                && !IsEnded(item.Quest) && DefDatabase<HistoricalEventQuestDef>.GetNamedSilentFail(item.DefName)?.questType
                    == HistoricalBattleSideQuestType.FieldMedical && !item.ContainsTreatedPawn(patient)
                    && IsRegisteredArmyPawn(patient));
            if (state == null) return false;
            state.AddTreatedPawn(patient);
            HistoricalEventQuestDef def = DefDatabase<HistoricalEventQuestDef>.GetNamedSilentFail(state.DefName);
            if (def != null && state.TreatedCount >= def.minimumTreatedCount) End(state, QuestEndOutcome.Success);
            return true;
        }

        private static bool IsRegisteredArmyPawn(Pawn pawn)
        {
            HistoricalArmyGameComponent armies = Current.Game?.GetComponent<HistoricalArmyGameComponent>();
            HistoricalBattleDef battle = DefDatabase<HistoricalBattleDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.historicalEventId == EventId);
            HistoricalArmyRuntime royal = armies?.FindArmy(battle?.royalArmyId);
            HistoricalArmyRuntime opposing = armies?.FindArmy(battle?.opposingArmyId);
            return royal?.Members.Any(member => member?.Pawn == pawn) == true && pawn.Faction?.def?.defName == royal.FactionId
                || opposing?.Members.Any(member => member?.Pawn == pawn) == true && pawn.Faction?.def?.defName == opposing.FactionId;
        }

        internal static bool RecordDocumentSecured(Thing thing)
        {
            HistoricalBattleSideQuestState state = Current.Game?.GetComponent<HistoricalBattleSideQuestGameComponent>()?.States
                .FirstOrDefault(item => item?.Document == thing && item.Quest != null && !IsEnded(item.Quest));
            if (state == null || thing == null || thing.Destroyed || !IsDocumentStored(thing)) return false;
            HistoricalWitnessArchiveService.RecordRecoveredRoyalDocument(thing);
            End(state, QuestEndOutcome.Success);
            return true;
        }

        private static bool IsDocumentStored(Thing thing) => thing != null && !thing.Destroyed
            && (thing.ParentHolder != null || thing.Spawned && StoreUtility.IsInValidStorage(thing));

        private static bool IsEnded(Quest quest) => quest == null || quest.State != QuestState.Ongoing;
        private static void End(HistoricalBattleSideQuestState state, QuestEndOutcome outcome)
        { if (state?.Quest != null && !IsEnded(state.Quest)) state.Quest.End(outcome, true, true); }

        private static IntVec3 FindSafeCell(Map map, CellRect zone)
        {
            if (map == null || zone.IsEmpty) return IntVec3.Invalid;
            return zone.Cells.Where(cell => cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null
                    && !cell.GetThingList(map).Any(thing => thing is Pawn || thing is Building && thing.def.passability == Traversability.Impassable))
                .OrderBy(cell => cell.DistanceToSquared(zone.CenterCell)).FirstOrDefault();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "he001SideQuestDataVersion", 0);
            Scribe_Values.Look(ref lastUpdateTick, "he001SideQuestLastUpdateTick", -1);
            Scribe_Collections.Look(ref states, "he001SideQuestStates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) dataVersion = DataVersion;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                states = states ?? new List<HistoricalBattleSideQuestState>();
                states.RemoveAll(item => item == null || string.IsNullOrEmpty(item.DefName));
                if (dataVersion != DataVersion) dataVersion = DataVersion;
            }
        }
    }

    [HarmonyPatch(typeof(Hediff), nameof(Hediff.Tended))]
    internal static class HistoricalFieldMedicalTendPatch
    {
        private static void Postfix(Hediff __instance)
        {
            try { HistoricalBattleSideQuestGameComponent.RecordTreatment(__instance?.pawn); }
            catch (Exception exception) { Log.Error("[Historical RimWorld] Could not record HE-001 field treatment: " + exception); }
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
    internal static class HistoricalBattleMedicalFloatMenuPatch
    {
        private static void Postfix(List<Pawn> selectedPawns, Vector3 clickPos, ref FloatMenuContext context,
            List<FloatMenuOption> __result)
        {
            try
            {
                if (__result == null || selectedPawns == null || selectedPawns.Count == 0 || context.map == null
                    || !HistoricalScenarioManager.IsScenarioActive || context.map.GetComponent<HistoricalMapSceneComponent>() == null) return;
                IntVec3 cell = IntVec3.FromVector3(clickPos);
                Pawn patient = cell.GetThingList(context.map).OfType<Pawn>().FirstOrDefault(pawn => pawn != null
                    && !pawn.Dead && pawn.Downed && pawn.Faction != Faction.OfPlayer
                    && !HistoricalCharacterProtectionService.IsProtected(pawn)
                    && Current.Game?.GetComponent<HistoricalBattleSideQuestGameComponent>()?.States.Any(state => state != null
                        && state.Quest != null && state.Quest.State == QuestState.Ongoing
                        && DefDatabase<HistoricalEventQuestDef>.GetNamedSilentFail(state.DefName)?.questType
                            == HistoricalBattleSideQuestType.FieldMedical && state.ContainsPawn(pawn)) == true);
                if (patient == null) return;
                Pawn doctor = selectedPawns.FirstOrDefault(pawn => pawn != null && pawn.Faction == Faction.OfPlayer
                    && pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) == true);
                if (doctor == null) return;
                Pawn selectedDoctor = doctor;
                Map rescueMap = context.map;
                __result.Add(new FloatMenuOption("HE001_TendWoundedSoldier".Translate(patient.LabelShort), () =>
                {
                    selectedDoctor.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.TendPatient, patient), JobTag.Misc);
                }));
                __result.Add(new FloatMenuOption("HE001_RescueAndAdoptSoldier".Translate(patient.LabelShort), () =>
                {
                    if (HistoricalCharacterProtectionService.CanChangeFaction(patient))
                        patient.SetFaction(Faction.OfPlayer, selectedDoctor);
                    HistoricalMapSceneComponent scene = rescueMap.GetComponent<HistoricalMapSceneComponent>();
                    IntVec3 destination = scene != null && scene.TryGetZone("CivilianEscapeZone", out CellRect exit)
                        ? FindRescueDestination(rescueMap, exit) : IntVec3.Invalid;
                    JobDef rescueDef = DefDatabase<JobDef>.GetNamedSilentFail("HR_RescueWoundedSoldierToSafety");
                    if (destination.IsValid && rescueDef != null)
                        selectedDoctor.jobs.TryTakeOrderedJob(JobMaker.MakeJob(rescueDef, patient, destination), JobTag.Misc);
                }));
            }
            catch (Exception exception) { Log.Error("[Historical RimWorld] Could not add HE-001 field treatment option: " + exception); }
        }

        private static IntVec3 FindRescueDestination(Map map, CellRect zone)
        {
            if (map == null || zone.IsEmpty) return IntVec3.Invalid;
            return zone.Cells.Where(cell => cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null)
                .OrderBy(cell => cell.DistanceToSquared(zone.CenterCell)).FirstOrDefault();
        }
    }

    public sealed class JobDriver_RescueWoundedSoldierToSafety : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false);
            Toil carry = Toils_Haul.StartCarryThing(TargetIndex.A, false, true, false, false, false);
            yield return carry;
            Toil goToSafety = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return goToSafety;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, goToSafety, false, false);
        }
    }

    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.CanDesignateCell))]
    internal static class HistoricalBattleCoreConstructionPatch
    {
        private static bool Prefix(Designator_Build __instance, IntVec3 c, ref AcceptanceReport __result)
        {
            Map map = __instance?.Map;
            if (map == null || !HistoricalScenarioManager.IsScenarioActive
                || !HistoricalEventManager.HasReachedStage("HE_001_LishanCrisis", "MilitaryCrisis")
                || HistoricalEventManager.HasReachedStage("HE_001_LishanCrisis", "Aftermath")
                || !HistoricalEventLocationService.TryGetScene(map, out HistoricalMapSceneComponent scene)) return true;
            bool core = scene.TryGetZone("RoyalRetreatCorridor", out CellRect retreat) && retreat.Contains(c)
                || scene.TryGetZone("HistoricalDeathSceneZone", out CellRect death) && death.Contains(c);
            if (!core) return true;
            __result = new AcceptanceReport("HE001_CoreConstructionRestricted".Translate());
            return false;
        }
    }
}
