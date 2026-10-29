using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Scenarios.EasternZhou
{
    public sealed class HistoricalStartingSuppliesDef : Def
    {
        public List<HistoricalStartingSupply> items;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (items == null || items.Count == 0) yield return "Starting supplies must contain at least one item.";
            if (items == null) yield break;
            foreach (HistoricalStartingSupply item in items)
            {
                if (item == null) { yield return "Starting supplies cannot contain a null entry."; continue; }
                if (item.thingDef == null) yield return "Starting supply requires a ThingDef.";
                if (item.count < 1) yield return "Starting supply count must be positive.";
            }
        }
    }

    public sealed class HistoricalStartingSupply
    {
        public ThingDef thingDef;
        public int count;
        public ThingDef stuffDef;
    }
}
