using RimWorld;
using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalFactionMapping : IExposable
    {
        private string historicalPolityId;
        private Faction faction;

        public string HistoricalPolityId => historicalPolityId;
        public Faction Faction => faction;

        public void ExposeData()
        {
            Scribe_Values.Look(ref historicalPolityId, "historicalPolityId");
            // This is a reference to a Faction in the current save only, not a cross-save ID.
            Scribe_References.Look(ref faction, "faction");
        }

        internal void Initialize(string polityId, Faction mappedFaction)
        {
            historicalPolityId = polityId;
            faction = mappedFaction;
        }
    }
}
