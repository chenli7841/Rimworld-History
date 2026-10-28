using System.Collections.Generic;

namespace HistoricalRimWorld.Politics
{
    internal sealed class HistoricalPoliticsInitializationPlan
    {
        public string ScenarioDefName { get; private set; }
        public string SetupDefName { get; private set; }
        public List<HistoricalPolityState> Polities { get; private set; }
        public List<HistoricalRegionPoliticalState> Regions { get; private set; }
        public List<HistoricalDomainState> Domains { get; private set; }
        public List<HistoricalPoliticalRelation> Relations { get; private set; }
        public List<HistoricalLocalCommunityState> Communities { get; private set; }

        public HistoricalPoliticsInitializationPlan(
            string scenarioDefName,
            string setupDefName,
            List<HistoricalPolityState> polities,
            List<HistoricalRegionPoliticalState> regions,
            List<HistoricalDomainState> domains,
            List<HistoricalPoliticalRelation> relations,
            List<HistoricalLocalCommunityState> communities)
        {
            ScenarioDefName = scenarioDefName;
            SetupDefName = setupDefName;
            Polities = polities;
            Regions = regions;
            Domains = domains;
            Relations = relations;
            Communities = communities;
        }
    }
}
