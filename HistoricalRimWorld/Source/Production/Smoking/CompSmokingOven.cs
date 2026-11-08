using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Production.Smoking
{
    public sealed class CompProperties_SmokingOven : CompProperties_ThingContainer
    {
        public List<SmokingRecipeDef> recipes = new List<SmokingRecipeDef>();
        public float woodConsumptionPerDay = 6f;
        public int updateInterval = 250;
        public bool pauseDuringRainWhenUnroofed = true;
        public float rainRateThreshold = 0.01f;
        public float maxStartRotProgressPct = 0.9f;
        public float failRotProgressPct = 1f;
        public int smokeFleckInterval = 2500;
        public float smokeFleckScale = 0.45f;

        public CompProperties_SmokingOven() => compClass = typeof(CompSmokingOven);

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (recipes == null || recipes.Count == 0) yield return "At least one smoking recipe is required.";
            if (recipes != null && recipes.Where(item => item != null).Select(item => item.defName).Distinct().Count() != recipes.Count)
                yield return "recipes contains duplicate entries.";
            if (woodConsumptionPerDay <= 0f || updateInterval < 1) yield return "Fuel use and updateInterval must be positive.";
            if (maxStartRotProgressPct <= 0f || maxStartRotProgressPct >= 1f
                || failRotProgressPct <= maxStartRotProgressPct || failRotProgressPct > 1f)
                yield return "Rot thresholds must satisfy 0 < maxStart < fail <= 1.";
            if (smokeFleckInterval < updateInterval || smokeFleckScale <= 0f) yield return "Smoke fleck settings are invalid.";
        }
    }

    public sealed class CompSmokingOven : CompThingContainer
    {
        private const int TicksPerDay = 60000;
        private string selectedRecipeDefName;
        private string batchIngredientDefName;
        private int batchInputCount;
        private int processingTicks;
        private bool manuallyPaused;
        private bool autoRepeat = true;
        private bool batchFailed;
        private bool batchCompleted;

        private new CompProperties_SmokingOven Props => (CompProperties_SmokingOven)props;
        public IEnumerable<SmokingRecipeDef> Recipes => Props.recipes ?? Enumerable.Empty<SmokingRecipeDef>();
        public SmokingRecipeDef SelectedRecipe
        {
            get
            {
                SmokingRecipeDef selected = string.IsNullOrWhiteSpace(selectedRecipeDefName)
                    ? null : DefDatabase<SmokingRecipeDef>.GetNamedSilentFail(selectedRecipeDefName);
                if (HasBatch || batchCompleted) return selected;
                return selected != null && selected.HasAvailableIngredients
                    ? selected : Recipes.FirstOrDefault(recipe => recipe != null && recipe.HasAvailableIngredients);
            }
        }
        public bool HasBatch => batchInputCount > 0 && !string.IsNullOrWhiteSpace(batchIngredientDefName);
        public bool CanLoad => !HasBatch && !batchCompleted && !batchFailed && !manuallyPaused;
        public bool HasFuel => parent?.TryGetComp<CompRefuelable>()?.HasFuel == true;
        public float Fuel => parent?.TryGetComp<CompRefuelable>()?.Fuel ?? 0f;
        public int BatchInputCount => batchInputCount;
        public string BatchIngredientDefName => batchIngredientDefName;
        public bool ManuallyPaused => manuallyPaused;

        public override bool Accepts(Thing thing) => thing != null && Accepts(thing.def);

        public override bool Accepts(ThingDef thingDef)
        {
            if (thingDef == null || batchCompleted || batchFailed) return false;
            if (HasBatch) return thingDef.defName == batchIngredientDefName && batchInputCount < SelectedRecipe?.inputCount;
            return CanLoad && SelectedRecipe?.Accepts(thingDef) == true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref selectedRecipeDefName, "historicalSmokingSelectedRecipe");
            Scribe_Values.Look(ref batchIngredientDefName, "historicalSmokingBatchIngredient");
            Scribe_Values.Look(ref batchInputCount, "historicalSmokingBatchInputCount", 0);
            Scribe_Values.Look(ref processingTicks, "historicalSmokingProcessingTicks", 0);
            Scribe_Values.Look(ref manuallyPaused, "historicalSmokingManuallyPaused", false);
            Scribe_Values.Look(ref autoRepeat, "historicalSmokingAutoRepeat", true);
            Scribe_Values.Look(ref batchFailed, "historicalSmokingBatchFailed", false);
            Scribe_Values.Look(ref batchCompleted, "historicalSmokingBatchCompleted", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                batchInputCount = Math.Max(0, batchInputCount);
                processingTicks = Math.Max(0, processingTicks);
                if (innerContainer == null) innerContainer = new ThingOwner<Thing>(this);
                if (!HasBatch && !batchCompleted)
                {
                    Thing input = innerContainer.FirstOrDefault(thing => thing?.def != SelectedRecipe?.outputThing);
                    if (input != null)
                    {
                        batchIngredientDefName = input.def.defName;
                        batchInputCount = innerContainer.Where(thing => thing?.def == input.def).Sum(thing => thing.stackCount);
                    }
                }
            }
        }

        public override void CompTick()
        {
            // Container contents tick normally; smoking never freezes input freshness.
            base.CompTick();
            if (parent?.Map == null || Find.TickManager == null || Find.TickManager.TicksGame % Props.updateInterval != 0) return;
            if (batchCompleted)
            {
                Thing output = innerContainer.FirstOrDefault(thing => thing?.def == SelectedRecipe?.outputThing);
                if (output != null && innerContainer.TryDrop(output, parent.Position, parent.Map, ThingPlaceMode.Near, out _))
                {
                    ResetBatch();
                    if (!autoRepeat) manuallyPaused = true;
                }
                return;
            }
            if (!HasBatch || batchFailed || manuallyPaused) return;
            if (!ValidateBatchFreshness()) { FailBatch(); return; }
            if (!EnvironmentAllowsProcessing(out _)) return;
            if (!HasFuel) return;

            SmokingRecipeDef recipe = SelectedRecipe;
            if (recipe == null) return;
            CompRefuelable fuel = parent.TryGetComp<CompRefuelable>();
            if (fuel == null || fuel.Fuel <= 0f) return;
            int fuelSupportedTicks = Mathf.FloorToInt(fuel.Fuel * TicksPerDay / Props.woodConsumptionPerDay);
            int processingDelta = Math.Min(Props.updateInterval, fuelSupportedTicks);
            if (processingDelta <= 0) return;

            // Consume only while a valid batch is actually processing. Refuelable's native gizmo,
            // filter, hauling job, and Scribe fuel state remain in use.
            fuel.ConsumeFuel(Props.woodConsumptionPerDay * processingDelta / TicksPerDay);
            processingTicks += processingDelta;
            if (Find.TickManager.TicksGame % Props.smokeFleckInterval == 0)
                FleckMaker.ThrowSmoke(parent.DrawPos, parent.Map, Props.smokeFleckScale);

            int requiredTicks = Mathf.Max(1, Mathf.RoundToInt(recipe.processingDays * TicksPerDay));
            if (processingTicks >= requiredTicks) TryFinishBatch(recipe);
        }

        private bool EnvironmentAllowsProcessing(out string reason)
        {
            reason = null;
            Map map = parent?.Map;
            if (map == null || !parent.Position.InBounds(map)) { reason = "HR_Smoking_NoMap"; return false; }
            bool roofed = map.roofGrid.Roofed(parent.Position);
            bool raining = map.weatherManager != null && (map.weatherManager.RainRate > Props.rainRateThreshold
                || map.weatherManager.SnowRate > Props.rainRateThreshold);
            if (!roofed && raining && Props.pauseDuringRainWhenUnroofed)
            {
                reason = "HR_Smoking_RainPause";
                return false;
            }
            return true;
        }

        private bool ValidateBatchFreshness()
        {
            SmokingRecipeDef recipe = SelectedRecipe;
            List<Thing> inputs = InputThings();
            if (recipe == null || inputs.Count == 0 || inputs.Sum(item => item.stackCount) != batchInputCount
                || batchInputCount > recipe.inputCount) return false;
            foreach (Thing input in inputs)
            {
                CompRottable rot = input.TryGetComp<CompRottable>();
                if (!recipe.Accepts(input.def) || rot == null || rot.RotProgressPct >= Props.failRotProgressPct) return false;
            }
            return true;
        }

        private List<Thing> InputThings() => innerContainer.Where(thing => thing != null && !thing.Destroyed
            && thing.def?.defName == batchIngredientDefName).ToList();

        private void TryFinishBatch(SmokingRecipeDef recipe)
        {
            if (!ValidateBatchFreshness()) { FailBatch(); return; }
            List<Thing> inputs = InputThings();
            int inputCount = inputs.Sum(item => item.stackCount);
            float inputNutrition = inputs.Sum(item => item.GetStatValue(StatDefOf.Nutrition) * item.stackCount);
            float outputNutrition = recipe.outputThing.GetStatValueAbstract(StatDefOf.Nutrition);
            int outputCount = Math.Min(recipe.OutputCountFor(inputCount), inputCount);
            if (outputNutrition > 0f) outputCount = Math.Min(outputCount, Mathf.FloorToInt(inputNutrition / outputNutrition));
            if (outputCount < 1) { FailBatch(); return; }

            float freshnessPct = inputs.Select(item => item.TryGetComp<CompRottable>()?.RotProgressPct ?? 1f).DefaultIfEmpty(1f).Max();
            Thing output = ThingMaker.MakeThing(recipe.outputThing);
            output.stackCount = outputCount;
            CompRottable outputRot = output.TryGetComp<CompRottable>();
            if (outputRot != null) outputRot.RotProgress = Mathf.Clamp01(freshnessPct) * outputRot.PropsRot.TicksToRotStart;
            if (!innerContainer.TryAdd(output))
            {
                output.Destroy(DestroyMode.Vanish);
                return; // Keep inputs and completion progress so output can retry without loss.
            }
            foreach (Thing input in inputs)
            {
                innerContainer.Remove(input);
                if (!input.Destroyed) input.Destroy(DestroyMode.Vanish);
            }
            batchIngredientDefName = recipe.outputThing.defName;
            batchInputCount = 0;
            batchCompleted = true;
        }

        public int CountToLoad(Thing ingredient)
        {
            SmokingRecipeDef recipe = SelectedRecipe;
            if (!CanLoad || recipe == null || ingredient == null || ingredient.Destroyed || ingredient.stackCount < recipe.minimumInputCount
                || ingredient.IsBurning() || !recipe.Accepts(ingredient.def)) return 0;
            CompRottable rot = ingredient.TryGetComp<CompRottable>();
            if (rot == null || rot.Stage != RotStage.Fresh || rot.RotProgressPct >= Props.maxStartRotProgressPct) return 0;
            return Math.Min(recipe.inputCount, ingredient.stackCount);
        }

        public bool TryLoadCarried(Pawn pawn, out string error)
        {
            error = null;
            Thing carried = pawn?.carryTracker?.CarriedThing;
            int count = carried?.stackCount ?? 0;
            SmokingRecipeDef recipe = SelectedRecipe;
            CompRottable rot = carried?.TryGetComp<CompRottable>();
            if (!CanLoad || recipe == null || carried == null || count < recipe.minimumInputCount || count > recipe.inputCount
                || !recipe.Accepts(carried.def) || rot == null || rot.Stage != RotStage.Fresh
                || rot.RotProgressPct >= Props.maxStartRotProgressPct)
            {
                error = "HR_Smoking_LoadRejected".Translate().ToString();
                return false;
            }
            string ingredientDefName = carried.def.defName;
            if (!innerContainer.TryAddOrTransfer(carried, true))
            {
                error = "HR_Smoking_LoadRejected".Translate().ToString();
                return false;
            }
            batchIngredientDefName = ingredientDefName;
            batchInputCount = count;
            processingTicks = 0;
            batchFailed = false;
            batchCompleted = false;
            return true;
        }

        public void SelectRecipe(SmokingRecipeDef recipe)
        {
            if (recipe != null && !HasBatch && Recipes.Contains(recipe)) selectedRecipeDefName = recipe.defName;
        }

        public void TogglePause() => manuallyPaused = !manuallyPaused;
        public void ToggleAutoRepeat() => autoRepeat = !autoRepeat;

        public bool TryCancelBatch(out string error)
        {
            error = null;
            if (parent?.Map == null) { error = "HR_Smoking_NoMap".Translate().ToString(); return false; }
            if (innerContainer.Any && !innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near))
            {
                error = "HR_Smoking_ReturnBlocked".Translate().ToString();
                return false;
            }
            ResetBatch();
            return true;
        }

        public override string CompInspectStringExtra()
        {
            string baseText = base.CompInspectStringExtra();
            SmokingRecipeDef recipe = SelectedRecipe;
            int requiredTicks = recipe == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(recipe.processingDays * TicksPerDay));
            int percent = requiredTicks == 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(processingTicks * 100f / requiredTicks), 0, 100);
            float fuelDays = Props.woodConsumptionPerDay <= 0f ? 0f : Fuel / Props.woodConsumptionPerDay;
            string status;
            if (batchFailed) status = "HR_Smoking_Status_Spoiled".Translate().ToString();
            else if (batchCompleted) status = "HR_Smoking_Status_Completed".Translate().ToString();
            else if (manuallyPaused) status = "HR_Smoking_Status_ManualPause".Translate().ToString();
            else if (!HasBatch) status = "HR_Smoking_Status_Empty".Translate().ToString();
            else if (!HasFuel) status = "HR_Smoking_Status_NoFuel".Translate().ToString();
            else if (!EnvironmentAllowsProcessing(out string reason)) status = "HR_Smoking_Status_Paused".Translate(reason.Translate()).ToString();
            else status = "HR_Smoking_Status_Working".Translate().ToString();
            string batchLabel = DefDatabase<ThingDef>.GetNamedSilentFail(batchIngredientDefName)?.LabelCap.ToString() ?? "-";
            string details = "HR_Smoking_Recipe".Translate(recipe?.LabelCap ?? "HR_Smoking_NoRecipe".Translate())
                + "\n" + "HR_Smoking_Batch".Translate(batchLabel, batchInputCount)
                + "\n" + "HR_Smoking_Progress".Translate(percent, Math.Max(0f, requiredTicks - processingTicks) / TicksPerDay)
                + "\n" + "HR_Smoking_Fuel".Translate(Fuel.ToString("0.0"), fuelDays.ToString("0.0"))
                + "\n" + status;
            return string.IsNullOrWhiteSpace(baseText) ? details : baseText + "\n" + details;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (parent?.Faction != Faction.OfPlayer) yield break;
            Command_Action choose = new Command_Action { defaultLabel = "HR_Smoking_ChooseRecipe".Translate(), defaultDesc = "HR_Smoking_ChooseRecipeDesc".Translate(), icon = TexCommand.ForbidOff, action = OpenRecipeMenu };
            choose.Disable(HasBatch || batchCompleted ? "HR_Smoking_CannotChange".Translate() : null);
            yield return choose;
            yield return new Command_Action { defaultLabel = manuallyPaused ? "HR_Smoking_Continue".Translate() : "HR_Smoking_Pause".Translate(), icon = manuallyPaused ? TexCommand.ForbidOff : TexCommand.ForbidOn, action = TogglePause };
            yield return new Command_Action { defaultLabel = autoRepeat ? "HR_Smoking_AutoRepeatOn".Translate() : "HR_Smoking_AutoRepeatOff".Translate(), icon = TexCommand.DesirePower, action = ToggleAutoRepeat };
            if (HasBatch || batchFailed || batchCompleted)
                yield return new Command_Action { defaultLabel = "HR_Smoking_Cancel".Translate(), icon = TexCommand.ForbidOn, action = () => { if (!TryCancelBatch(out string error)) Messages.Message(error, MessageTypeDefOf.RejectInput, false); } };
        }

        private void OpenRecipeMenu()
        {
            List<FloatMenuOption> options = Recipes.Where(recipe => recipe != null && recipe.HasAvailableIngredients)
                .Select(recipe => new FloatMenuOption(recipe.LabelCap.ToString(), () => SelectRecipe(recipe))).ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private void FailBatch()
        {
            innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
            processingTicks = 0;
            batchInputCount = 0;
            batchIngredientDefName = null;
            batchCompleted = false;
            batchFailed = true;
            Messages.Message("HR_Smoking_BatchSpoiled".Translate(), parent, MessageTypeDefOf.NegativeEvent, false);
        }

        private void ResetBatch()
        {
            batchIngredientDefName = null;
            batchInputCount = 0;
            processingTicks = 0;
            batchFailed = false;
            batchCompleted = false;
        }
    }

    public sealed class WorkGiver_LoadSmokingOven : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                ThingDef oven = DefDatabase<ThingDef>.GetNamedSilentFail("HR_SmokingOven");
                return oven == null ? ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial) : ThingRequest.ForDef(oven);
            }
        }
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;
        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompSmokingOven oven = thing?.TryGetComp<CompSmokingOven>();
            return oven != null && pawn?.Map != null && pawn.IsColonistPlayerControlled
                && (forced || (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && pawn.workSettings != null && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)))
                && oven.CanLoad && !thing.IsForbidden(pawn) && pawn.CanReserveAndReach(thing, PathEndMode.Touch, MaxPathDanger(pawn))
                && FindIngredient(pawn, oven) != null;
        }
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompSmokingOven oven = thing?.TryGetComp<CompSmokingOven>();
            Thing item = oven == null ? null : FindIngredient(pawn, oven);
            int count = item == null ? 0 : oven.CountToLoad(item);
            if (count < 1) return null;
            Job job = JobMaker.MakeJob(SmokingOvenJobDefOf.HR_LoadSmokingOven, thing, item);
            job.count = count;
            return job;
        }
        private static Thing FindIngredient(Pawn pawn, CompSmokingOven oven) => pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
            .Where(item => item != null && item.Spawned && !item.Destroyed && !item.IsForbidden(pawn) && oven.CountToLoad(item) > 0
                && pawn.CanReserveAndReach(item, PathEndMode.ClosestTouch, Danger.Some))
            .OrderBy(item => pawn.Position.DistanceToSquared(item.Position)).FirstOrDefault();
    }

    [DefOf]
    public static class SmokingOvenJobDefOf
    {
        public static JobDef HR_LoadSmokingOven;
        static SmokingOvenJobDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SmokingOvenJobDefOf));
    }

    public sealed class JobDriver_LoadSmokingOven : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed)
            && pawn.Reserve(TargetB, job, 1, job.count, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(() => TargetA.Thing == null || TargetB.Thing == null || TargetA.Thing.Destroyed || TargetB.Thing.Destroyed
                || TargetA.Thing.Map != pawn.Map || TargetB.Thing.Map != pawn.Map);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, true);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                CompSmokingOven oven = TargetA.Thing?.TryGetComp<CompSmokingOven>();
                string error = null;
                if (oven == null || !oven.TryLoadCarried(pawn, out error))
                {
                    Messages.Message(error ?? "HR_Smoking_LoadRejected".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                    EndJobWith(JobCondition.Incompletable);
                }
            });
        }
    }
}
