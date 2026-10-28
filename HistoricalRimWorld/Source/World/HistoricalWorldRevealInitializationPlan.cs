using System.Collections.Generic;

namespace HistoricalRimWorld.World
{
    internal sealed class HistoricalWorldRevealInitializationPlan
    {
        public string ScenarioDefName { get; }
        public string SetupDefName { get; }
        public HistoricalWorldMapMode MapMode { get; }
        public int MapVersion { get; }
        public List<HistoricalRegionRevealState> Regions { get; }

        public HistoricalWorldRevealInitializationPlan(string scenarioDefName, string setupDefName,
            HistoricalWorldMapMode mapMode, int mapVersion, List<HistoricalRegionRevealState> regions)
        {
            ScenarioDefName = scenarioDefName;
            SetupDefName = setupDefName;
            MapMode = mapMode;
            MapVersion = mapVersion;
            Regions = regions;
        }
    }
}
