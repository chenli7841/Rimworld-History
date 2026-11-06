using LudeonTK;
using Verse;

namespace HistoricalRimWorld.FogOfWar
{
    /// <summary>Developer-mode controls for inspecting and repairing local fog state.</summary>
    public static class HistoricalFogDebugActions
    {
        [DebugAction("Historical RimWorld", "HR Fog: Reveal Entire Map", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RevealEntireMap()
        {
            HistoricalLocalFogOfWarManager.GetComponent(Find.CurrentMap)?.RevealEntireMap();
        }

        [DebugAction("Historical RimWorld", "HR Fog: Reset Exploration", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ResetExploration()
        {
            HistoricalLocalFogMapComponent component = HistoricalLocalFogOfWarManager.GetComponent(Find.CurrentMap);
            if (component == null) return;
            component.ResetExploration();
            component.RefreshVisibility();
        }

        [DebugAction("Historical RimWorld", "HR Fog: Refresh Visibility", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RefreshVisibility()
        {
            HistoricalLocalFogOfWarManager.GetComponent(Find.CurrentMap)?.RefreshVisibility();
        }

        [DebugAction("Historical RimWorld", "HR Fog: Print Visibility Statistics", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void PrintVisibilityStatistics()
        {
            Map map = Find.CurrentMap;
            HistoricalLocalFogMapComponent component = HistoricalLocalFogOfWarManager.GetComponent(map);
            if (component == null || map == null) return;
            Log.Message("[Historical RimWorld] Fog stats: map=" + map.Size.x + "x" + map.Size.z
                + ", explored=" + component.ExploredCellCount
                + ", visible=" + component.VisibleCellCount
                + ", visionSources=" + component.ActiveVisionSourceCount
                + ", refreshMs=" + component.LastRefreshMilliseconds
                + ", refreshCount=" + component.RefreshCount);
        }
    }
}
