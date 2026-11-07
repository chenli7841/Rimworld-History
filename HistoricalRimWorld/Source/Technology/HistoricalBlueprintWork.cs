using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Technology
{
    public sealed class WorkGiver_DecodeHistoricalBlueprint : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(HistoricalBlueprintDefOf.HR_HistoricalTechnologyBlueprint);
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (!HistoricalTechnologyManager.IsActive || pawn?.Map == null) yield break;
            List<Thing> blueprints = pawn.Map.listerThings.ThingsOfDef(HistoricalBlueprintDefOf.HR_HistoricalTechnologyBlueprint);
            if (blueprints == null) yield break;
            foreach (Thing thing in blueprints) yield return thing;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!HistoricalTechnologyManager.IsActive || pawn?.Map == null || thing == null || thing.Destroyed
                || !pawn.IsColonistPlayerControlled || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research)
                || pawn.workSettings == null || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Research)
                || thing.IsForbidden(pawn) || !pawn.CanReserveAndReach(thing, PathEndMode.Touch, MaxPathDanger(pawn))) return false;

            CompHistoricalBlueprint blueprint = thing.TryGetComp<CompHistoricalBlueprint>();
            if (blueprint?.Research == null || HistoricalBlueprintService.HasAuthorization(blueprint.Research)
                || !HistoricalTechnologyManager.HasTechnologyEmerged(blueprint.Research)) return false;
            Thing bench = FindResearchBench(pawn, thing);
            return bench != null && pawn.CanReserveAndReach(bench, PathEndMode.InteractionCell, MaxPathDanger(pawn));
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Thing bench = FindResearchBench(pawn, thing);
            return bench == null ? null : JobMaker.MakeJob(HistoricalBlueprintJobDefOf.HR_DecodeHistoricalBlueprint, thing, bench);
        }

        private static Thing FindResearchBench(Pawn pawn, Thing blueprint) => pawn?.Map?.listerBuildings?.allBuildingsColonist
            .Where(item => item != null && !item.Destroyed && !item.IsForbidden(pawn)
                && (item.def.defName == "SimpleResearchBench" || item.def.defName == "HiTechResearchBench"))
            .Where(item => pawn.CanReserveAndReach(item, PathEndMode.InteractionCell, Danger.Some))
            .OrderBy(item => item.Position.DistanceTo(blueprint.Position)).FirstOrDefault();
    }

    [DefOf]
    public static class HistoricalBlueprintJobDefOf
    {
        public static JobDef HR_DecodeHistoricalBlueprint;
        static HistoricalBlueprintJobDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(HistoricalBlueprintJobDefOf));
    }

    public sealed class JobDriver_DecodeHistoricalBlueprint : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(TargetB, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(() => TargetA.Thing == null || TargetB.Thing == null
                || TargetA.Thing.Destroyed || TargetB.Thing.Destroyed
                || TargetA.Thing.Map != pawn.Map || TargetB.Thing.Map != pawn.Map);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil work = new Toil();
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.tickAction = () =>
            {
                CompHistoricalBlueprint blueprint = TargetA.Thing?.TryGetComp<CompHistoricalBlueprint>();
                if (blueprint == null) { EndJobWith(JobCondition.Incompletable); return; }
                blueprint.DecodingProgress = Math.Min(HistoricalBlueprintService.DefaultDecodingWork,
                    blueprint.DecodingProgress + Math.Max(0.1f, pawn.GetStatValue(StatDefOf.ResearchSpeed)));
                if (blueprint.DecodingProgress >= HistoricalBlueprintService.DefaultDecodingWork) ReadyForNextToil();
            };
            work.WithProgressBar(TargetIndex.A, () =>
            {
                CompHistoricalBlueprint blueprint = TargetA.Thing?.TryGetComp<CompHistoricalBlueprint>();
                return blueprint == null ? 0f : blueprint.DecodingProgress / HistoricalBlueprintService.DefaultDecodingWork;
            });
            yield return work;
            yield return Toils_General.Do(() =>
            {
                if (!HistoricalBlueprintService.TryCompleteDecoding(TargetA.Thing, out string error))
                {
                    Messages.Message("HR_Blueprint_DecodingFailed".Translate(error), MessageTypeDefOf.RejectInput, false);
                    EndJobWith(JobCondition.Incompletable);
                }
            });
        }
    }
}
