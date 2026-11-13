using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalBattleGameComponent : GameComponent
    {
        private const int DataVersion = 1;
        private int dataVersion;
        private List<HistoricalBattleInstance> battles = new List<HistoricalBattleInstance>();

        public HistoricalBattleGameComponent(Game game) { }
        public HistoricalBattleInstance FindBattle(string battleId) => string.IsNullOrWhiteSpace(battleId)
            ? null : battles?.FirstOrDefault(item => item != null && item.BattleId == battleId);

        internal HistoricalBattleInstance GetOrCreate(HistoricalBattleDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.battleId)) return null;
            if (battles == null) battles = new List<HistoricalBattleInstance>();
            HistoricalBattleInstance item = FindBattle(def.battleId);
            if (item != null) return item;
            item = new HistoricalBattleInstance(); item.Initialize(def); battles.Add(item); return item;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.TickManager.TicksGame % 60 != 0) return;
            HistoricalBattleTimelineBridge.Tick(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "historicalBattleDataVersion", 0);
            Scribe_Collections.Look(ref battles, "historicalBattleInstances", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) dataVersion = DataVersion;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                battles = battles ?? new List<HistoricalBattleInstance>();
                battles.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.BattleId));
                if (dataVersion != DataVersion) dataVersion = DataVersion;
            }
        }
    }
}
