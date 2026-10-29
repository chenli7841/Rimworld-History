using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public sealed class HistoricalScenarioGameComponent : GameComponent
    {
        public HistoricalScenarioGameComponent(Game game) { }

        private const int CurrentInitializationVersion = 1;

        private string activeScenarioDefName;
        private bool initialized;
        private int initializationVersion;

        public bool Initialized => initialized;
        public string ActiveScenarioDefName => activeScenarioDefName;
        public int InitializationVersion => initializationVersion;

        public bool TryBindScenario(string scenarioDefName)
        {
            if (string.IsNullOrWhiteSpace(scenarioDefName)) return false;

            if (initialized)
            {
                return string.Equals(activeScenarioDefName, scenarioDefName, System.StringComparison.Ordinal);
            }

            activeScenarioDefName = scenarioDefName;
            initializationVersion = CurrentInitializationVersion;
            initialized = true;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref activeScenarioDefName, "activeScenarioDefName");
            Scribe_Values.Look(ref initialized, "scenarioInitialized");
            Scribe_Values.Look(ref initializationVersion, "scenarioInitializationVersion");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && initialized)
            {
                if (string.IsNullOrWhiteSpace(activeScenarioDefName))
                {
                    Log.Error("[Historical RimWorld] This save marks a historical scenario active but contains no scenario DefName. The saved identity was left untouched.");
                }
                else if (!HistoricalScenarioRegistry.TryGetById(activeScenarioDefName, out _))
                {
                    Log.Error("[Historical RimWorld] Saved scenario Def was not found: " + activeScenarioDefName + ". The saved identity and historical clock were preserved.");
                }
            }
        }
    }
}
