using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalPoliticalChangeRecord : IExposable
    {
        private string changeId;
        private string targetId;
        private string changeType;
        private string oldValue;
        private string newValue;
        private HistoricalPoliticalChangeSource source;
        private int gameTick;
        private int astronomicalYear;
        private int month;
        private int day;

        public string ChangeId => changeId;
        public string TargetId => targetId;
        public string ChangeType => changeType;
        public string OldValue => oldValue;
        public string NewValue => newValue;
        public HistoricalPoliticalChangeSource Source => source;
        public int GameTick => gameTick;
        public int AstronomicalYear => astronomicalYear;
        public int Month => month;
        public int Day => day;

        public void ExposeData()
        {
            Scribe_Values.Look(ref changeId, "changeId");
            Scribe_Values.Look(ref targetId, "targetId");
            Scribe_Values.Look(ref changeType, "changeType");
            Scribe_Values.Look(ref oldValue, "oldValue");
            Scribe_Values.Look(ref newValue, "newValue");
            Scribe_Values.Look(ref source, "source", HistoricalPoliticalChangeSource.ScenarioInitialization);
            Scribe_Values.Look(ref gameTick, "gameTick", 0);
            Scribe_Values.Look(ref astronomicalYear, "astronomicalYear", 1);
            Scribe_Values.Look(ref month, "month", 1);
            Scribe_Values.Look(ref day, "day", 1);
        }

        internal void Initialize(
            string id,
            string target,
            string type,
            string oldState,
            string newState,
            HistoricalPoliticalChangeSource changeSource,
            int tick,
            int year,
            int changeMonth,
            int changeDay)
        {
            changeId = id;
            targetId = target;
            changeType = type;
            oldValue = oldState;
            newValue = newState;
            source = changeSource;
            gameTick = tick;
            astronomicalYear = year;
            month = changeMonth;
            day = changeDay;
        }
    }
}
