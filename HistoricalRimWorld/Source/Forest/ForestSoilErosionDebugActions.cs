using LudeonTK;
using Verse;
using GameUI = Verse.UI;

namespace HistoricalRimWorld.Forest
{
    public static class ForestSoilErosionDebugActions
    {
        [DebugAction("Historical RimWorld", "HR Erosion: Show Tracking Statistics", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ShowStats()
        {
            Map map = Find.CurrentMap;
            MapComponent_ForestSoilErosion erosion = map?.GetComponent<MapComponent_ForestSoilErosion>();
            int active = map?.GetComponent<MapComponent_AncientForestSoil>()?.AncientForestSoilCellCount ?? 0;
            if (erosion == null) return;
            Log.Message("[Historical RimWorld] Forest erosion stats: active soil=" + active
                + ", tracked floors=" + erosion.TrackedFloorCount + ", repair requests=" + erosion.RepairRequestedCount
                + ", exposed buildings=" + erosion.ExposedBuildingCount
                + ", update interval=" + ForestSoilErosionUtility.ErosionUpdateInterval + " ticks.");
        }

        [DebugAction("Historical RimWorld", "HR Erosion: Toggle Floor Overlay", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ToggleOverlay()
        {
            MapComponent_ForestSoilErosion.ShowErosionOverlay = !MapComponent_ForestSoilErosion.ShowErosionOverlay;
            Find.CurrentMap?.GetComponent<MapComponent_ForestSoilErosion>()?.RebuildErosionOverlay();
            Log.Message("[Historical RimWorld] Forest floor erosion overlay "
                + (MapComponent_ForestSoilErosion.ShowErosionOverlay ? "enabled" : "disabled") + ".");
        }

        [DebugAction("Historical RimWorld", "HR Erosion: Set Mouse Floor to 0%", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SetZero() => SetMouseCell(0f);

        [DebugAction("Historical RimWorld", "HR Erosion: Set Mouse Floor to 25%", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Set25() => SetMouseCell(0.25f);

        [DebugAction("Historical RimWorld", "HR Erosion: Set Mouse Floor to 50%", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Set50() => SetMouseCell(0.50f);

        [DebugAction("Historical RimWorld", "HR Erosion: Set Mouse Floor to 80%", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Set80() => SetMouseCell(0.80f);

        [DebugAction("Historical RimWorld", "HR Erosion: Set Mouse Floor to 99%", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Set99() => SetMouseCell(0.99f);

        [DebugAction("Historical RimWorld", "HR Erosion: Destroy Mouse Floor", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DestroyFloor() => SetMouseCell(1f);

        [DebugAction("Historical RimWorld", "HR Erosion: Advance Floors by One Day", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void AdvanceOneDay()
        {
            int count = Find.CurrentMap?.GetComponent<MapComponent_ForestSoilErosion>()?.SimulateFloorErosionDays(1f) ?? 0;
            Log.Message("[Historical RimWorld] Simulated one day of erosion on " + count + " tracked floor(s).");
        }

        [DebugAction("Historical RimWorld", "HR Erosion: Repair Mouse Floor", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RepairMouseFloor()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = GameUI.MouseCell();
            bool repaired = map?.GetComponent<MapComponent_ForestSoilErosion>()?.TrySetErosionProgress(cell, 0f) == true;
            Log.Message("[Historical RimWorld] Forced floor repair " + (repaired ? "at " + cell : "failed: no tracked floor at the mouse cell") + ".");
        }

        [DebugAction("Historical RimWorld", "HR Erosion: Toggle Mouse Floor Maintenance", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ToggleMouseFloorMaintenance()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = GameUI.MouseCell();
            MapComponent_ForestSoilErosion erosion = map?.GetComponent<MapComponent_ForestSoilErosion>();
            if (erosion == null || !ForestSoilErosionUtility.IsConstructedFloor(map, cell, out _)) return;
            FloorMaintenanceMode current = erosion.GetMaintenanceMode(cell);
            FloorMaintenanceMode next = current == FloorMaintenanceMode.Normal ? FloorMaintenanceMode.Priority
                : current == FloorMaintenanceMode.Priority ? FloorMaintenanceMode.Disabled : FloorMaintenanceMode.Normal;
            erosion.SetMaintenanceMode(cell, next);
            Log.Message("[Historical RimWorld] Floor maintenance at " + cell + " set to " + next + ".");
        }

        [DebugAction("Historical RimWorld", "HR Erosion: Request Mouse Floor Repair", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RequestMouseFloorRepair()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = GameUI.MouseCell();
            map?.GetComponent<MapComponent_ForestSoilErosion>()?.RequestManualRepair(cell);
            Log.Message("[Historical RimWorld] Manual floor repair requested at " + cell + ".");
        }

        private static void SetMouseCell(float progress)
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = GameUI.MouseCell();
            bool changed = map?.GetComponent<MapComponent_ForestSoilErosion>()?.TrySetErosionProgress(cell, progress) == true;
            Log.Message("[Historical RimWorld] Set floor erosion to " + (progress * 100f) + "% "
                + (changed ? "at " + cell : "failed: no floor at the mouse cell") + ".");
        }
    }
}
