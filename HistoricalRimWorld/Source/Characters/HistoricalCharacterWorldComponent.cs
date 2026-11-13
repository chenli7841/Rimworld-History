using System.Linq;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public sealed class HistoricalCharacterWorldComponent : GameComponent
    {
        private const int DataVersion = 2;
        private int dataVersion;
        private System.Collections.Generic.List<HistoricalCharacterState> characters = new System.Collections.Generic.List<HistoricalCharacterState>();
        private readonly System.Collections.Generic.Dictionary<Pawn, string> pawnIdIndex = new System.Collections.Generic.Dictionary<Pawn, string>();

        public HistoricalCharacterWorldComponent(Game game) { }

        public bool Exists(string historicalCharacterId) => FindState(historicalCharacterId) != null;
        public Pawn GetPawn(string historicalCharacterId) => FindState(historicalCharacterId)?.Pawn;
        public HistoricalCharacterState FindState(string id) => string.IsNullOrWhiteSpace(id)
            ? null : characters?.FirstOrDefault(item => item != null && item.HistoricalCharacterId == id);

        public bool TryGetCharacterState(string id, out HistoricalCharacterState state)
        {
            state = FindState(id);
            return state != null;
        }

        internal HistoricalCharacterState GetOrRegister(HistoricalCharacterDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.historicalCharacterId)) return null;
            if (characters == null) characters = new System.Collections.Generic.List<HistoricalCharacterState>();
            HistoricalCharacterState state = FindState(def.historicalCharacterId);
            if (state != null) return state;
            state = new HistoricalCharacterState();
            state.Initialize(def);
            characters.Add(state);
            return state;
        }

        internal void BindPawn(HistoricalCharacterDef def, Pawn pawn)
        {
            HistoricalCharacterState state = GetOrRegister(def);
            if (state != null && pawn != null)
            {
                state.BindPawn(pawn);
                pawnIdIndex[pawn] = state.HistoricalCharacterId;
            }
        }

        public bool TryGetHistoricalCharacterId(Pawn pawn, out string historicalCharacterId)
        {
            if (pawn != null && pawnIdIndex.TryGetValue(pawn, out historicalCharacterId)) return true;
            historicalCharacterId = null;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "historicalCharacterDataVersion", 0);
            Scribe_Collections.Look(ref characters, "historicalCharacterStates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) dataVersion = DataVersion;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                characters = characters ?? new System.Collections.Generic.List<HistoricalCharacterState>();
                characters.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.HistoricalCharacterId));
                pawnIdIndex.Clear();
                foreach (HistoricalCharacterState state in characters)
                    if (state.Pawn != null) pawnIdIndex[state.Pawn] = state.HistoricalCharacterId;
                if (dataVersion != DataVersion) dataVersion = DataVersion;
            }
        }
    }

    public sealed class HistoricalCharacterState : IExposable
    {
        private string historicalCharacterId;
        private string historicalFactionId;
        private string currentEventId;
        private Pawn pawn;
        private bool generationAttempted;
        private bool historicallyDead;
        private string deathEventId;
        private string deathFateId;

        public string HistoricalCharacterId => historicalCharacterId;
        public string HistoricalFactionId => historicalFactionId;
        public string CurrentEventId => currentEventId;
        public Pawn Pawn => pawn;
        public string CurrentFactionId => pawn?.Faction?.def?.defName ?? historicalFactionId;
        public bool GenerationAttempted => generationAttempted;
        public bool IsHistoricallyDead => historicallyDead;
        public string DeathEventId => deathEventId;
        public string DeathFateId => deathFateId;
        public bool IsGenerated => pawn != null;
        public bool IsOnLocalMap => pawn != null && pawn.Spawned;
        public bool IsDead => pawn != null && pawn.Dead;
        public int CurrentTile => pawn?.Map?.Parent?.Tile ?? -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref historicalCharacterId, "historicalCharacterId");
            Scribe_Values.Look(ref historicalFactionId, "historicalFactionId");
            Scribe_Values.Look(ref currentEventId, "currentEventId");
            Scribe_Values.Look(ref generationAttempted, "generationAttempted", false);
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref historicallyDead, "historicallyDead", false);
            Scribe_Values.Look(ref deathEventId, "deathEventId");
            Scribe_Values.Look(ref deathFateId, "deathFateId");
        }

        internal void Initialize(HistoricalCharacterDef def)
        {
            historicalCharacterId = def.historicalCharacterId;
            historicalFactionId = def.historicalFactionId;
            currentEventId = def.relatedEventId;
        }
        internal void BeginGeneration() => generationAttempted = true;
        internal void ResetGenerationAttemptForRecovery() { if (pawn == null) generationAttempted = false; }
        internal void BindPawn(Pawn value) { pawn = value; generationAttempted = true; }
        internal void MarkHistoricallyDead(string eventId, string fateId)
        {
            historicallyDead = true;
            deathEventId = eventId;
            deathFateId = fateId;
            generationAttempted = true;
        }
    }
}
