using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using Verse;

namespace HistoricalRimWorld.Events
{
    /// <summary>Saveable battle orchestration only. Pawn, equipment, wounds and corpses remain in RimWorld's own saves.</summary>
    public sealed class HistoricalBattleInstance : IExposable
    {
        private string battleId;
        private string historicalEventId;
        private HistoricalBattlePhase phase;
        private bool battleStarted;
        private bool battleCompleted;
        private bool resolvedOffMap;
        private bool royalRetreatStarted;
        private bool kingYouDeathScenePlayed;
        private bool kingYouFateExecuted;
        private bool battleOutcomeCommitted;
        private string royalArmyInstanceId;
        private string opposingArmyInstanceId;
        private Map activeMap;
        private int activeMapId = -1;
        private int battleStartTick = -1;
        private int battleStartYear;
        private int battleStartMonth = -1;
        private int battleStartDay = -1;
        private long battleEndAbsoluteDay = long.MinValue;
        private int phaseStartTick = -1;
        private int deathSceneStartTick = -1;
        private int lastOrdersTick = -1;
        private int lastMoraleTick = -1;
        private float royalMorale = 1f;
        private int royalCasualtyCount;
        private int opposingCasualtyCount;
        private List<string> processedTransitions = new List<string>();

        public string BattleId => battleId;
        public string HistoricalEventId => historicalEventId;
        public HistoricalBattlePhase Phase => phase;
        public bool BattleStarted => battleStarted;
        public bool BattleCompleted => battleCompleted;
        public bool ResolvedOffMap => resolvedOffMap;
        public bool RoyalRetreatStarted => royalRetreatStarted;
        public bool KingYouDeathScenePlayed => kingYouDeathScenePlayed;
        public bool KingYouFateExecuted => kingYouFateExecuted;
        public bool BattleOutcomeCommitted => battleOutcomeCommitted;
        public string RoyalArmyInstanceId => royalArmyInstanceId;
        public string OpposingArmyInstanceId => opposingArmyInstanceId;
        public Map ActiveMap => activeMap;
        public int ActiveMapId => activeMapId;
        public int BattleStartTick => battleStartTick;
        public HistoricalDate BattleStartDate => new HistoricalDate(battleStartYear, battleStartMonth, battleStartDay);
        public long BattleEndAbsoluteDay => battleEndAbsoluteDay;
        public int PhaseStartTick => phaseStartTick;
        public int DeathSceneStartTick => deathSceneStartTick;
        public int LastOrdersTick => lastOrdersTick;
        public int LastMoraleTick => lastMoraleTick;
        public float RoyalMorale => royalMorale;
        public int RoyalCasualtyCount => royalCasualtyCount;
        public int OpposingCasualtyCount => opposingCasualtyCount;

        public void ExposeData()
        {
            Scribe_Values.Look(ref battleId, "battleId"); Scribe_Values.Look(ref historicalEventId, "historicalEventId");
            Scribe_Values.Look(ref phase, "phase", HistoricalBattlePhase.NotStarted);
            Scribe_Values.Look(ref battleStarted, "battleStarted", false); Scribe_Values.Look(ref battleCompleted, "battleCompleted", false);
            Scribe_Values.Look(ref resolvedOffMap, "resolvedOffMap", false); Scribe_Values.Look(ref royalRetreatStarted, "royalRetreatStarted", false);
            Scribe_Values.Look(ref kingYouDeathScenePlayed, "kingYouDeathScenePlayed", false); Scribe_Values.Look(ref kingYouFateExecuted, "kingYouFateExecuted", false);
            Scribe_Values.Look(ref battleOutcomeCommitted, "battleOutcomeCommitted", false);
            Scribe_Values.Look(ref royalArmyInstanceId, "royalArmyInstanceId"); Scribe_Values.Look(ref opposingArmyInstanceId, "opposingArmyInstanceId");
            Scribe_References.Look(ref activeMap, "activeMap"); Scribe_Values.Look(ref activeMapId, "activeMapId", -1);
            Scribe_Values.Look(ref battleStartTick, "battleStartTick", -1); Scribe_Values.Look(ref phaseStartTick, "phaseStartTick", -1);
            Scribe_Values.Look(ref battleStartYear, "battleStartYear"); Scribe_Values.Look(ref battleStartMonth, "battleStartMonth", -1);
            Scribe_Values.Look(ref battleStartDay, "battleStartDay", -1);
            Scribe_Values.Look(ref battleEndAbsoluteDay, "battleEndAbsoluteDay", long.MinValue);
            Scribe_Values.Look(ref deathSceneStartTick, "deathSceneStartTick", -1); Scribe_Values.Look(ref lastOrdersTick, "lastOrdersTick", -1);
            Scribe_Values.Look(ref lastMoraleTick, "lastMoraleTick", -1); Scribe_Values.Look(ref royalMorale, "royalMorale", 1f);
            Scribe_Values.Look(ref royalCasualtyCount, "royalCasualtyCount", 0); Scribe_Values.Look(ref opposingCasualtyCount, "opposingCasualtyCount", 0);
            Scribe_Collections.Look(ref processedTransitions, "processedTransitions", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) processedTransitions = processedTransitions ?? new List<string>();
        }

