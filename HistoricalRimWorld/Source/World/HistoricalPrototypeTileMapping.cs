using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.World
{
    public sealed class HistoricalPrototypeTileMapping : IExposable
    {
        private int tileId = -1;
        private List<string> regionIds = new List<string>();

        public int TileId => tileId;
        public IReadOnlyList<string> RegionIds => regionIds;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tileId, "tileId", -1);
            Scribe_Collections.Look(ref regionIds, "regionIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                regionIds = regionIds ?? new List<string>();
            }
        }

        internal void Initialize(int id, IEnumerable<string> regions)
        {
            tileId = id;
            regionIds = regions == null ? new List<string>() : new List<string>(regions);
        }

        internal bool AddRegion(string regionId)
        {
            if (regionIds == null) regionIds = new List<string>();
            if (regionIds.Contains(regionId)) return false;
            regionIds.Add(regionId);
            return true;
        }

        internal bool RemoveRegion(string regionId)
        {
            return regionIds != null && regionIds.Remove(regionId);
        }
    }
}
