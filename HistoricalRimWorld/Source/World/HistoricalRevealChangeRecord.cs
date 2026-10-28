using Verse;

namespace HistoricalRimWorld.World
{
    public sealed class HistoricalRevealChangeRecord : IExposable
    {
        private string changeId;
        private string regionId;
        private HistoricalRegionAccess oldAccess;
        private HistoricalRegionAccess newAccess;
        private HistoricalRevealSource source;
        private int gameTick;
        private bool oldEverVisited;
        private bool newEverVisited;

        public string ChangeId => changeId;
        public string RegionId => regionId;
        public HistoricalRegionAccess OldAccess => oldAccess;
        public HistoricalRegionAccess NewAccess => newAccess;
        public HistoricalRevealSource Source => source;
        public int GameTick => gameTick;
        public bool OldEverVisited => oldEverVisited;
        public bool NewEverVisited => newEverVisited;

        public void ExposeData()
        {
            Scribe_Values.Look(ref changeId, "changeId");
            Scribe_Values.Look(ref regionId, "regionId");
            Scribe_Values.Look(ref oldAccess, "oldAccess", HistoricalRegionAccess.Locked);
            Scribe_Values.Look(ref newAccess, "newAccess", HistoricalRegionAccess.Locked);
            Scribe_Values.Look(ref source, "source", HistoricalRevealSource.ScenarioInitialization);
            Scribe_Values.Look(ref gameTick, "gameTick", 0);
            Scribe_Values.Look(ref oldEverVisited, "oldEverVisited", false);
            Scribe_Values.Look(ref newEverVisited, "newEverVisited", false);
        }

        internal void Initialize(string id, string region, HistoricalRegionAccess oldState,
            HistoricalRegionAccess newState, HistoricalRevealSource changeSource, int tick,
            bool wasVisited, bool isVisited)
        {
            changeId = id;
            regionId = region;
            oldAccess = oldState;
            newAccess = newState;
            source = changeSource;
            gameTick = tick;
            oldEverVisited = wasVisited;
            newEverVisited = isVisited;
        }
    }
}
