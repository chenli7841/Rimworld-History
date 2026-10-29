using Verse;

namespace HistoricalRimWorld.Relics
{
    public sealed class CompProperties_HistoricalRelic : CompProperties
    {
        public CompProperties_HistoricalRelic() { compClass = typeof(CompHistoricalRelic); }
    }

    public sealed class CompHistoricalRelic : ThingComp
    {
        private HistoricalRelicThingLink link;
        public string RelicId => link?.RelicId;
        public bool IsLinked => !string.IsNullOrWhiteSpace(RelicId);
        public void SetRelicId(string id) => link = new HistoricalRelicThingLink { RelicId = id };
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref link, "historicalRelicLink");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && link == null) link = new HistoricalRelicThingLink();
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (IsLinked) HistoricalRelicManager.NotifyThingDestroyed(RelicId, parent?.GetUniqueLoadID(), previousMap);
        }
    }
}
