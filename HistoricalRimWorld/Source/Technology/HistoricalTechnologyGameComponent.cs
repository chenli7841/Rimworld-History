using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Patches.Technology;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public sealed class HistoricalTechnologyGameComponent : GameComponent
    {
        private string scenarioDefName;
        private bool initialized;
        private List<HistoricalCommunityTechnologyState> communities = new List<HistoricalCommunityTechnologyState>();
        private List<HistoricalFactionTechnologyState> factionStates = new List<HistoricalFactionTechnologyState>();
        private List<PlayerHistoricalTechAwarenessState> playerAwareness = new List<PlayerHistoricalTechAwarenessState>();
        private List<HistoricalTechnologyScheduleState> scheduleStates = new List<HistoricalTechnologyScheduleState>();
        private List<HistoricalResearchAuthorization> researchAuthorizations = new List<HistoricalResearchAuthorization>();
        private long lastProcessedAbsoluteDay = long.MinValue;
        private int dataVersion;

        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public IEnumerable<HistoricalCommunityTechnologyState> Communities => communities ?? Enumerable.Empty<HistoricalCommunityTechnologyState>();
        public IEnumerable<HistoricalFactionTechnologyState> FactionStates => factionStates ?? Enumerable.Empty<HistoricalFactionTechnologyState>();
        public IEnumerable<PlayerHistoricalTechAwarenessState> PlayerAwareness => playerAwareness ?? Enumerable.Empty<PlayerHistoricalTechAwarenessState>();
        public IEnumerable<HistoricalTechnologyScheduleState> ScheduleStates => scheduleStates ?? Enumerable.Empty<HistoricalTechnologyScheduleState>();
        public long LastProcessedAbsoluteDay => lastProcessedAbsoluteDay;
        public IEnumerable<HistoricalResearchAuthorization> ResearchAuthorizations => researchAuthorizations ?? Enumerable.Empty<HistoricalResearchAuthorization>();

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

        public HistoricalFactionTechnologyState FindFactionState(string stableFactionId) =>
            factionStates?.FirstOrDefault(item => item != null && item.StableFactionId == stableFactionId);

        public HistoricalFactionTechRecord FindFactionTech(string stableFactionId, string researchDefName) =>
            FindFactionState(stableFactionId)?.Find(researchDefName);

        internal HistoricalFactionTechnologyState GetOrAddFactionState(string stableId, string historicalStateId,
            string factionDefName, bool isPlayer, IEnumerable<HistoricalFactionTechRecord> records = null)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            HistoricalFactionTechnologyState state = FindFactionState(stableId);
            if (state != null) return state;
            state = new HistoricalFactionTechnologyState();
            state.Initialize(stableId, historicalStateId, factionDefName, isPlayer, records);
            if (factionStates == null) factionStates = new List<HistoricalFactionTechnologyState>();
            factionStates.Add(state);
            return state;
        }

        public PlayerHistoricalTechAwarenessState FindAwareness(string researchDefName) =>
            playerAwareness?.FirstOrDefault(item => item != null && item.ResearchDefName == researchDefName);

        internal PlayerHistoricalTechAwarenessState GetOrAddAwareness(string researchDefName)
        {
            if (string.IsNullOrWhiteSpace(researchDefName)) return null;
            PlayerHistoricalTechAwarenessState state = FindAwareness(researchDefName);
            if (state != null) return state;
            state = new PlayerHistoricalTechAwarenessState { ResearchDefName = researchDefName };
            if (playerAwareness == null) playerAwareness = new List<PlayerHistoricalTechAwarenessState>();
            playerAwareness.Add(state);
            return state;
        }

        internal HistoricalTechnologyScheduleState GetOrAddSchedule(string researchDefName)
        {
            if (string.IsNullOrWhiteSpace(researchDefName)) return null;
            HistoricalTechnologyScheduleState state = scheduleStates?.FirstOrDefault(item => item != null && item.ResearchDefName == researchDefName);
            if (state != null) return state;
            state = new HistoricalTechnologyScheduleState { ResearchDefName = researchDefName };
            if (scheduleStates == null) scheduleStates = new List<HistoricalTechnologyScheduleState>();
            scheduleStates.Add(state);
            return state;
        }

        internal void MarkDayProcessed(long absoluteDay) => lastProcessedAbsoluteDay = absoluteDay;

        public HistoricalResearchAuthorization FindAuthorization(string researchDefName) =>
            researchAuthorizations?.FirstOrDefault(item => item != null && item.ResearchDefName == researchDefName && item.IsAuthorized);

        internal bool GrantAuthorization(string researchDefName, HistoricalResearchAuthorizationSource source, string sourceDetails)
        {
            if (string.IsNullOrWhiteSpace(researchDefName)) return false;
            researchAuthorizations = researchAuthorizations ?? new List<HistoricalResearchAuthorization>();
            HistoricalResearchAuthorization record = researchAuthorizations.FirstOrDefault(item => item != null && item.ResearchDefName == researchDefName);
            if (record != null && record.IsAuthorized) return false;
            if (record == null)
            {
                record = new HistoricalResearchAuthorization { ResearchDefName = researchDefName };
                researchAuthorizations.Add(record);
            }
            record.Grant(source, sourceDetails);
            return true;
        }

        internal bool RevokeAuthorization(string researchDefName)
        {
            HistoricalResearchAuthorization record = researchAuthorizations?.FirstOrDefault(item => item != null && item.ResearchDefName == researchDefName);
            if (record == null || !record.IsAuthorized) return false;
            record.IsAuthorized = false;
            return true;
        }

        internal void InitializeScheduler(long currentAbsoluteDay)
        {
            if (lastProcessedAbsoluteDay == long.MinValue) lastProcessedAbsoluteDay = currentAbsoluteDay;
            dataVersion = Math.Max(dataVersion, 1);
            foreach (ResearchProjectDef project in HistoricalTechnologyTimelineLayout.HistoricalProjects)
            {
                HistoricalTechnologyScheduleState schedule = GetOrAddSchedule(project.defName);
                if (schedule != null && schedule.LastDiffusionAbsoluteDay == long.MinValue)
                    schedule.LastDiffusionAbsoluteDay = currentAbsoluteDay;
            }
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int tick = Verse.Find.TickManager?.TicksGame ?? -1;
            if (!initialized || tick < 0 || tick % 250 != 0 || !HistoricalScenarioManager.IsScenarioActive) return;
            if (tick % 6000 == 0) HistoricalBlueprintService.ScanPlayerBlueprintAcquisitions();
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date) || date.AbsoluteDay <= lastProcessedAbsoluteDay) return;
            HistoricalTechnologyDiffusionService.ProcessDay(date);
            lastProcessedAbsoluteDay = date.AbsoluteDay;
            HistoricalResearchTimelineUIState.Invalidate();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalTechnologyScenarioDefName");
            Scribe_Values.Look(ref initialized, "historicalTechnologyInitialized", false);
            Scribe_Collections.Look(ref communities, "historicalTechnologyCommunities", LookMode.Deep);
            Scribe_Collections.Look(ref factionStates, "historicalTechnologyFactionStates", LookMode.Deep);
            Scribe_Collections.Look(ref playerAwareness, "historicalTechnologyPlayerAwareness", LookMode.Deep);
            Scribe_Collections.Look(ref scheduleStates, "historicalTechnologyScheduleStates", LookMode.Deep);
            Scribe_Collections.Look(ref researchAuthorizations, "historicalResearchAuthorizations", LookMode.Deep);
            Scribe_Values.Look(ref lastProcessedAbsoluteDay, "historicalTechnologyLastProcessedAbsoluteDay", long.MinValue);
            Scribe_Values.Look(ref dataVersion, "historicalTechnologyDataVersion", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                communities = communities ?? new List<HistoricalCommunityTechnologyState>();
                factionStates = factionStates ?? new List<HistoricalFactionTechnologyState>();
                playerAwareness = playerAwareness ?? new List<PlayerHistoricalTechAwarenessState>();
                scheduleStates = scheduleStates ?? new List<HistoricalTechnologyScheduleState>();
                researchAuthorizations = researchAuthorizations ?? new List<HistoricalResearchAuthorization>();
                factionStates.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.StableFactionId));
                playerAwareness.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.ResearchDefName));
                scheduleStates.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.ResearchDefName));
                researchAuthorizations.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.ResearchDefName));
                if (dataVersion < 1)
                {
                    // Phase 1 saves had only player-community research records; Phase 2 initializes world records once.
                    dataVersion = 1;
                }
            }
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
