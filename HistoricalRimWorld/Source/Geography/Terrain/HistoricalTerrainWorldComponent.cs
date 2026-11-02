using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Geography.Terrain
{
    public sealed class HistoricalTerrainWorldComponent : WorldComponent
    {
        private string datasetDefName;
        private string sourceName;
        private string sourceVersion;
        private int datasetVersion;
        private List<HistoricalTerrainTileState> tiles = new List<HistoricalTerrainTileState>();

        public string DatasetDefName => datasetDefName;
        public string SourceName => sourceName;
        public string SourceVersion => sourceVersion;
        public int DatasetVersion => datasetVersion;
        public IEnumerable<HistoricalTerrainTileState> Tiles => tiles ?? Enumerable.Empty<HistoricalTerrainTileState>();

        public HistoricalTerrainWorldComponent(RimWorldWorld world) : base(world) { }

        public HistoricalTerrainTileState GetTile(int tileId) =>
            tiles?.FirstOrDefault(item => item != null && item.TileId == tileId);

        internal void SetDataset(string defName, int version, string datasetSourceName, string datasetSourceVersion)
        {
            datasetDefName = defName;
            sourceName = datasetSourceName;
            sourceVersion = datasetSourceVersion;
            datasetVersion = version;
            tiles = new List<HistoricalTerrainTileState>();
        }

        internal void AddTile(HistoricalTerrainTileState state)
        {
            if (state == null) return;
            if (tiles == null) tiles = new List<HistoricalTerrainTileState>();
            tiles.Add(state);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref datasetDefName, "historicalTerrainDatasetDefName");
            Scribe_Values.Look(ref sourceName, "historicalTerrainSourceName");
            Scribe_Values.Look(ref sourceVersion, "historicalTerrainSourceVersion");
            Scribe_Values.Look(ref datasetVersion, "historicalTerrainDatasetVersion", 0);
            Scribe_Collections.Look(ref tiles, "historicalTerrainTiles", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                tiles = tiles ?? new List<HistoricalTerrainTileState>();
        }
    }
}
