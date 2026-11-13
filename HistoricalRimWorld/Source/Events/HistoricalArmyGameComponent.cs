using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalArmyGameComponent : GameComponent
    {
        private const int DataVersion = 1;
        private int dataVersion;
        private List<HistoricalArmyRuntime> armies = new List<HistoricalArmyRuntime>();

        public HistoricalArmyGameComponent(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.TickManager.TicksGame % 2500 != 0
                || !HistoricalScenarioManager.IsScenarioActive) return;
            string activeScenario = HistoricalScenarioManager.ActiveScenarioDefName;
            foreach (HistoricalCharacterDef def in DefDatabase<HistoricalCharacterDef>.AllDefsListForReading
                         .Where(item => item != null && item.historicalScenarioId == activeScenario))
                Current.Game?.GetComponent<HistoricalCharacterWorldComponent>()?.GetOrRegister(def);
        }

        public HistoricalArmyRuntime FindArmy(string armyId) => string.IsNullOrWhiteSpace(armyId)
            ? null : armies?.FirstOrDefault(item => item != null && item.ArmyId == armyId);

        internal HistoricalArmyRuntime GetOrCreateArmy(HistoricalArmyDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.armyId)) return null;
            if (armies == null) armies = new List<HistoricalArmyRuntime>();
            HistoricalArmyRuntime state = FindArmy(def.armyId);
            if (state != null) return state;
            state = new HistoricalArmyRuntime();
            state.Initialize(def.armyId, def.historicalEventId, def.factionId);
            armies.Add(state);
            return state;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "historicalArmyDataVersion", 0);
            Scribe_Collections.Look(ref armies, "historicalArmyInstances", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.Saving) dataVersion = DataVersion;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                armies = armies ?? new List<HistoricalArmyRuntime>();
                armies.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.ArmyId));
                if (dataVersion != DataVersion) dataVersion = DataVersion;
            }
        }
    }
}