        internal void Initialize(HistoricalBattleDef def)
        {
            battleId = def.battleId; historicalEventId = def.historicalEventId;
            royalArmyInstanceId = def.royalArmyId; opposingArmyInstanceId = def.opposingArmyId;
        }
        internal bool HasTransition(string id) => processedTransitions?.Contains(id) == true;
        internal bool MarkTransition(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (processedTransitions == null) processedTransitions = new List<string>();
            if (processedTransitions.Contains(id)) return false;
            processedTransitions.Add(id); return true;
        }
        internal void SetMap(Map map) { activeMap = map; activeMapId = map?.uniqueID ?? -1; }
        internal bool TryRestoreMap()
        {
            if (activeMap != null && !activeMap.Disposed) return true;
            activeMap = activeMapId < 0 ? null : Find.Maps?.FirstOrDefault(item => item != null && item.uniqueID == activeMapId);
            return activeMap != null && !activeMap.Disposed;
        }
        internal void EnterPhase(HistoricalBattlePhase value, int tick)
        { if (phase == value) return; phase = value; phaseStartTick = tick; }
        internal void StartBattle(int tick)
        {
            battleStarted = true; battleStartTick = tick; EnterPhase(HistoricalBattlePhase.Engagement, tick);
            if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
            { battleStartYear = date.AstronomicalYear; battleStartMonth = date.Month; battleStartDay = date.Day; }
        }
        internal void StartRoyalRetreat(int tick)
        { royalRetreatStarted = true; EnterPhase(HistoricalBattlePhase.RoyalRetreat, tick); }
        internal void StartDeathScene(int tick)
        { if (deathSceneStartTick < 0) deathSceneStartTick = tick; EnterPhase(HistoricalBattlePhase.DeathScene, tick); }
        internal void MarkDeathScenePlayed() => kingYouDeathScenePlayed = true;
        internal void MarkFateExecuted() => kingYouFateExecuted = true;
        internal void MarkOutcomeCommitted() => battleOutcomeCommitted = true;
        internal void ResolveOffMap(int tick)
        { resolvedOffMap = true; battleCompleted = true; battleOutcomeCommitted = true; MarkBattleEndDate(); EnterPhase(HistoricalBattlePhase.Completed, tick); }
        internal void Complete(int tick)
        { battleCompleted = true; MarkBattleEndDate(); EnterPhase(HistoricalBattlePhase.Completed, tick); }
        private void MarkBattleEndDate()
        { if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)) battleEndAbsoluteDay = date.AbsoluteDay; }
        internal void MarkOrders(int tick) => lastOrdersTick = tick;
        internal void MarkMorale(int tick, float value, int royalDead, int oppositionDead)
        { lastMoraleTick = tick; royalMorale = value; royalCasualtyCount = royalDead; opposingCasualtyCount = oppositionDead; }
    }
}
