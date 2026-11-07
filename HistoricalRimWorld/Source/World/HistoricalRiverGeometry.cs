using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.World
{
    /// <summary>Runtime and saved geometry for one named river; separate segments preserve tributary topology.</summary>
    public sealed class HistoricalRiverGeometry : IExposable
    {
        public string RiverDefName;
        public List<HistoricalRiverSegment> Segments = new List<HistoricalRiverSegment>();
        public List<RiverLabelAnchor> LabelAnchors = new List<RiverLabelAnchor>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref RiverDefName, "riverDefName");
            Scribe_Collections.Look(ref Segments, "segments", LookMode.Deep);
            Scribe_Collections.Look(ref LabelAnchors, "labelAnchors", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Segments = Segments ?? new List<HistoricalRiverSegment>();
                LabelAnchors = LabelAnchors ?? new List<RiverLabelAnchor>();
                Segments.RemoveAll(item => item == null || item.PathTiles == null || item.PathTiles.Count < 2);
                LabelAnchors.RemoveAll(item => item == null || item.TileId < 0);
            }
        }
    }

    public sealed class HistoricalRiverSegment : IExposable
    {
        public string SegmentId;
        public string ParentSegmentId;
        public List<int> PathTiles = new List<int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref SegmentId, "segmentId");
            Scribe_Values.Look(ref ParentSegmentId, "parentSegmentId");
            Scribe_Collections.Look(ref PathTiles, "pathTiles", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) PathTiles = PathTiles ?? new List<int>();
        }
    }

    public sealed class RiverLabelAnchor : IExposable
    {
        public int TileId = -1;
        public int PathIndex = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref TileId, "tileId", -1);
            Scribe_Values.Look(ref PathIndex, "pathIndex", -1);
        }
    }
}
