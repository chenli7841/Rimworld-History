using Verse;
using RimWorld;
using HistoricalRimWorld.Technology;

namespace HistoricalRimWorld.Production
{
    public sealed class HistoricalTechnologyRequirementExtension : DefModExtension
    {
        public string requiredTechnology;
        public HistoricalContentPolicy policy = HistoricalContentPolicy.Restricted;
    }
}
