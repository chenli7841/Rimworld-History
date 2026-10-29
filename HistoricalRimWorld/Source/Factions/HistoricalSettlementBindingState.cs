using Verse;

namespace HistoricalRimWorld.Factions
{
    public sealed class HistoricalSettlementBindingState : IExposable
    {
        public int worldObjectId;
        public string polityId;
        public string regionId;
        public int tileId = -1;

        public void Initialize(int id, string stateId, string historicalRegionId, int tile)
        {
            worldObjectId = id;
            polityId = stateId;
            regionId = historicalRegionId;
            tileId = tile;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref worldObjectId, "worldObjectId");
            Scribe_Values.Look(ref polityId, "polityId");
            Scribe_Values.Look(ref regionId, "regionId");
            Scribe_Values.Look(ref tileId, "tileId", -1);
        }
    }
}
