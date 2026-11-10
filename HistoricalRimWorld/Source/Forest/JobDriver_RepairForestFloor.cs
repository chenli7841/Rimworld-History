using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Forest
{
    public sealed class JobDriver_RepairForestFloor : JobDriver
    {
        private IntVec3 RepairCell => job.targetA.Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !RepairCell.InBounds(pawn.Map)
                || !ForestSoilErosionUtility.IsConstructedFloor(pawn.Map, RepairCell, out _));
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);

            Toil repair = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Never
            };
            repair.initAction = () =>
            {
                if (pawn.Map?.GetComponent<MapComponent_ForestSoilErosion>()?.IsRepairRequested(RepairCell) != true)
                    EndJobWith(JobCondition.Succeeded);
            };
            repair.tickAction = () =>
            {
                MapComponent_ForestSoilErosion erosion = pawn.Map?.GetComponent<MapComponent_ForestSoilErosion>();
                if (erosion == null || !ForestSoilErosionUtility.IsConstructedFloor(pawn.Map, RepairCell, out _))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                float workSpeed = pawn.GetStatValue(StatDefOf.ConstructionSpeed);
                if (workSpeed <= 0f) workSpeed = 0.05f;
                if (erosion.ApplyRepairWork(RepairCell, workSpeed))
                    EndJobWith(JobCondition.Succeeded);
            };
            yield return repair;
        }
    }
}
