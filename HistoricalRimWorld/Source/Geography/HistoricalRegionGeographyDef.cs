using System.Collections.Generic;
using System.Linq;
using Verse;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.Geography
{
    public sealed class HistoricalRegionGeographyDef : Def
    {
        public HistoricalRegionDef region;
        public HistoricalBoundaryConfidence confidence = HistoricalBoundaryConfidence.Approximate;
        public List<HistoricalGeoPolygon> polygons = new List<HistoricalGeoPolygon>();
        public List<int> prototypeTileIds = new List<int>();
        public int primaryRegionPriority;
        public string sourceNotes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (region == null) yield return "region is required.";
            if (!System.Enum.IsDefined(typeof(HistoricalBoundaryConfidence), confidence))
                yield return "confidence contains an unknown value.";

            if (polygons == null) yield break;
            for (int index = 0; index < polygons.Count; index++)
            {
                HistoricalGeoPolygon polygon = polygons[index];
                if (polygon == null)
                {
                    yield return "polygons contains a null polygon.";
                    continue;
                }
                foreach (string error in polygon.Validate()) yield return "polygon " + index + ": " + error;
            }

            if (prototypeTileIds != null)
            {
                HashSet<int> seenTiles = new HashSet<int>();
                foreach (int tileId in prototypeTileIds)
                {
                    if (tileId < 0) yield return "prototypeTileIds cannot contain negative values.";
                    if (!seenTiles.Add(tileId)) yield return "prototypeTileIds contains duplicate tile " + tileId + ".";
                }
            }
        }
    }
}
