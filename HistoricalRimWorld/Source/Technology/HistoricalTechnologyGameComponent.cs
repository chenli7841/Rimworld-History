using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public sealed class HistoricalTechnologyGameComponent : GameComponent
    {
        private string scenarioDefName;
        private bool initialized;
        private List<HistoricalCommunityTechnologyState> communities = new List<HistoricalCommunityTechnologyState>();

        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public IEnumerable<HistoricalCommunityTechnologyState> Communities => communities ?? Enumerable.Empty<HistoricalCommunityTechnologyState>();

        public HistoricalTechnologyGameComponent(Game game) { }

        public bool Initialize(string scenarioId, string playerCommunityId, IEnumerable<HistoricalTechnologyInitialState> initial)
        {
            if (initialized) return string.Equals(scenarioDefName, scenarioId, StringComparison.Ordinal);
            scenarioDefName = scenarioId;
            communities = communities ?? new List<HistoricalCommunityTechnologyState>();
            HistoricalCommunityTechnologyState community = new HistoricalCommunityTechnologyState();
            community.Initialize(playerCommunityId, initial);
            communities.Add(community);
            initialized = true;
            return true;
        }

        public HistoricalTechnologyState Find(string communityId, string technologyId)
        {
            HistoricalCommunityTechnologyState community = communities?.FirstOrDefault(item => item.CommunityId == communityId);
            return community?.Find(technologyId);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalTechnologyScenarioDefName");
            Scribe_Values.Look(ref initialized, "historicalTechnologyInitialized", false);
            Scribe_Collections.Look(ref communities, "historicalTechnologyCommunities", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) communities = communities ?? new List<HistoricalCommunityTechnologyState>();
        }
    }

    public sealed class HistoricalCommunityTechnologyState : IExposable
    {
        private string communityId;
        private List<HistoricalTechnologyState> technologies = new List<HistoricalTechnologyState>();
        public string CommunityId => communityId;
        public IEnumerable<HistoricalTechnologyState> Technologies => technologies ?? Enumerable.Empty<HistoricalTechnologyState>();
        public HistoricalTechnologyState Find(string id) => technologies?.FirstOrDefault(item => item != null && item.TechnologyId == id);
        public void Add(HistoricalTechnologyState state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.TechnologyId) || Find(state.TechnologyId) != null) return;
            technologies = technologies ?? new List<HistoricalTechnologyState>();
            technologies.Add(state);
        }

        public void Initialize(string id, IEnumerable<HistoricalTechnologyInitialState> initial)
        {
            communityId = id;
            technologies = new List<HistoricalTechnologyState>();
            if (initial == null) return;
            foreach (HistoricalTechnologyInitialState item in initial)
            {
                if (item?.technology == null || Find(item.technology.defName) != null) continue;
                technologies.Add(new HistoricalTechnologyState { TechnologyId = item.technology.defName, Availability = item.availability, Adoption = item.adoption });
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref communityId, "communityId");
            Scribe_Collections.Look(ref technologies, "technologies", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) technologies = technologies ?? new List<HistoricalTechnologyState>();
        }
    }
}
