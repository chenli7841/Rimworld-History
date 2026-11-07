using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public sealed class HistoricalTechnologyTimelineLayoutDef : Def
    {
        public List<HistoricalTimelineSegment> segments;
        public float undatedX = 14.5f;
        public float categorySpacing = 2.0f;
        public float laneSpacing = 0.85f;
        public float minimumNodeGap = 1.25f;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (segments == null || segments.Count == 0)
            {
                yield return "A historical timeline layout requires at least one segment.";
                yield break;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                HistoricalTimelineSegment segment = segments[i];
                if (segment == null)
                {
                    yield return "Timeline segment " + i + " is null.";
                    continue;
                }
                if (segment.endAstronomicalYear <= segment.startAstronomicalYear)
                    yield return "Timeline segment " + i + " must have an increasing year range.";
                if (segment.xEnd <= segment.xStart)
                    yield return "Timeline segment " + i + " must have an increasing X range.";
                if (i > 0 && (segments[i - 1].endAstronomicalYear != segment.startAstronomicalYear
                    || segments[i - 1].xEnd != segment.xStart))
                    yield return "Timeline segments must be contiguous in year and X coordinates.";
            }

            if (categorySpacing <= 0f || laneSpacing <= 0f || minimumNodeGap <= 0f)
                yield return "Timeline category spacing, lane spacing, and node gap must be positive.";
            if (segments.Any(segment => segment != null && segment.xEnd > undatedX))
                yield return "undatedX must be to the right of the dated timeline.";
        }
    }

    public sealed class HistoricalTimelineSegment
    {
        public int startAstronomicalYear;
        public int endAstronomicalYear;
        public float xStart;
        public float xEnd;
    }
}
