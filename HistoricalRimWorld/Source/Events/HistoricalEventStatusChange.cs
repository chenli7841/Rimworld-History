using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventStatusChange : IExposable
    {
        private string eventId;
        private HistoricalEventExecutionStatus from;
        private HistoricalEventExecutionStatus to;
        private int gameTick;
        private long absoluteDay;
        private string reason;

        public string EventId => eventId;
        public HistoricalEventExecutionStatus From => from;
        public HistoricalEventExecutionStatus To => to;
        public int GameTick => gameTick;
        public long AbsoluteDay => absoluteDay;
        public string Reason => reason;

        public void ExposeData()
        {
            Scribe_Values.Look(ref eventId, "eventId");
            Scribe_Values.Look(ref from, "from", HistoricalEventExecutionStatus.Pending);
            Scribe_Values.Look(ref to, "to", HistoricalEventExecutionStatus.Pending);
            Scribe_Values.Look(ref gameTick, "gameTick", -1);
            Scribe_Values.Look(ref absoluteDay, "absoluteDay");
            Scribe_Values.Look(ref reason, "reason");
        }

        internal void Initialize(string id, HistoricalEventExecutionStatus oldStatus,
            HistoricalEventExecutionStatus newStatus, int tick, long day, string failureReason)
        {
            eventId = id; from = oldStatus; to = newStatus; gameTick = tick; absoluteDay = day; reason = failureReason;
        }
    }
}
