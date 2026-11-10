using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Forest
{
    public sealed class JobDriver_CutAncientTree : JobDriver
    {
        private Plant TargetTree => job.targetA.Thing as Plant;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => TargetTree == null || !TargetTree.Spawned
                || !AncientWoodcuttingUtility.CanCutAncientTree(pawn, TargetTree, out _));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil cut = new Toil { defaultCompleteMode = ToilCompleteMode.Never };
            cut.initAction = () =>
            {
                if (!AncientWoodcuttingUtility.CanCutAncientTree(pawn, TargetTree, out string reason))
                {
                    if (!string.IsNullOrEmpty(reason)) Messages.Message(reason, TargetTree, MessageTypeDefOf.RejectInput, false);
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            cut.tickAction = () =>
            {
                Plant tree = TargetTree;
                if (tree == null || !tree.Spawned || !AncientWoodcuttingUtility.CanCutAncientTree(pawn, tree, out _))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                HR_WoodcuttingToolExtension toolExtension = AncientWoodcuttingUtility.GetEquippedTool(pawn);
                ThingWithComps tool = pawn.equipment?.Primary;
                float toolFactor = toolExtension?.ancientTreeCutSpeedFactor ?? 0f;
                float work = pawn.GetStatValue(StatDefOf.PlantWorkSpeed) * toolFactor;
                if (work <= 0f) work = 0.01f;
                MapComponent_AncientTreeHarvest progress = pawn.Map.GetComponent<MapComponent_AncientTreeHarvest>();
                float total = progress.AddWork(tree.thingIDNumber, work);
                bool completed = total >= AncientWoodcuttingUtility.TreeWorkAmount(tree);
                tool?.GetComp<CompAncientWoodcuttingTool>()?.ApplyWorkWear(work);

                if (tree.Destroyed)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (completed)
                {
                    progress.ClearWorkProgress(tree.thingIDNumber);
                    tree.PlantCollected(pawn, PlantDestructionMode.Chop);
                    EndJobWith(JobCondition.Succeeded);
                }
                else if (tool == null || tool.Destroyed || pawn.equipment?.Primary != tool)
                    EndJobWith(JobCondition.Incompletable);
            };
            yield return cut;
        }
    }
}
