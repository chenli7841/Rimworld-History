using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    /// <summary>Saveable descriptions for XML-defined map tiles, keyed by generated WorldGrid tile ID.</summary>
    public sealed class HistoricalMapTileDescriptionWorldComponent : WorldComponent
    {
        private List<HistoricalMapTileDescription> descriptions = new List<HistoricalMapTileDescription>();

        public HistoricalMapTileDescriptionWorldComponent(RimWorldWorld world) : base(world) { }

        public IEnumerable<HistoricalMapTileDescription> Descriptions =>
            descriptions ?? Enumerable.Empty<HistoricalMapTileDescription>();

        public string GetDescription(int tileId) => descriptions?
            .FirstOrDefault(item => item != null && item.TileId == tileId)?.Description;

        public void SetDescription(int tileId, string description)
        {
            if (tileId < 0) return;
            if (descriptions == null) descriptions = new List<HistoricalMapTileDescription>();
            HistoricalMapTileDescription existing = descriptions.FirstOrDefault(item => item != null && item.TileId == tileId);
            if (existing != null)
            {
                existing.Description = description ?? string.Empty;
                return;
            }
            descriptions.Add(new HistoricalMapTileDescription(tileId, description ?? string.Empty));
        }

        internal void SetDescriptions(IEnumerable<HistoricalMapTileDescription> values)
        {
            descriptions = values?.Where(item => item != null).ToList()
                ?? new List<HistoricalMapTileDescription>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref descriptions, "historicalMapTileDescriptions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                descriptions = descriptions ?? new List<HistoricalMapTileDescription>();
        }
    }

    public sealed class HistoricalMapTileDescription : IExposable
    {
        public int TileId = -1;
        public string Description;

        public HistoricalMapTileDescription() { }

        public HistoricalMapTileDescription(int tileId, string description)
        {
            TileId = tileId;
            Description = description;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref TileId, "tileId", -1);
            Scribe_Values.Look(ref Description, "description");
        }
    }
}
