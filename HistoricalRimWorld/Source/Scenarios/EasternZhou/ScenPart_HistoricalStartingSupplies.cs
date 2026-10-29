using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Scenarios.EasternZhou
{
    public sealed class ScenPart_HistoricalStartingSupplies : ScenPart
    {
        public HistoricalStartingSuppliesDef supplies;

        public override IEnumerable<Thing> PlayerStartingThings()
        {
            if (supplies?.items == null) yield break;
            foreach (HistoricalStartingSupply item in supplies.items.Where(entry => entry?.thingDef != null && entry.count > 0))
            {
                int remaining = item.count;
                while (remaining > 0)
                {
                    Thing thing = ThingMaker.MakeThing(item.thingDef, item.stuffDef);
                    int stackCount = System.Math.Min(remaining, thing.def.stackLimit);
                    thing.stackCount = stackCount;
                    remaining -= stackCount;
                    yield return thing;
                }
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (supplies == null) yield return "Historical starting-supplies scenario part requires a supplies Def.";
        }
    }
}
