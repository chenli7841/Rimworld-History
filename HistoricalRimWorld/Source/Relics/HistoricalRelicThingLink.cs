using Verse;

namespace HistoricalRimWorld.Relics
{
    public sealed class HistoricalRelicThingLink : IExposable
    {
        public string RelicId;
        public void ExposeData() => Scribe_Values.Look(ref RelicId, "relicId");
    }
}
