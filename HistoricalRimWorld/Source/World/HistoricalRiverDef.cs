using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.World
{
    /// <summary>Stable historical identity layered over one or more vanilla river paths.</summary>
    public sealed class HistoricalRiverDef : Def
    {
        public List<string> alternateNames = new List<string>();
        public List<HistoricalRiverNamePeriod> namePeriods = new List<HistoricalRiverNamePeriod>();
        public int preferredLabelCount = 1;
        public float minLabelZoom;
        public float maxLabelZoom = 1f;
        public int labelPriority = 50;
        public Color displayColor = Color.white;
        public HistoricalRiverDef parentRiver;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(label)) yield return "HistoricalRiverDef requires a label.";
            if (preferredLabelCount < 1) yield return "preferredLabelCount must be at least 1.";
            if (minLabelZoom < 0f || maxLabelZoom < minLabelZoom)
                yield return "River label zoom bounds must be non-negative and maxLabelZoom >= minLabelZoom.";
            if (labelPriority < 0) yield return "labelPriority cannot be negative.";
            foreach (HistoricalRiverNamePeriod period in namePeriods ?? Enumerable.Empty<HistoricalRiverNamePeriod>())
            {
                if (period == null) continue;
                if (string.IsNullOrWhiteSpace(period.nameKey)) yield return "A historical river name period requires nameKey.";
                if (period.endYear < period.startYear) yield return "A historical river name period ends before it starts.";
            }
        }
    }

    public sealed class HistoricalRiverNamePeriod
    {
        public int startYear;
        public int endYear;
        public string nameKey;
    }
}
