using Verse;

namespace HistoricalRimWorld.World
{
    public sealed class HistoricalRegionRevealState : IExposable
    {
        private string regionId;
        private HistoricalRegionAccess access;
        private bool everVisited;
        private int firstRevealedGameTick = -1;

        public string RegionId => regionId;
        public HistoricalRegionAccess Access => access;
        public bool EverVisited => everVisited;
        public int? FirstRevealedGameTick => firstRevealedGameTick < 0 ? (int?)null : firstRevealedGameTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref regionId, "regionId");
            Scribe_Values.Look(ref access, "access", HistoricalRegionAccess.Locked);
            Scribe_Values.Look(ref everVisited, "everVisited", false);
            Scribe_Values.Look(ref firstRevealedGameTick, "firstRevealedGameTick", -1);
        }

        internal void Initialize(string id, HistoricalRegionAccess initialAccess)
        {
            regionId = id;
            access = initialAccess;
        }

        internal bool SetAccess(HistoricalRegionAccess value, int gameTick, bool visited)
        {
            if (access == value && (!visited || everVisited)) return false;
            access = value;
            if (value == HistoricalRegionAccess.Revealed && firstRevealedGameTick < 0)
            {
                firstRevealedGameTick = gameTick;
            }
            if (visited) everVisited = true;
            return true;
        }
    }
}
