using HistoricalRimWorld.Calendar;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventRuntimeState : IExposable
    {
        private string eventId;
        private HistoricalEventExecutionStatus status;
        private int executionGameTick = -1;
        private int executionAstronomicalYear;
        private int executionMonth = -1;
        private int executionDay = -1;
        private HistoricalEventFailureKind failureKind;
        private string failureReason;
        private long lastAttemptAbsoluteDay = long.MinValue;
        private HistoricalEventRuntimeStatus timelineStatus = HistoricalEventRuntimeStatus.NotStarted;
        private string currentStageId;
        private long transitionSequence;
        private bool historicalOutcomeCommitted;
        private List<string> committedOutcomeIds = new List<string>();
        private List<HistoricalEventStageRecord> stageHistory = new List<HistoricalEventStageRecord>();
        private List<string> appliedOutcomeConsumerKeys = new List<string>();

        public string EventId => eventId;
        public HistoricalEventExecutionStatus Status => status;
        public int? ExecutionGameTick => executionGameTick < 0 ? (int?)null : executionGameTick;
        public int? ExecutionAstronomicalYear => executionGameTick < 0 ? (int?)null : executionAstronomicalYear;
        public int? ExecutionMonth => executionMonth < 0 ? (int?)null : executionMonth;
        public int? ExecutionDay => executionDay < 0 ? (int?)null : executionDay;
        public HistoricalEventFailureKind FailureKind => failureKind;
        public string FailureReason => failureReason;
        public long LastAttemptAbsoluteDay => lastAttemptAbsoluteDay;
        public HistoricalEventRuntimeStatus TimelineStatus => timelineStatus;
        public string CurrentStageId => currentStageId;
        public long TransitionSequence => transitionSequence;
        public bool HistoricalOutcomeCommitted => historicalOutcomeCommitted;
        public IEnumerable<string> CommittedOutcomeIds => committedOutcomeIds ?? Enumerable.Empty<string>();
        public IEnumerable<HistoricalEventStageRecord> StageHistory => stageHistory ?? Enumerable.Empty<HistoricalEventStageRecord>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref eventId, "eventId");
            Scribe_Values.Look(ref status, "status", HistoricalEventExecutionStatus.Pending);
            Scribe_Values.Look(ref executionGameTick, "executionGameTick", -1);
            Scribe_Values.Look(ref executionAstronomicalYear, "executionAstronomicalYear");
            Scribe_Values.Look(ref executionMonth, "executionMonth", -1);
            Scribe_Values.Look(ref executionDay, "executionDay", -1);
            Scribe_Values.Look(ref failureKind, "failureKind", HistoricalEventFailureKind.None);
            Scribe_Values.Look(ref failureReason, "failureReason");
            Scribe_Values.Look(ref lastAttemptAbsoluteDay, "lastAttemptAbsoluteDay", long.MinValue);
            Scribe_Values.Look(ref timelineStatus, "timelineStatus", HistoricalEventRuntimeStatus.NotStarted);
            Scribe_Values.Look(ref currentStageId, "currentStageId");
            Scribe_Values.Look(ref transitionSequence, "transitionSequence", 0L);
            Scribe_Values.Look(ref historicalOutcomeCommitted, "historicalOutcomeCommitted", false);
            Scribe_Collections.Look(ref committedOutcomeIds, "committedOutcomeIds", LookMode.Value);
            Scribe_Collections.Look(ref stageHistory, "stageHistory", LookMode.Deep);
            Scribe_Collections.Look(ref appliedOutcomeConsumerKeys, "appliedOutcomeConsumerKeys", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                committedOutcomeIds = committedOutcomeIds ?? new List<string>();
                stageHistory = stageHistory ?? new List<HistoricalEventStageRecord>();
                appliedOutcomeConsumerKeys = appliedOutcomeConsumerKeys ?? new List<string>();
            }
        }

        internal void Initialize(string id, HistoricalEventExecutionStatus initialStatus, string reason = null)
        {
            eventId = id;
            status = initialStatus;
            failureReason = reason;
        }

        internal void RecordAttempt(long absoluteDay) => lastAttemptAbsoluteDay = absoluteDay;

        internal HistoricalEventStageRecord EnterStage(HistoricalEventStageEntry stage, HistoricalDate scheduledDate, HistoricalDate transitionDate)
        {
            if (stage == null || HasReachedStage(stage.stageId)) return FindStageRecord(stage?.stageId);
            if (stageHistory == null) stageHistory = new List<HistoricalEventStageRecord>();
            timelineStatus = HistoricalEventRuntimeStatus.Active;
            currentStageId = stage.stageId;
            transitionSequence++;
            HistoricalEventStageRecord record = new HistoricalEventStageRecord();
            record.Initialize(stage.stageId, transitionSequence, scheduledDate, transitionDate);
            stageHistory.Add(record);
            return record;
        }

        internal bool HasReachedStage(string id) => !string.IsNullOrEmpty(id)
            && stageHistory != null && stageHistory.Any(record => record != null && record.StageId == id);
        internal HistoricalEventStageRecord FindStageRecord(string id) => stageHistory?.FirstOrDefault(record => record != null && record.StageId == id);
        internal bool TryCommitOutcome(string outcomeId)
        {
            if (string.IsNullOrWhiteSpace(outcomeId)) return false;
            if (committedOutcomeIds == null) committedOutcomeIds = new List<string>();
            if (committedOutcomeIds.Contains(outcomeId, System.StringComparer.Ordinal)) return false;
            committedOutcomeIds.Add(outcomeId);
            return true;
        }
        internal bool TryMarkOutcomeApplied(string outcomeId, string consumerId)
        {
            if (string.IsNullOrWhiteSpace(outcomeId) || string.IsNullOrWhiteSpace(consumerId)
                || !committedOutcomeIds.Contains(outcomeId, System.StringComparer.Ordinal)) return false;
            if (appliedOutcomeConsumerKeys == null) appliedOutcomeConsumerKeys = new List<string>();
            string key = outcomeId + "|" + consumerId;
            if (appliedOutcomeConsumerKeys.Contains(key, System.StringComparer.Ordinal)) return false;
            appliedOutcomeConsumerKeys.Add(key);
            return true;
        }
        internal bool HasOutcomeBeenApplied(string outcomeId, string consumerId) =>
            !string.IsNullOrWhiteSpace(outcomeId) && !string.IsNullOrWhiteSpace(consumerId)
            && appliedOutcomeConsumerKeys != null
            && appliedOutcomeConsumerKeys.Contains(outcomeId + "|" + consumerId, System.StringComparer.Ordinal);
        internal void SetHistoricalOutcomeCommitted(bool value) => historicalOutcomeCommitted = value;
        internal void CompleteTimeline() => timelineStatus = HistoricalEventRuntimeStatus.Completed;
        internal void SetTimelineStatus(HistoricalEventRuntimeStatus value) => timelineStatus = value;
        internal bool PrepareTimelineCatchUp()
        {
            if (timelineStatus != HistoricalEventRuntimeStatus.NotStarted
                || (status != HistoricalEventExecutionStatus.Executed && status != HistoricalEventExecutionStatus.Skipped)) return false;
            status = HistoricalEventExecutionStatus.Pending;
            failureKind = HistoricalEventFailureKind.None;
            failureReason = "Upgraded save: timeline stages are being reconciled from the current historical date.";
            return true;
        }

        internal void SetPendingDiagnostic(HistoricalEventFailureKind kind, string reason)
        {
            failureKind = kind;
            failureReason = reason;
        }

        internal HistoricalEventExecutionStatus SetTerminalStatus(HistoricalEventExecutionStatus value,
            HistoricalEventFailureKind kind, string reason, int gameTick, HistoricalDate currentDate)
        {
            HistoricalEventExecutionStatus old = status;
            status = value;
            failureKind = kind;
            failureReason = reason;
            if (value == HistoricalEventExecutionStatus.Executed)
            {
                executionGameTick = gameTick;
                executionAstronomicalYear = currentDate.AstronomicalYear;
                executionMonth = currentDate.Month;
                executionDay = currentDate.Day;
            }
            return old;
        }
    }

    public enum HistoricalEventRuntimeStatus { NotStarted, Active, Completed, Disabled, Error }

    public sealed class HistoricalEventStageRecord : IExposable
    {
        private string stageId;
        private long sequence;
        private int scheduledYear;
        private int scheduledMonth;
        private int scheduledDay;
        private int transitionYear;
        private int transitionMonth;
        private int transitionDay;
        private bool chronicleRecorded;
        private bool notificationHandled;
        private bool outcomesProcessed;

        public string StageId => stageId;
        public long Sequence => sequence;
        public HistoricalDate ScheduledDate => new HistoricalDate(scheduledYear, scheduledMonth, scheduledDay);
        public HistoricalDate TransitionDate => new HistoricalDate(transitionYear, transitionMonth, transitionDay);
        public bool ChronicleRecorded => chronicleRecorded;
        public bool NotificationHandled => notificationHandled;
        public bool OutcomesProcessed => outcomesProcessed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref stageId, "stageId"); Scribe_Values.Look(ref sequence, "sequence", 0L);
            Scribe_Values.Look(ref scheduledYear, "scheduledYear"); Scribe_Values.Look(ref scheduledMonth, "scheduledMonth", 1);
            Scribe_Values.Look(ref scheduledDay, "scheduledDay", 1); Scribe_Values.Look(ref transitionYear, "transitionYear");
            Scribe_Values.Look(ref transitionMonth, "transitionMonth", 1); Scribe_Values.Look(ref transitionDay, "transitionDay", 1);
            Scribe_Values.Look(ref chronicleRecorded, "chronicleRecorded", false);
            Scribe_Values.Look(ref notificationHandled, "notificationHandled", false);
            Scribe_Values.Look(ref outcomesProcessed, "outcomesProcessed", false);
        }
        internal void Initialize(string id, long seq, HistoricalDate scheduled, HistoricalDate transition)
        {
            stageId = id; sequence = seq; scheduledYear = scheduled.AstronomicalYear; scheduledMonth = scheduled.Month; scheduledDay = scheduled.Day;
            transitionYear = transition.AstronomicalYear; transitionMonth = transition.Month; transitionDay = transition.Day;
        }
        internal void MarkChronicleRecorded() => chronicleRecorded = true;
        internal void MarkNotificationHandled() => notificationHandled = true;
        internal void MarkOutcomesProcessed() => outcomesProcessed = true;
    }
}
