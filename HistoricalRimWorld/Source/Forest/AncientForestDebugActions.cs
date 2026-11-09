using LudeonTK;
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
                + ", ancient trees=" + treeCount + ", generation applied=" + soil.GenerationApplied + ".");
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
    }
}
