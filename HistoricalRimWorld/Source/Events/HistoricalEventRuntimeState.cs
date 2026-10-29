using HistoricalRimWorld.Calendar;
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

        public string EventId => eventId;
        public HistoricalEventExecutionStatus Status => status;
        public int? ExecutionGameTick => executionGameTick < 0 ? (int?)null : executionGameTick;
        public int? ExecutionAstronomicalYear => executionGameTick < 0 ? (int?)null : executionAstronomicalYear;
        public int? ExecutionMonth => executionMonth < 0 ? (int?)null : executionMonth;
        public int? ExecutionDay => executionDay < 0 ? (int?)null : executionDay;
        public HistoricalEventFailureKind FailureKind => failureKind;
        public string FailureReason => failureReason;
        public long LastAttemptAbsoluteDay => lastAttemptAbsoluteDay;

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
        }

        internal void Initialize(string id, HistoricalEventExecutionStatus initialStatus, string reason = null)
        {
            eventId = id;
            status = initialStatus;
            failureReason = reason;
        }

        internal void RecordAttempt(long absoluteDay) => lastAttemptAbsoluteDay = absoluteDay;

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
}
