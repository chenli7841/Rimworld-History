using LudeonTK;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    public static class AncientForestDebugActions
    {
        [DebugAction("Historical RimWorld", "HR Forest: Show Soil and Tree Counts", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ShowCounts()
        {
            Map map = Find.CurrentMap;
            MapComponent_AncientForestSoil soil = map?.GetComponent<MapComponent_AncientForestSoil>();
            if (map == null || soil == null) return;
            int treeCount = 0;
            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
                if (thing?.def?.GetModExtension<HR_AncientTreeExtension>() != null) treeCount++;
            Log.Message("[Historical RimWorld] Ancient forest map status: soil cells=" + soil.AncientForestSoilCellCount
                + ", ancient trees=" + treeCount + ", generation applied=" + soil.GenerationApplied
                + ", mature trees=" + (map.GetComponent<MapComponent_AncientForestGrowth>()?.MatureTreeCount ?? 0) + ".");
        }

        [DebugAction("Historical RimWorld", "HR Forest: Generate Test Forest", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void GenerateTestForest()
        {
            new GenStep_AncientForest().GenerateForMap(Find.CurrentMap, true);
        }

        [DebugAction("Historical RimWorld", "HR Forest: Clear Debug Trees Only", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ClearDebugTrees()
        {
            int removed = Find.CurrentMap?.GetComponent<MapComponent_AncientForestSoil>()?.ClearDebugTrees() ?? 0;
            Log.Message("[Historical RimWorld] Removed " + removed + " debug-generated ancient tree(s); persistent forest soil was retained.");
        }

        [DebugAction("Historical RimWorld", "HR Forest: Run Reproduction Check Now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunReproductionCheck()
        {
            Find.CurrentMap?.GetComponent<MapComponent_AncientForestGrowth>()?.RunReproductionCheck();
        }

        [DebugAction("Historical RimWorld", "HR Forest: Toggle Reproduction Area Overlay", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ToggleReproductionOverlay()
        {
            AncientForestGrowthSettings.ShowReproductionOverlay = !AncientForestGrowthSettings.ShowReproductionOverlay;
            MapComponent_AncientForestGrowth growth = Find.CurrentMap?.GetComponent<MapComponent_AncientForestGrowth>();
            if (AncientForestGrowthSettings.ShowReproductionOverlay) growth?.RebuildReproductionOverlay();
            Log.Message("[Historical RimWorld] Ancient forest reproduction overlay "
                + (AncientForestGrowthSettings.ShowReproductionOverlay ? "enabled" : "disabled") + ".");
        }

        [DebugAction("Historical RimWorld", "HR Forest: Toggle Accelerated Growth Testing", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ToggleAcceleratedTesting()
        {
            AncientForestGrowthSettings.AcceleratedTesting = !AncientForestGrowthSettings.AcceleratedTesting;
            Find.CurrentMap?.GetComponent<MapComponent_AncientForestGrowth>()?.ResetScheduleForTesting();
            Log.Message("[Historical RimWorld] Ancient forest accelerated testing "
                + (AncientForestGrowthSettings.AcceleratedTesting ? "enabled" : "disabled") + ".");
        }

        [DebugAction("Historical RimWorld", "HR Forest: Simulate 10 Recovery Checks", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SimulateRecovery()
        {
            MapComponent_AncientForestGrowth growth = Find.CurrentMap?.GetComponent<MapComponent_AncientForestGrowth>();
            if (growth == null) return;
            int total = 0;
            for (int i = 0; i < 10; i++) total += growth.RunNaturalRecoveryCheck();
            Log.Message("[Historical RimWorld] Ten natural recovery checks created " + total + " ancient tree sapling(s).");
        }
    }
}
