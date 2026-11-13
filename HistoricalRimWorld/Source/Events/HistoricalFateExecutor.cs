using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Characters;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalFateGameComponent : GameComponent
    {
        private const int DataVersion = 1;
        private int dataVersion;
        private List<HistoricalFateExecutionRecord> records = new List<HistoricalFateExecutionRecord>();
        private readonly HashSet<string> reportedFailures = new HashSet<string>(StringComparer.Ordinal);

        public HistoricalFateGameComponent(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.TickManager.TicksGame % 60 != 0) return;
            foreach (HistoricalFateDef fate in DefDatabase<HistoricalFateDef>.AllDefsListForReading
                         .Where(item => item != null && HistoricalEventManager.HasReachedStage(item.fateEventId, item.fateStageId)))
                ExecuteScheduledFate(fate);
        }

        public bool HasExecuted(string characterId, string eventId) => records?.Any(record => record != null
            && record.HistoricalCharacterId == characterId && record.EventId == eventId
            && record.Status == HistoricalFateExecutionStatus.Completed) == true;

        public bool HasExecutedFate(string fateId) => records?.Any(record => record != null
            && record.FateId == fateId && record.Status == HistoricalFateExecutionStatus.Completed) == true;

        public HistoricalFateExecutionRecord FindRecord(HistoricalFateDef fate) => fate == null ? null
            : records?.FirstOrDefault(record => record != null && record.FateId == fate.defName);

        internal bool ExecuteScheduledFate(HistoricalFateDef fate)
        {
            if (!HistoricalFateExecutor.CanExecuteFate(fate?.historicalCharacterId, fate?.fateEventId, fate?.fateStageId)) return false;
            if (records == null) records = new List<HistoricalFateExecutionRecord>();
            HistoricalFateExecutionRecord record = FindRecord(fate);
            if (record?.Status == HistoricalFateExecutionStatus.Completed) return true;
            if (record == null)
            {
                record = new HistoricalFateExecutionRecord();
                record.Initialize(fate);
                records.Add(record);
            }

            HistoricalCharacterWorldComponent characters = Current.Game?.GetComponent<HistoricalCharacterWorldComponent>();
            HistoricalCharacterDef characterDef = DefDatabase<HistoricalCharacterDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.historicalCharacterId == fate.historicalCharacterId);
            HistoricalCharacterState character = characters?.FindState(fate.historicalCharacterId)
                ?? characters?.GetOrRegister(characterDef);
            Pawn pawn = character?.Pawn;
            record.BeginExecution(pawn);

            if (fate.fateType == HistoricalFateType.ScheduledSurvival)
            {
                if (character?.IsHistoricallyDead == true || (pawn != null && pawn.Dead))
                {
                    record.MarkFailedRecoverable("Scheduled survival target was already dead; no resurrection or duplicate Pawn was attempted.");
                    LogFailureOnce(fate, "Cannot commit scheduled survival for already-dead character " + fate.historicalCharacterId + ".");
                    return false;
                }
                record.MarkCompleted();
                Log.Message("[Historical RimWorld] Committed scheduled survival for " + fate.historicalCharacterId
                    + " at " + fate.fateEventId + "/" + fate.fateStageId + ".");
                return true;
            }

            if (fate.fateType != HistoricalFateType.ScheduledDeath)
            {
                record.MarkFailedRecoverable("This fate type is reserved for a later phase.");
                return false;
            }

            if (character?.IsHistoricallyDead == true || (pawn != null && pawn.Dead))
            {
                character?.MarkHistoricallyDead(fate.fateEventId, fate.defName);
                record.MarkCompleted();
                return true;
            }

            try
            {
                if (pawn != null && !pawn.Destroyed)
                {
                    HistoricalFateExecutor.AuthorizeDeath(pawn);
                    try { pawn.Kill(null, null); }
                    finally { HistoricalFateExecutor.RevokeDeathAuthorization(pawn); }
                    if (!pawn.Dead)
                    {
                        record.MarkFailedRecoverable("Pawn.Kill returned without committing death; the fate will retry safely.");
                        LogFailureOnce(fate, "Scheduled death did not complete for " + fate.historicalCharacterId + "; it will retry.");
                        return false;
                    }
                }
                // This persistent identity-level marker also handles a Pawn that never existed or
                // was destroyed by an external system before its death date.
                character?.MarkHistoricallyDead(fate.fateEventId, fate.defName);
                record.MarkCompleted();
                Log.Message("[Historical RimWorld] Executed scheduled historical death for " + fate.historicalCharacterId
                    + " via " + fate.fateEventId + "/" + fate.fateStageId + ".");
                return true;
            }
            catch (Exception exception)
            {
                record.MarkFailedRecoverable(exception.Message);
                LogFailureOnce(fate, "Scheduled death failed for " + fate.historicalCharacterId
                    + "; it will recover on a later tick: " + exception);
                return false;
            }
        }

        private void LogFailureOnce(HistoricalFateDef fate, string message)
        {
            if (reportedFailures.Add(fate?.defName ?? "unknown"))
                Log.Error("[Historical RimWorld] " + message);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "historicalFateDataVersion", 0);
            Scribe_Collections.Look(ref records, "historicalFateExecutionRecords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) dataVersion = DataVersion;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records = records ?? new List<HistoricalFateExecutionRecord>();
                records.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.FateId));
                if (dataVersion != DataVersion) dataVersion = DataVersion;
            }
        }
    }

    public sealed class HistoricalFateExecutionRecord : IExposable
    {
        private string fateId;
        private string historicalCharacterId;
        private string eventId;
        private HistoricalFateExecutionStatus status;
        private string failureReason;
        private int astronomicalYear;
        private int month;
        private int day;
        private int pawnThingId = -1;

        public string FateId => fateId;
        public string HistoricalCharacterId => historicalCharacterId;
        public string EventId => eventId;
        public HistoricalFateExecutionStatus Status => status;
        public string FailureReason => failureReason;
        public int AstronomicalYear => astronomicalYear;
        public int Month => month;
        public int Day => day;
        public int PawnThingId => pawnThingId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref fateId, "fateId");
            Scribe_Values.Look(ref historicalCharacterId, "historicalCharacterId");
            Scribe_Values.Look(ref eventId, "eventId");
            Scribe_Values.Look(ref status, "status", HistoricalFateExecutionStatus.Pending);
            Scribe_Values.Look(ref failureReason, "failureReason");
            Scribe_Values.Look(ref astronomicalYear, "astronomicalYear");
            Scribe_Values.Look(ref month, "month", -1);
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref pawnThingId, "pawnThingId", -1);
        }

        internal void Initialize(HistoricalFateDef fate)
        {
            fateId = fate.defName;
            historicalCharacterId = fate.historicalCharacterId;
            eventId = fate.fateEventId;
        }

        internal void BeginExecution(Pawn pawn)
        {
            status = HistoricalFateExecutionStatus.Executing;
            failureReason = null;
            if (pawn != null) pawnThingId = pawn.thingIDNumber;
            if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
            {
                astronomicalYear = date.AstronomicalYear;
                month = date.Month;
                day = date.Day;
            }
        }

        internal void MarkCompleted() { status = HistoricalFateExecutionStatus.Completed; failureReason = null; }
        internal void MarkFailedRecoverable(string reason)
        {
            status = HistoricalFateExecutionStatus.FailedRecoverable;
            failureReason = reason;
        }
    }

    public static class HistoricalFateExecutor
    {
        private static readonly HashSet<Pawn> authorizedDeaths = new HashSet<Pawn>();

        public static bool CanExecuteFate(string historicalCharacterId, string eventId, string stageId)
        {
            if (string.IsNullOrWhiteSpace(historicalCharacterId) || string.IsNullOrWhiteSpace(eventId)
                || string.IsNullOrWhiteSpace(stageId) || !HistoricalEventManager.HasReachedStage(eventId, stageId)) return false;
            return DefDatabase<HistoricalFateDef>.AllDefsListForReading.Any(fate => fate != null
                && fate.historicalCharacterId == historicalCharacterId && fate.fateEventId == eventId
                && fate.fateStageId == stageId);
        }

        public static bool ExecuteScheduledDeath(string historicalCharacterId, string eventId, string stageId)
        {
            HistoricalFateDef fate = DefDatabase<HistoricalFateDef>.AllDefsListForReading.FirstOrDefault(item => item != null
                && item.historicalCharacterId == historicalCharacterId && item.fateEventId == eventId
                && item.fateStageId == stageId && item.fateType == HistoricalFateType.ScheduledDeath);
            return fate != null && Current.Game?.GetComponent<HistoricalFateGameComponent>()?.ExecuteScheduledFate(fate) == true;
        }

        public static bool HasExecutedFate(string historicalCharacterId, string eventId) =>
            Current.Game?.GetComponent<HistoricalFateGameComponent>()?.HasExecuted(historicalCharacterId, eventId) == true;

        public static bool HasExecutedFate(HistoricalFateDef fate) => fate != null
            && Current.Game?.GetComponent<HistoricalFateGameComponent>()?.HasExecutedFate(fate.defName) == true;

        public static bool HasExecutedScheduledDeath(string historicalCharacterId) =>
            DefDatabase<HistoricalFateDef>.AllDefsListForReading.Any(fate => fate != null
                && fate.historicalCharacterId == historicalCharacterId
                && fate.fateType == HistoricalFateType.ScheduledDeath
                && HasExecutedFate(fate));

        internal static bool IsDeathAuthorized(Pawn pawn) => pawn != null && authorizedDeaths.Contains(pawn);
        internal static void AuthorizeDeath(Pawn pawn) { if (pawn != null) authorizedDeaths.Add(pawn); }
        internal static void RevokeDeathAuthorization(Pawn pawn) { if (pawn != null) authorizedDeaths.Remove(pawn); }
    }
}
