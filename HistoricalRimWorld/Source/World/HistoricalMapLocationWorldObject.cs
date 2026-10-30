using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.World
{
    /// <summary>Lightweight, saveable named world-map marker with its icon supplied by WorldObjectDef.</summary>
    public sealed class HistoricalMapLocationWorldObject : WorldObject
    {
        private string historicalLabel;
        private string historicalDescription;
        private Color historicalFactionColor = Color.white;
        private bool hasHistoricalFactionColor;

        public override string Label => string.IsNullOrWhiteSpace(historicalLabel) ? base.Label : historicalLabel;
        public override string LabelShort => Label;
        public override bool HasName => !string.IsNullOrWhiteSpace(historicalLabel) || base.HasName;
        public override Color ExpandingIconColor => hasHistoricalFactionColor
            ? historicalFactionColor
            : base.ExpandingIconColor;

        public void SetHistoricalLabel(string label)
        {
            historicalLabel = label;
        }

        public void SetHistoricalDescription(string description)
        {
            historicalDescription = description;
        }

        public void SetHistoricalFactionColor(Color color)
        {
            historicalFactionColor = color;
            hasHistoricalFactionColor = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref historicalLabel, "historicalLabel");
            Scribe_Values.Look(ref historicalDescription, "historicalDescription");
            Scribe_Values.Look(ref historicalFactionColor, "historicalFactionColor", Color.white);
            Scribe_Values.Look(ref hasHistoricalFactionColor, "hasHistoricalFactionColor", false);
        }

        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();
            if (string.IsNullOrWhiteSpace(historicalDescription)) return baseString;
            return string.IsNullOrWhiteSpace(baseString)
                ? historicalDescription
                : baseString + "\n" + historicalDescription;
        }

        public override string GetDescription() => string.IsNullOrWhiteSpace(historicalDescription)
            ? base.GetDescription()
            : historicalDescription;
    }
}
