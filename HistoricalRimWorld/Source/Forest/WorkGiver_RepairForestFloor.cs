using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Forest
{
    public sealed class WorkGiver_RepairForestFloor : WorkGiver_Scanner
    {
        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            return pawn?.Map?.GetComponent<MapComponent_ForestSoilErosion>()?.GetRepairWorkCells()
                ?? System.Array.Empty<IntVec3>();
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
        {
            MapComponent_ForestSoilErosion erosion = pawn?.Map?.GetComponent<MapComponent_ForestSoilErosion>();
            return pawn != null && erosion != null && erosion.IsRepairRequested(cell)
                && pawn.CanReserve(cell) && pawn.CanReach(cell, PathEndMode.Touch, Danger.Some);
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
        {
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("HR_RepairForestFloor");
            return jobDef == null ? null : JobMaker.MakeJob(jobDef, new LocalTargetInfo(cell));
        }
    }
}
