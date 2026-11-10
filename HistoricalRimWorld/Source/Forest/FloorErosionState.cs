using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Saved per-cell state for a constructed floor above permanent ancient forest soil.</summary>
    public sealed class FloorErosionState : IExposable
    {
        public int CellIndex;
        public string TerrainDefName;
        public float ErosionProgress;
        public FloorMaintenanceMode MaintenanceMode = FloorMaintenanceMode.Normal;
        public bool RepairRequested;
        public bool ManualRepairRequested;
        public int LastUpdatedTick;

        public FloorErosionState() { }

        public FloorErosionState(int cellIndex, string terrainDefName, FloorMaintenanceMode mode, int tick)
        {
            CellIndex = cellIndex;
            TerrainDefName = terrainDefName;
            MaintenanceMode = mode;
            LastUpdatedTick = tick;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref CellIndex, "cellIndex", -1);
            Scribe_Values.Look(ref TerrainDefName, "terrainDefName");
            Scribe_Values.Look(ref ErosionProgress, "erosionProgress", 0f);
            Scribe_Values.Look(ref MaintenanceMode, "maintenanceMode", FloorMaintenanceMode.Normal);
            Scribe_Values.Look(ref RepairRequested, "repairRequested", false);
            Scribe_Values.Look(ref ManualRepairRequested, "manualRepairRequested", false);
            Scribe_Values.Look(ref LastUpdatedTick, "lastUpdatedTick", 0);
        }
    }
}
