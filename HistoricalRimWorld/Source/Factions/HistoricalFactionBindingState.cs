using Verse;
using RimWorld;

namespace HistoricalRimWorld.Factions
{
    public sealed class HistoricalFactionBindingState : IExposable
    {
        public string factionLoadId;
        public string historicalStateId;
        public string factionDefName;

        public void Initialize(Faction faction, string stateId)
        {
            factionLoadId = faction?.GetUniqueLoadID();
            historicalStateId = stateId;
            factionDefName = faction?.def?.defName;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionLoadId, "factionLoadId");
            Scribe_Values.Look(ref historicalStateId, "historicalStateId");
            Scribe_Values.Look(ref factionDefName, "factionDefName");
        }
    }
}
