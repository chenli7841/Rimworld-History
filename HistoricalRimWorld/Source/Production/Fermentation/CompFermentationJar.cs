using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Production.Fermentation
{
    public sealed class CompProperties_FermentationJar : CompProperties_ThingContainer
    {
        public List<FermentationRecipeDef> recipes = new List<FermentationRecipeDef>();
        public int updateInterval = 250;
        public float maxStartRotProgressPct = 0.9f;
        public float failRotProgressPct = 1f;
        public float overheatFailureRiskPerDay = 0.25f;
        public float failureRiskThreshold = 1f;

        public CompProperties_FermentationJar() => compClass = typeof(CompFermentationJar);

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (recipes == null || !recipes.Any(recipe => recipe != null && recipe.enabledByDefault && recipe.HasAvailableIngredients))
                yield return "At least one enabled fermentation recipe with available ingredients is required.";
            if (recipes != null && recipes.Where(recipe => recipe != null).Select(recipe => recipe.defName).Distinct().Count() != recipes.Count)
                yield return "recipes contains duplicate entries.";
            if (updateInterval < 1) yield return "updateInterval must be positive.";
            if (maxStartRotProgressPct <= 0f || maxStartRotProgressPct >= 1f || failRotProgressPct <= maxStartRotProgressPct || failRotProgressPct > 1f)
                yield return "Rot thresholds must satisfy 0 < maxStart < fail <= 1.";
            if (overheatFailureRiskPerDay < 0f || failureRiskThreshold <= 0f) yield return "Overheat risk settings are invalid.";
        }
    }

    public sealed class CompFermentationJar : CompThingContainer
    {
        private const int TicksPerDay = 60000;
        private string selectedRecipeDefName;
        private string batchRecipeDefName;
        private string batchIngredientDefName;
        private int batchInputCount;
        private int processingTicks;
        private float failureRisk;
        private bool processingStarted;
        private bool manuallyPaused;
        private bool autoRepeat = true;
        private bool batchFailed;
        private bool batchCompleted;
        private string failureReason;

        private new CompProperties_FermentationJar Props => (CompProperties_FermentationJar)props;
        public IEnumerable<FermentationRecipeDef> Recipes => Props.recipes ?? Enumerable.Empty<FermentationRecipeDef>();
        private FermentationRecipeDef BatchRecipe => string.IsNullOrEmpty(batchRecipeDefName)
            ? null : DefDatabase<FermentationRecipeDef>.GetNamedSilentFail(batchRecipeDefName);
        public FermentationRecipeDef SelectedRecipe
        {
            get
            {
                FermentationRecipeDef selected = string.IsNullOrWhiteSpace(selectedRecipeDefName)
                    ? null : DefDatabase<FermentationRecipeDef>.GetNamedSilentFail(selectedRecipeDefName);
                if (processingStarted || batchCompleted || batchFailed) return BatchRecipe ?? selected;
                return selected != null && selected.HasAvailableIngredients
                    ? selected : Recipes.FirstOrDefault(recipe => recipe != null && recipe.HasAvailableIngredients);
            }
        }
        public bool HasBatch => !string.IsNullOrEmpty(batchIngredientDefName) || processingStarted || batchCompleted || batchFailed;
        public bool CanLoad => !manuallyPaused && !processingStarted && !batchCompleted && !batchFailed;
        public bool ProcessingStarted => processingStarted;
        public bool ManuallyPaused => manuallyPaused;
        public int BatchInputCount => batchInputCount;
        public string BatchIngredientDefName => batchIngredientDefName;

        private int InputCount => string.IsNullOrEmpty(batchIngredientDefName) ? 0
            : innerContainer.Where(thing => thing?.def?.defName == batchIngredientDefName).Sum(thing => thing.stackCount);
        private int AuxiliaryCount => BatchRecipe?.auxiliaryIngredientDef == null ? 0
            : innerContainer.Where(thing => thing?.def == BatchRecipe.auxiliaryIngredientDef).Sum(thing => thing.stackCount);
        private bool InputsReady => BatchRecipe != null && InputCount >= BatchRecipe.inputCount;
        private bool AuxiliaryReady => BatchRecipe == null || BatchRecipe.auxiliaryCount <= 0 || AuxiliaryCount >= BatchRecipe.auxiliaryCount;

        public override bool Accepts(Thing thing) => thing != null && Accepts(thing.def);

        public override bool Accepts(ThingDef thingDef)
        {
            if (thingDef == null || batchCompleted || batchFailed || processingStarted) return false;
            FermentationRecipeDef recipe = HasBatch ? BatchRecipe : SelectedRecipe;
            if (recipe == null) return false;
            if (InputsReady && recipe.auxiliaryIngredientDef == thingDef && AuxiliaryCount < recipe.auxiliaryCount) return true;
            return !InputsReady && recipe.AcceptsIngredient(thingDef)
                && (string.IsNullOrEmpty(batchIngredientDefName) || batchIngredientDefName == thingDef.defName)
                && InputCount < recipe.inputCount;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref selectedRecipeDefName, "historicalFermentationSelectedRecipe");
            Scribe_Values.Look(ref batchRecipeDefName, "historicalFermentationBatchRecipe");
            Scribe_Values.Look(ref batchIngredientDefName, "historicalFermentationBatchIngredient");
            Scribe_Values.Look(ref batchInputCount, "historicalFermentationBatchInputCount", 0);
            Scribe_Values.Look(ref processingTicks, "historicalFermentationProcessingTicks", 0);
            Scribe_Values.Look(ref failureRisk, "historicalFermentationFailureRisk", 0f);
            Scribe_Values.Look(ref processingStarted, "historicalFermentationProcessingStarted", false);
            Scribe_Values.Look(ref manuallyPaused, "historicalFermentationManuallyPaused", false);
            Scribe_Values.Look(ref autoRepeat, "historicalFermentationAutoRepeat", true);
            Scribe_Values.Look(ref batchFailed, "historicalFermentationBatchFailed", false);
            Scribe_Values.Look(ref batchCompleted, "historicalFermentationBatchCompleted", false);
            Scribe_Values.Look(ref failureReason, "historicalFermentationFailureReason");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                batchInputCount = Math.Max(0, batchInputCount);
                processingTicks = Math.Max(0, processingTicks);
                failureRisk = Mathf.Max(0f, failureRisk);
                if (innerContainer == null) innerContainer = new ThingOwner<Thing>(this);
                if (string.IsNullOrEmpty(batchIngredientDefName) && !batchCompleted)
                {
                    FermentationRecipeDef recipe = BatchRecipe ?? SelectedRecipe;
                    Thing first = innerContainer.FirstOrDefault(thing => thing?.def != recipe?.outputThing && thing?.def != recipe?.auxiliaryIngredientDef);
                    if (first != null)
                    {
                        batchIngredientDefName = first.def.defName;
                        batchInputCount = innerContainer.Where(thing => thing?.def == first.def).Sum(thing => thing.stackCount);
                    }
                }
            }
        }

        public override void CompTick()
        {
            // Keep native rot ticking for all contained input stacks; processing never freezes freshness.
            base.CompTick();
            if (parent?.Map == null || Find.TickManager == null || Find.TickManager.TicksGame % Props.updateInterval != 0) return;
            if (batchCompleted)
            {
                Thing output = innerContainer.FirstOrDefault(thing => thing?.def == BatchRecipe?.outputThing);
                if (output != null && innerContainer.TryDrop(output, parent.Position, parent.Map, ThingPlaceMode.Near, out _))
                {
                    ResetBatch();
                    if (!autoRepeat) manuallyPaused = true;
                }
                return;
            }
            if (batchFailed || !HasBatch) return;
            if (!ValidateLoadedIngredients()) { FailBatch("HR_Fermentation_Failure_Spoiled"); return; }
            if (!processingStarted)
            {
                TryStartBatch();
                return;
            }
            if (manuallyPaused) return;

            float temperature = GenTemperature.GetTemperatureForCell(parent.Position, parent.Map);
            FermentationRecipeDef recipe = BatchRecipe;
            if (recipe == null) { FailBatch("HR_Fermentation_Failure_MissingRecipe"); return; }
            if (temperature > recipe.maxTemperature)
            {
                failureRisk += Props.overheatFailureRiskPerDay * Props.updateInterval / TicksPerDay;
                if (failureRisk >= Props.failureRiskThreshold) FailBatch("HR_Fermentation_Failure_Overheated");
                return;
            }

            float efficiency = FermentationTemperatureUtility.Efficiency(temperature, recipe);
            if (efficiency <= 0f) return;
            processingTicks += Mathf.Max(1, Mathf.RoundToInt(Props.updateInterval * efficiency));
            int requiredTicks = Mathf.Max(1, Mathf.RoundToInt(recipe.processingDays * TicksPerDay));
            if (processingTicks >= requiredTicks) TryFinishBatch(recipe);
        }

        private bool ValidateLoadedIngredients()
        {
            FermentationRecipeDef recipe = BatchRecipe ?? SelectedRecipe;
            if (recipe == null || string.IsNullOrEmpty(batchIngredientDefName)) return false;
            List<Thing> ingredients = InputThings();
            if (ingredients.Count == 0 || ingredients.Sum(item => item.stackCount) != batchInputCount || batchInputCount > recipe.inputCount) return false;
            foreach (Thing ingredient in ingredients)
            {
                CompRottable rot = ingredient.TryGetComp<CompRottable>();
                if (!recipe.AcceptsIngredient(ingredient.def) || rot == null || rot.Stage != RotStage.Fresh
                    || rot.RotProgressPct >= (processingStarted ? Props.failRotProgressPct : Props.maxStartRotProgressPct)) return false;
            }
            return AuxiliaryCount <= recipe.auxiliaryCount;
        }

        private List<Thing> InputThings() => innerContainer.Where(thing => thing != null && !thing.Destroyed
            && thing.def?.defName == batchIngredientDefName).ToList();

        private bool TryStartBatch()
        {
            FermentationRecipeDef recipe = BatchRecipe ?? SelectedRecipe;
            if (recipe == null || !InputsReady || !AuxiliaryReady) return false;
            if (!ValidateLoadedIngredients()) { FailBatch("HR_Fermentation_Failure_Spoiled"); return false; }
            foreach (Thing auxiliary in innerContainer.Where(thing => thing?.def == recipe.auxiliaryIngredientDef).ToList())
            {
                innerContainer.Remove(auxiliary);
                if (!auxiliary.Destroyed) auxiliary.Destroy(DestroyMode.Vanish);
            }
            batchRecipeDefName = recipe.defName;
            processingStarted = true;
            processingTicks = 0;
            failureRisk = 0f;
            return true;
        }

        public int CountToLoad(Thing item)
        {
            FermentationRecipeDef recipe = HasBatch ? BatchRecipe : SelectedRecipe;
            if (!CanLoad || recipe == null || item == null || item.Destroyed || item.stackCount < 1 || item.IsBurning()
                || !Accepts(item.def)) return 0;
            if (item.def == recipe.auxiliaryIngredientDef)
                return Math.Min(recipe.auxiliaryCount - AuxiliaryCount, item.stackCount);
            CompRottable rot = item.TryGetComp<CompRottable>();
            if (rot == null || rot.Stage != RotStage.Fresh || rot.RotProgressPct >= Props.maxStartRotProgressPct) return 0;
            return Math.Min(recipe.inputCount - InputCount, item.stackCount);
        }

        public bool TryLoadCarried(Pawn pawn, out string error)
        {
            error = null;
            Thing carried = pawn?.carryTracker?.CarriedThing;
            FermentationRecipeDef recipe = HasBatch ? BatchRecipe : SelectedRecipe;
            int count = carried?.stackCount ?? 0;
            if (!CanLoad || recipe == null || carried == null || count < 1 || count > CountToLoad(carried))
            {
                error = "HR_Fermentation_LoadRejected".Translate().ToString();
                return false;
            }
            bool isAuxiliary = carried.def == recipe.auxiliaryIngredientDef;
            if (!isAuxiliary)
            {
                CompRottable rot = carried.TryGetComp<CompRottable>();
                if (!recipe.AcceptsIngredient(carried.def) || rot == null || rot.Stage != RotStage.Fresh
                    || rot.RotProgressPct >= Props.maxStartRotProgressPct)
                {
                    error = "HR_Fermentation_LoadRejected".Translate().ToString();
                    return false;
                }
            }
            string inputId = isAuxiliary ? batchIngredientDefName : carried.def.defName;
            if (!innerContainer.TryAddOrTransfer(carried, true))
            {
                error = "HR_Fermentation_LoadRejected".Translate().ToString();
                return false;
            }
            if (!isAuxiliary)
            {
                batchIngredientDefName = inputId;
                batchInputCount = InputCount;
            }
            batchRecipeDefName = recipe.defName;
            batchFailed = false;
            TryStartBatch();
            return true;
        }

        public void SelectRecipe(FermentationRecipeDef recipe)
        {
            if (recipe != null && recipe.HasAvailableIngredients && !HasBatch) selectedRecipeDefName = recipe.defName;
        }
        public void TogglePause() => manuallyPaused = !manuallyPaused;
        public void ToggleAutoRepeat() => autoRepeat = !autoRepeat;

        public bool TryCancelBatch(out string error)
        {
            error = null;
            if (parent?.Map == null) { error = "HR_Fermentation_NoMap".Translate().ToString(); return false; }
            if (innerContainer.Any && !innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near))
            {
                error = "HR_Fermentation_ReturnBlocked".Translate().ToString();
                return false;
            }
            // Salt and other auxiliary materials are consumed at the instant processing starts;
            // cancellation after that point returns remaining food but does not refund them.
            ResetBatch();
            return true;
        }

        public override string CompInspectStringExtra()
        {
            string baseText = base.CompInspectStringExtra();
            FermentationRecipeDef recipe = HasBatch ? BatchRecipe : SelectedRecipe;
            float temperature = parent?.Map == null ? 0f : GenTemperature.GetTemperatureForCell(parent.Position, parent.Map);
            float efficiency = recipe == null ? 0f : FermentationTemperatureUtility.Efficiency(temperature, recipe);
            int requiredTicks = recipe == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(recipe.processingDays * TicksPerDay));
            int percent = requiredTicks == 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(processingTicks * 100f / requiredTicks), 0, 100);
            string label = recipe?.LabelCap.ToString() ?? "HR_Fermentation_NoRecipe".Translate().ToString();
            string status;
            if (batchFailed) status = "HR_Fermentation_Status_Failed".Translate(failureReason.Translate()).ToString();
            else if (batchCompleted) status = "HR_Fermentation_Status_Completed".Translate().ToString();
            else if (manuallyPaused) status = "HR_Fermentation_Status_ManualPause".Translate().ToString();
            else if (!HasBatch) status = "HR_Fermentation_Status_Empty".Translate().ToString();
            else if (!processingStarted) status = "HR_Fermentation_Status_Loading".Translate().ToString();
            else if (temperature < recipe.minTemperature) status = "HR_Fermentation_Status_Cold".Translate().ToString();
            else if (temperature > recipe.maxTemperature) status = "HR_Fermentation_Status_Hot".Translate((failureRisk / Props.failureRiskThreshold * 100f).ToString("0")).ToString();
            else status = "HR_Fermentation_Status_Working".Translate().ToString();
            string inputLabel = DefDatabase<ThingDef>.GetNamedSilentFail(batchIngredientDefName)?.LabelCap.ToString() ?? "-";
            int auxiliaryForDisplay = processingStarted || batchCompleted ? recipe?.auxiliaryCount ?? 0 : AuxiliaryCount;
            string ingredientDetails = batchCompleted
                ? "HR_Fermentation_Output".Translate(inputLabel, innerContainer.TotalStackCount)
                : "HR_Fermentation_Ingredients".Translate(inputLabel, InputCount, recipe?.inputCount ?? 0,
                    recipe?.auxiliaryIngredientDef?.LabelCap ?? "-", auxiliaryForDisplay, recipe?.auxiliaryCount ?? 0);
            string details = "HR_Fermentation_Recipe".Translate(label)
                + "\n" + ingredientDetails
                + "\n" + "HR_Fermentation_Temperature".Translate(temperature.ToString("0.0"), (efficiency * 100f).ToString("0"))
                + "\n" + "HR_Fermentation_Progress".Translate(percent, Math.Max(0f, requiredTicks - processingTicks) / TicksPerDay)
                + "\n" + status;
            return string.IsNullOrWhiteSpace(baseText) ? details : baseText + "\n" + details;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (parent?.Faction != Faction.OfPlayer) yield break;
            Command_Action choose = new Command_Action { defaultLabel = "HR_Fermentation_ChooseRecipe".Translate(), defaultDesc = "HR_Fermentation_ChooseRecipeDesc".Translate(), icon = TexCommand.ForbidOff, action = OpenRecipeMenu };
            choose.Disable(HasBatch ? "HR_Fermentation_CannotChange".Translate() : null);
            yield return choose;
            yield return new Command_Action { defaultLabel = manuallyPaused ? "HR_Fermentation_Continue".Translate() : "HR_Fermentation_Pause".Translate(), icon = manuallyPaused ? TexCommand.ForbidOff : TexCommand.ForbidOn, action = TogglePause };
            yield return new Command_Action { defaultLabel = autoRepeat ? "HR_Fermentation_AutoRepeatOn".Translate() : "HR_Fermentation_AutoRepeatOff".Translate(), icon = TexCommand.DesirePower, action = ToggleAutoRepeat };
            if (HasBatch || batchFailed || batchCompleted)
                yield return new Command_Action { defaultLabel = "HR_Fermentation_Cancel".Translate(), icon = TexCommand.ForbidOn, action = () => { if (!TryCancelBatch(out string error)) Messages.Message(error, MessageTypeDefOf.RejectInput, false); } };
        }

        private void OpenRecipeMenu()
        {
            List<FloatMenuOption> options = Recipes.Where(recipe => recipe != null && recipe.HasAvailableIngredients)
                .Select(recipe => new FloatMenuOption(recipe.LabelCap.ToString(), () => SelectRecipe(recipe))).ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private void TryFinishBatch(FermentationRecipeDef recipe)
        {
            if (!ValidateLoadedIngredients()) { FailBatch("HR_Fermentation_Failure_Spoiled"); return; }
            List<Thing> inputs = InputThings();
            int count = inputs.Sum(item => item.stackCount);
            float nutritionIn = inputs.Sum(item => item.GetStatValue(StatDefOf.Nutrition) * item.stackCount);
            float nutritionOut = recipe.outputThing.GetStatValueAbstract(StatDefOf.Nutrition);
            int outputCount = Math.Min(recipe.outputCount, count);
            if (nutritionOut > 0f) outputCount = Math.Min(outputCount, Mathf.FloorToInt(nutritionIn / nutritionOut));
            if (outputCount < 1) { FailBatch("HR_Fermentation_Failure_Nutrition"); return; }
            float freshness = inputs.Select(item => item.TryGetComp<CompRottable>()?.RotProgressPct ?? 1f).DefaultIfEmpty(1f).Max();
            Thing output = ThingMaker.MakeThing(recipe.outputThing);
            output.stackCount = outputCount;
            CompRottable outputRot = output.TryGetComp<CompRottable>();
            if (outputRot != null) outputRot.RotProgress = Mathf.Clamp01(freshness) * outputRot.PropsRot.TicksToRotStart;
            if (!innerContainer.TryAdd(output))
            {
                output.Destroy(DestroyMode.Vanish);
                return; // Preserve inputs and progress; the completed output can retry later.
            }
            foreach (Thing input in inputs)
            {
                innerContainer.Remove(input);
                if (!input.Destroyed) input.Destroy(DestroyMode.Vanish);
            }
            batchInputCount = 0;
            batchIngredientDefName = recipe.outputThing.defName;
            processingStarted = false;
            batchCompleted = true;
        }

        private void FailBatch(string reason)
        {
            innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
            processingTicks = 0;
            batchInputCount = 0;
            batchIngredientDefName = null;
            processingStarted = false;
            batchCompleted = false;
            batchFailed = true;
            failureReason = reason;
            Messages.Message("HR_Fermentation_BatchFailed".Translate(reason.Translate()), parent, MessageTypeDefOf.NegativeEvent, false);
        }

        private void ResetBatch()
        {
            batchRecipeDefName = null;
            batchIngredientDefName = null;
            batchInputCount = 0;
            processingTicks = 0;
            failureRisk = 0f;
            processingStarted = false;
            batchFailed = false;
            batchCompleted = false;
            failureReason = null;
        }
    }

    public static class FermentationTemperatureUtility
    {
        public static float Efficiency(float temperature, FermentationRecipeDef recipe)
        {
            if (recipe == null || temperature < recipe.minTemperature || temperature > recipe.maxTemperature) return 0f;
            if (temperature >= recipe.normalTemperature && temperature <= recipe.warmTemperature) return 1f;
            if (temperature >= recipe.slowTemperature && temperature < recipe.normalTemperature) return 0.7f;
            if (temperature > recipe.warmTemperature && temperature <= recipe.maxTemperature) return 0.7f;
            return 0.3f;
        }
    }

    public sealed class WorkGiver_LoadFermentationJar : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                ThingDef jar = DefDatabase<ThingDef>.GetNamedSilentFail("HR_FermentationJar");
                return jar == null ? ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial) : ThingRequest.ForDef(jar);
            }
        }
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;
        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompFermentationJar jar = thing?.TryGetComp<CompFermentationJar>();
            return jar != null && pawn?.Map != null && pawn.IsColonistPlayerControlled
                && (forced || (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && pawn.workSettings != null && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)))
                && jar.CanLoad && !thing.IsForbidden(pawn) && pawn.CanReserveAndReach(thing, PathEndMode.Touch, MaxPathDanger(pawn))
                && FindIngredient(pawn, jar) != null;
        }
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompFermentationJar jar = thing?.TryGetComp<CompFermentationJar>();
            Thing item = jar == null ? null : FindIngredient(pawn, jar);
            int count = item == null ? 0 : jar.CountToLoad(item);
            if (count < 1) return null;
            Job job = JobMaker.MakeJob(FermentationJarJobDefOf.HR_LoadFermentationJar, thing, item);
            job.count = count;
            return job;
        }
        private static Thing FindIngredient(Pawn pawn, CompFermentationJar jar) => pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
            .Where(item => item != null && item.Spawned && !item.Destroyed && !item.IsForbidden(pawn) && jar.CountToLoad(item) > 0
                && pawn.CanReserveAndReach(item, PathEndMode.ClosestTouch, Danger.Some))
            .OrderBy(item => pawn.Position.DistanceToSquared(item.Position)).FirstOrDefault();
    }

    [DefOf]
    public static class FermentationJarJobDefOf
    {
        public static JobDef HR_LoadFermentationJar;
        static FermentationJarJobDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(FermentationJarJobDefOf));
    }

    public sealed class JobDriver_LoadFermentationJar : JobDriver
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
                CompFermentationJar jar = TargetA.Thing?.TryGetComp<CompFermentationJar>();
                string error = null;
                if (jar == null || !jar.TryLoadCarried(pawn, out error))
                {
                    Messages.Message(error ?? "HR_Fermentation_LoadRejected".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                    EndJobWith(JobCondition.Incompletable);
                }
            });
        }
    }
}
