using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalArmyRuntime : IExposable
    {
        private string armyId;
        private string eventId;
        private string factionId;
        private bool generationStarted;
        private bool deploymentComplete;
        private int mapId = -1;
        private Map map;
        private List<HistoricalArmyMemberState> members = new List<HistoricalArmyMemberState>();

        public string ArmyId => armyId;
        public string EventId => eventId;
        public string FactionId => factionId;
        public bool GenerationStarted => generationStarted;
        public bool DeploymentComplete => deploymentComplete;
        public Map Map => map;
        public IEnumerable<HistoricalArmyMemberState> Members => members ?? Enumerable.Empty<HistoricalArmyMemberState>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref armyId, "armyId");
            Scribe_Values.Look(ref eventId, "eventId");
            Scribe_Values.Look(ref factionId, "factionId");
            Scribe_Values.Look(ref generationStarted, "generationStarted", false);
            Scribe_Values.Look(ref deploymentComplete, "deploymentComplete", false);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref members, "members", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) members = members ?? new List<HistoricalArmyMemberState>();
        }

        internal void Initialize(string id, string eventDefId, string factionDefId)
        {
            armyId = id; eventId = eventDefId; factionId = factionDefId;
        }
        internal HistoricalArmyMemberState GetOrCreateMember(string id, string groupId, string role, string characterId = null)
        {
            if (members == null) members = new List<HistoricalArmyMemberState>();
            HistoricalArmyMemberState existing = members.FirstOrDefault(item => item != null && item.MemberId == id);
            if (existing != null) return existing;
            var state = new HistoricalArmyMemberState(); state.Initialize(id, groupId, role, characterId); members.Add(state); return state;
        }
        internal void SetGenerationStarted() => generationStarted = true;
        internal void SetMap(Map value) { map = value; mapId = value?.uniqueID ?? -1; }
        internal void SetDeploymentComplete() => deploymentComplete = true;
    }

    public sealed class HistoricalArmyMemberState : IExposable
    {
        private string memberId;
        private string groupId;
        private string unitRole;
        private string historicalCharacterId;
        private bool generationAttempted;
        private Pawn pawn;

        public string MemberId => memberId;
        public string GroupId => groupId;
        public string UnitRole => unitRole;
        public string HistoricalCharacterId => historicalCharacterId;
        public bool GenerationAttempted => generationAttempted;
        public Pawn Pawn => pawn;

        public void ExposeData()
        {
            Scribe_Values.Look(ref memberId, "memberId"); Scribe_Values.Look(ref groupId, "groupId");
            Scribe_Values.Look(ref unitRole, "unitRole"); Scribe_Values.Look(ref historicalCharacterId, "historicalCharacterId");
            Scribe_Values.Look(ref generationAttempted, "generationAttempted", false);
            Scribe_References.Look(ref pawn, "pawn");
        }
        internal void Initialize(string id, string group, string role, string characterId)
        { memberId = id; groupId = group; unitRole = role; historicalCharacterId = characterId; }
        internal void BeginGeneration() => generationAttempted = true;
        internal void ResetFailedGeneration() { if (pawn == null) generationAttempted = false; }
        internal void BindPawn(Pawn value) { pawn = value; generationAttempted = true; }
    }
}
