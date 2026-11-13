using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Chronicle
{
    public enum HistoricalEvidenceKind { Eyewitness, RecoveredDocument, BattlefieldArtifact, OralTradition, Reconstruction }
    public enum HistoricalEvidenceReliability { DirectObservation, ContemporaryDocument, SecondhandReport, LaterTradition, Reconstruction }

    /// <summary>Durable, save-backed record of what one player Pawn actually observed at Lishan.</summary>
    public sealed class HistoricalWitnessRecord : IExposable
    {
        private string recordId;
        private string eventId;
        private Pawn witness;
        private string landmarkId;
        private HistoricalWitnessGrade grade;
        private int observationTicks;
        private int lastObservationTick = -1;
        private int historicalYear;
        private int month = -1;
        private int day = -1;
        private List<string> observedStageIds = new List<string>();
        private List<string> evidenceIds = new List<string>();
        private bool archived;

        public string RecordId => recordId;
        public string EventId => eventId;
        public Pawn Witness => witness;
        public string LandmarkId => landmarkId;
        public HistoricalWitnessGrade Grade => grade;
        public int ObservationTicks => observationTicks;
        public HistoricalDateReference EventDate => new HistoricalDateReference(historicalYear, null, null, HistoricalDatePrecision.Year);
        public IEnumerable<string> ObservedStageIds => observedStageIds ?? Enumerable.Empty<string>();
        public IEnumerable<string> EvidenceIds => evidenceIds ?? Enumerable.Empty<string>();
        public bool Archived => archived;

        public void ExposeData()
        {
            Scribe_Values.Look(ref recordId, "recordId");
            Scribe_Values.Look(ref eventId, "eventId");
            Scribe_References.Look(ref witness, "witness");
            Scribe_Values.Look(ref landmarkId, "landmarkId");
            Scribe_Values.Look(ref grade, "grade", HistoricalWitnessGrade.None);
            Scribe_Values.Look(ref observationTicks, "observationTicks", 0);
            Scribe_Values.Look(ref lastObservationTick, "lastObservationTick", -1);
            Scribe_Values.Look(ref historicalYear, "historicalYear", 1);
            Scribe_Values.Look(ref month, "month", -1);
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Collections.Look(ref observedStageIds, "observedStageIds", LookMode.Value);
            Scribe_Collections.Look(ref evidenceIds, "evidenceIds", LookMode.Value);
            Scribe_Values.Look(ref archived, "archived", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                observedStageIds = observedStageIds ?? new List<string>();
                evidenceIds = evidenceIds ?? new List<string>();
            }
        }

        internal void Initialize(string id, string sourceEvent, Pawn pawn, string landmark, HistoricalDate date)
        {
            recordId = id; eventId = sourceEvent; witness = pawn; landmarkId = landmark;
            historicalYear = date.AstronomicalYear;
            month = date.Month; day = date.Day;
        }

        internal void Observe(int ticks, int tick, HistoricalWitnessGrade value, string stageId, string evidenceId)
        {
            if (lastObservationTick != tick)
            {
                observationTicks = Math.Max(0, observationTicks + ticks);
                lastObservationTick = tick;
            }
            if (value > grade) grade = value;
            if (!string.IsNullOrWhiteSpace(stageId) && observedStageIds != null && !observedStageIds.Contains(stageId)) observedStageIds.Add(stageId);
            if (!string.IsNullOrWhiteSpace(evidenceId) && evidenceIds != null && !evidenceIds.Contains(evidenceId)) evidenceIds.Add(evidenceId);
        }
        internal void MarkArchived() => archived = true;
    }

    public sealed class HistoricalEvidenceRecord : IExposable
    {
        private string evidenceId;
        private string eventId;
        private HistoricalEvidenceKind kind;
        private HistoricalEvidenceReliability reliability;
        private Pawn sourcePawn;
        private Thing sourceThing;
        private string landmarkId;
        private int acquisitionTick;
        private int acquisitionAstronomicalYear;
        private int acquisitionMonth = -1;
        private int acquisitionDay = -1;
        private List<string> relatedRecordIds = new List<string>();

        public string EvidenceId => evidenceId;
        public string EventId => eventId;
        public HistoricalEvidenceKind Kind => kind;
        public HistoricalEvidenceReliability Reliability => reliability;
        public Pawn SourcePawn => sourcePawn;
        public Thing SourceThing => sourceThing;
        public string LandmarkId => landmarkId;
        public HistoricalDateReference AcquisitionDate => acquisitionAstronomicalYear == 0
            ? null : new HistoricalDateReference(acquisitionAstronomicalYear, acquisitionMonth < 0 ? (int?)null : acquisitionMonth,
                acquisitionDay < 0 ? (int?)null : acquisitionDay,
                acquisitionDay >= 0 ? HistoricalDatePrecision.Day : acquisitionMonth >= 0 ? HistoricalDatePrecision.Month : HistoricalDatePrecision.Year);
        public IEnumerable<string> RelatedRecordIds => relatedRecordIds ?? Enumerable.Empty<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref evidenceId, "evidenceId");
            Scribe_Values.Look(ref eventId, "eventId");
            Scribe_Values.Look(ref kind, "kind", HistoricalEvidenceKind.Reconstruction);
            Scribe_Values.Look(ref reliability, "reliability", HistoricalEvidenceReliability.Reconstruction);
            Scribe_References.Look(ref sourcePawn, "sourcePawn");
            Scribe_References.Look(ref sourceThing, "sourceThing");
            Scribe_Values.Look(ref landmarkId, "landmarkId");
            Scribe_Values.Look(ref acquisitionTick, "acquisitionTick", -1);
            Scribe_Values.Look(ref acquisitionAstronomicalYear, "acquisitionAstronomicalYear", 0);
            Scribe_Values.Look(ref acquisitionMonth, "acquisitionMonth", -1);
            Scribe_Values.Look(ref acquisitionDay, "acquisitionDay", -1);
            Scribe_Collections.Look(ref relatedRecordIds, "relatedRecordIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) relatedRecordIds = relatedRecordIds ?? new List<string>();
        }

        internal void Initialize(string id, string sourceEvent, HistoricalEvidenceKind evidenceKind,
            HistoricalEvidenceReliability confidence, Pawn pawn, Thing thing, string landmark,
            IEnumerable<string> relatedIds)
        {
            evidenceId = id; eventId = sourceEvent; kind = evidenceKind; reliability = confidence;
            sourcePawn = pawn; sourceThing = thing; landmarkId = landmark;
            acquisitionTick = Find.TickManager?.TicksGame ?? -1;
            if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
            {
                acquisitionAstronomicalYear = date.AstronomicalYear;
                acquisitionMonth = date.Month;
                acquisitionDay = date.Day;
            }
            relatedRecordIds = relatedIds == null ? new List<string>() : relatedIds.Distinct().ToList();
        }
    }

    /// <summary>Versioned game-save storage for witness/evidence records; uses the existing chronicle for player-facing history.</summary>
    public sealed class HistoricalEvidenceGameComponent : GameComponent
    {
        private const int CurrentVersion = 1;
        private int dataVersion;
        private List<HistoricalWitnessRecord> witnesses = new List<HistoricalWitnessRecord>();
        private List<HistoricalEvidenceRecord> evidence = new List<HistoricalEvidenceRecord>();

        public HistoricalEvidenceGameComponent(Game game) { }
        public IEnumerable<HistoricalWitnessRecord> Witnesses => witnesses ?? Enumerable.Empty<HistoricalWitnessRecord>();
        public IEnumerable<HistoricalEvidenceRecord> Evidence => evidence ?? Enumerable.Empty<HistoricalEvidenceRecord>();
        public HistoricalWitnessRecord FindWitness(string id) => witnesses?.FirstOrDefault(item => item?.RecordId == id);
        public HistoricalEvidenceRecord FindEvidence(string id) => evidence?.FirstOrDefault(item => item?.EvidenceId == id);

        internal HistoricalWitnessRecord GetOrCreateWitness(Pawn pawn)
        {
            if (pawn == null) return null;
            string id = HistoricalWitnessArchiveService.WitnessId(pawn);
            HistoricalWitnessRecord record = FindWitness(id);
            if (record != null) return record;
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)) return null;
            record = new HistoricalWitnessRecord();
            record.Initialize(id, HistoricalWitnessArchiveService.EventId, pawn,
                HistoricalWitnessArchiveService.LandmarkId, date);
            (witnesses ?? (witnesses = new List<HistoricalWitnessRecord>())).Add(record);
            return record;
        }

        internal bool AddEvidence(HistoricalEvidenceRecord record)
        {
            if (record == null || FindEvidence(record.EvidenceId) != null) return false;
            (evidence ?? (evidence = new List<HistoricalEvidenceRecord>())).Add(record);
            return true;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive || Find.TickManager == null
                || Find.TickManager.TicksGame % 250 != 0) return;
            HistoricalWitnessArchiveService.TryArchiveReturnedWitnesses(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "historicalEvidenceDataVersion", 0);
            Scribe_Collections.Look(ref witnesses, "historicalWitnessRecords", LookMode.Deep);
            Scribe_Collections.Look(ref evidence, "historicalEvidenceRecords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) dataVersion = CurrentVersion;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                witnesses = witnesses ?? new List<HistoricalWitnessRecord>();
                evidence = evidence ?? new List<HistoricalEvidenceRecord>();
                witnesses.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.RecordId));
                evidence.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.EvidenceId));
                if (dataVersion != CurrentVersion) dataVersion = CurrentVersion;
            }
        }
    }

    public static class HistoricalWitnessArchiveService
    {
        public const string EventId = "HE_001_LishanCrisis";
        public const string LandmarkId = "HR_Landmark_Lishan";
        private const string ConsumerId = "HE001_PlayerEvidenceBindings";

        public static IEnumerable<HistoricalWitnessRecord> Witnesses => Current.Game?.GetComponent<HistoricalEvidenceGameComponent>()?.Witnesses
            ?? Enumerable.Empty<HistoricalWitnessRecord>();
        public static IEnumerable<HistoricalEvidenceRecord> Evidence => Current.Game?.GetComponent<HistoricalEvidenceGameComponent>()?.Evidence
            ?? Enumerable.Empty<HistoricalEvidenceRecord>();

        internal static string WitnessId(Pawn pawn) => "HE001_Witness_Pawn_" + (pawn?.thingIDNumber ?? 0);

        public static void RecordObservation(Pawn pawn, string observedStageId, HistoricalWitnessGrade questGrade = HistoricalWitnessGrade.None)
        {
            if (pawn == null || pawn.Faction != Faction.OfPlayer || pawn.Dead || pawn.Destroyed
                || !HistoricalScenarioManager.IsScenarioActive || !HistoricalEventManager.IsEventKnownToCurrentScenario(EventId)) return;
            HistoricalEvidenceGameComponent component = Current.Game?.GetComponent<HistoricalEvidenceGameComponent>();
            HistoricalWitnessRecord record = component?.GetOrCreateWitness(pawn);
            if (record == null) return;
            HistoricalEventQuestDef witnessDef = DefDatabase<HistoricalEventQuestDef>.GetNamedSilentFail("HE001_Quest_WitnessBattle");
            int nextTicks = record.ObservationTicks + 250;
            HistoricalWitnessGrade grade = nextTicks >= (witnessDef?.observationTicks ?? 1800)
                ? HistoricalWitnessGrade.FullHistoricalWitness
                : nextTicks >= (witnessDef?.battleWitnessTicks ?? 1200) ? HistoricalWitnessGrade.BattleWitness
                : nextTicks >= (witnessDef?.partialWitnessTicks ?? 600) ? HistoricalWitnessGrade.PartialWitness : HistoricalWitnessGrade.None;
            if (questGrade > grade) grade = questGrade;
            string evidenceId = "HE001_Eyewitness_" + pawn.thingIDNumber;
            record.Observe(250, Find.TickManager?.TicksGame ?? -1, grade, observedStageId, evidenceId);
            if (component.FindEvidence(evidenceId) == null)
            {
                HistoricalEvidenceRecord evidence = new HistoricalEvidenceRecord();
                evidence.Initialize(evidenceId, EventId, HistoricalEvidenceKind.Eyewitness,
                    HistoricalEvidenceReliability.DirectObservation, pawn, null, LandmarkId, new[] { record.RecordId });
                component.AddEvidence(evidence);
            }
        }

        public static bool RecordRecoveredRoyalDocument(Thing document)
        {
            if (document == null || document.Destroyed || document.def?.defName != "HR_Thing_RoyalRecordBundle") return false;
            HistoricalEvidenceGameComponent component = Current.Game?.GetComponent<HistoricalEvidenceGameComponent>();
            if (component == null) return false;
            string id = "HE001_RoyalDocument_" + document.thingIDNumber;
            bool added = component.FindEvidence(id) != null;
            if (!added)
            {
                HistoricalEvidenceRecord evidence = new HistoricalEvidenceRecord();
                evidence.Initialize(id, EventId, HistoricalEvidenceKind.RecoveredDocument,
                    HistoricalEvidenceReliability.ContemporaryDocument, null, document, LandmarkId, null);
                added = component.AddEvidence(evidence);
            }
            if (!added) return false;
            string entryId = "HE001_RoyalDocumentArchive_" + document.thingIDNumber;
            if (!HistoricalChronicleManager.Entries.Any(entry => entry != null && entry.EntryId == entryId))
            {
                bool archived = HistoricalChronicleManager.TryAddWitnessArchive(entryId,
                    new HistoricalDateReference(component.FindEvidence(id).AcquisitionDate?.AstronomicalYear ?? 1,
                        component.FindEvidence(id).AcquisitionDate?.Month, component.FindEvidence(id).AcquisitionDate?.Day,
                        component.FindEvidence(id).AcquisitionDate?.Precision ?? HistoricalDatePrecision.Year),
                    "HE001_RoyalDocumentArchive_Title", "HE001_RoyalDocumentArchive_Description",
                    "HR_Region_Guanzhong", "HR_State_EasternZhou", HistoricalEvidenceLevel.Probable,
                    string.Format(Translator.Translate("HE001_RoyalDocumentArchive_SourceNotes"), document.thingIDNumber, id), out string error);
                if (!archived) Log.Warning("[Historical RimWorld] Recovered royal-document evidence is waiting for chronicle registration: " + error);
                else Log.Message("[Historical RimWorld] Recorded a recovered, game-fictional HE-001 royal document as evidence; the Thing remains in the game.");
            }
            return true;
        }

        internal static void TryArchiveReturnedWitnesses(HistoricalEvidenceGameComponent component)
        {
            if (component == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)) return;
            foreach (HistoricalWitnessRecord record in component.Witnesses.Where(item => item != null && !item.Archived
                         && item.Grade >= HistoricalWitnessGrade.PartialWitness && item.Witness != null
                         && item.Witness.Spawned && item.Witness.Map?.IsPlayerHome == true && !item.Witness.Dead).ToList())
            {
                string key = "HE001_WitnessArchive_" + record.RecordId;
                if (HistoricalChronicleManager.Entries.Any(entry => entry != null && entry.EntryId == key))
                {
                    record.MarkArchived();
                    continue;
                }
                string grade = "HE001_WitnessGrade_" + record.Grade;
                string gradeLabel = Translator.Translate(grade);
                string stages = string.Join(", ", record.ObservedStageIds.Select(id => Translator.Translate("HE001_WitnessStage_" + id)));
                string sourceNotes = string.Format(Translator.Translate("HE001_WitnessArchive_SourceNotes"),
                    record.Witness.thingIDNumber, gradeLabel, stages,
                    string.Join(", ", record.EvidenceIds));
                if (!HistoricalChronicleManager.TryAddWitnessArchive(key, record.EventDate,
                    "HE001_WitnessArchive_Title", "HE001_WitnessArchive_Description",
                    "HR_Region_Guanzhong", "HR_State_EasternZhou", HistoricalEvidenceLevel.Probable,
                    sourceNotes, out string error))
                {
                    Log.Warning("[Historical RimWorld] Could not archive returned HE-001 witness " + record.RecordId + ": " + error);
                    continue;
                }
                record.MarkArchived();
            }
        }
    }
}
