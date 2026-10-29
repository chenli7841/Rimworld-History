using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Factions
{
    public sealed class HistoricalFactionWorldComponent : WorldComponent
    {
        private string scenarioDefName;
        private HistoricalFactionGenerationMode generationMode;
        private int mappingVersion;
        private bool generationComplete;
        private string generationFailureReason;
        private List<HistoricalFactionBindingState> bindings = new List<HistoricalFactionBindingState>();
        private List<HistoricalSettlementBindingState> settlements = new List<HistoricalSettlementBindingState>();
        private List<HistoricalPrototypeRegionTileBindingState> prototypeRegionTiles = new List<HistoricalPrototypeRegionTileBindingState>();

        public string ScenarioDefName => scenarioDefName;
        public HistoricalFactionGenerationMode GenerationMode => generationMode;
        public int MappingVersion => mappingVersion;
        public bool GenerationComplete => generationComplete;
        public string GenerationFailureReason => generationFailureReason;
        public IEnumerable<HistoricalFactionBindingState> Bindings => bindings ?? Enumerable.Empty<HistoricalFactionBindingState>();
        public IEnumerable<HistoricalSettlementBindingState> Settlements => settlements ?? Enumerable.Empty<HistoricalSettlementBindingState>();
        public IEnumerable<HistoricalPrototypeRegionTileBindingState> PrototypeRegionTiles => prototypeRegionTiles ?? Enumerable.Empty<HistoricalPrototypeRegionTileBindingState>();

        public HistoricalFactionWorldComponent(RimWorldWorld world) : base(world) { }

        internal void Initialize(string scenarioId, int version, IEnumerable<Faction> factions)
        {
            if (!string.IsNullOrEmpty(scenarioDefName) && scenarioDefName != scenarioId)
            {
                Log.Warning("[Historical RimWorld] Historical faction world state belongs to another scenario; saved bindings were preserved.");
                return;
            }

            scenarioDefName = scenarioId;
            generationMode = HistoricalFactionGenerationMode.HistoricalScenario;
            mappingVersion = version;
            generationComplete = false;
            generationFailureReason = null;
            bindings = new List<HistoricalFactionBindingState>();
            foreach (FactionBindingPair pair in HistoricalFactionManager.GeneratedPairs(factions, scenarioId))
            {
                HistoricalFactionBindingState state = new HistoricalFactionBindingState();
                state.Initialize(pair.Faction, pair.StateId);
                bindings.Add(state);
            }
        }

        internal void MarkGenerationResult(bool complete, string failureReason)
        {
            generationComplete = complete;
            generationFailureReason = complete ? null : failureReason;
        }

        internal void RecordSettlement(Settlement settlement, string stateId, string regionId, int tileId)
        {
            if (settlement == null || string.IsNullOrEmpty(stateId) || string.IsNullOrEmpty(regionId)) return;
            if (settlements == null) settlements = new List<HistoricalSettlementBindingState>();
            settlements.RemoveAll(item => item != null && item.worldObjectId == settlement.ID);
            HistoricalSettlementBindingState state = new HistoricalSettlementBindingState();
            state.Initialize(settlement.ID, stateId, regionId, tileId);
            settlements.Add(state);
        }

        internal void RecordRegionTile(string stateId, string regionId, int tileId)
        {
            if (string.IsNullOrEmpty(stateId) || string.IsNullOrEmpty(regionId) || tileId < 0) return;
            if (prototypeRegionTiles == null) prototypeRegionTiles = new List<HistoricalPrototypeRegionTileBindingState>();
            if (prototypeRegionTiles.Any(item => item != null && item.tileId == tileId)) return;
            HistoricalPrototypeRegionTileBindingState state = new HistoricalPrototypeRegionTileBindingState();
            state.Initialize(stateId, regionId, tileId);
            prototypeRegionTiles.Add(state);
        }

        public bool IsTileInRegion(int tileId, string regionId) => prototypeRegionTiles != null
            && prototypeRegionTiles.Any(item => item != null && item.tileId == tileId && item.regionId == regionId);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalFactionScenarioDefName");
            Scribe_Values.Look(ref generationMode, "historicalFactionGenerationMode", HistoricalFactionGenerationMode.Vanilla);
            Scribe_Values.Look(ref mappingVersion, "historicalFactionMappingVersion", 0);
            Scribe_Values.Look(ref generationComplete, "historicalFactionGenerationComplete", false);
            Scribe_Values.Look(ref generationFailureReason, "historicalFactionGenerationFailureReason");
            Scribe_Collections.Look(ref bindings, "historicalFactionBindings", LookMode.Deep);
            Scribe_Collections.Look(ref settlements, "historicalFactionSettlements", LookMode.Deep);
            Scribe_Collections.Look(ref prototypeRegionTiles, "historicalPrototypeRegionTiles", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bindings = bindings ?? new List<HistoricalFactionBindingState>();
                settlements = settlements ?? new List<HistoricalSettlementBindingState>();
                prototypeRegionTiles = prototypeRegionTiles ?? new List<HistoricalPrototypeRegionTileBindingState>();
                if (generationMode == HistoricalFactionGenerationMode.HistoricalScenario
                    && DefDatabase<HistoricalScenarioDef>.GetNamedSilentFail(scenarioDefName) == null)
                    Log.Warning("[Historical RimWorld] Historical faction bindings reference missing scenario " + scenarioDefName + "; saved IDs were preserved.");
            }
        }
    }

    internal sealed class FactionBindingPair
    {
        public Faction Faction { get; }
        public string StateId { get; }

        public FactionBindingPair(Faction faction, string stateId)
        {
            Faction = faction;
            StateId = stateId;
        }
    }
}
