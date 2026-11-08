using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Production.Salting
{
    public sealed class CompProperties_PicklingJar : CompProperties_ThingContainer
    {
        public List<SaltingRecipeDef> recipes = new List<SaltingRecipeDef>();
        public ThingDef saltThing;
        public int updateInterval = 250;
        public float freezingPointCelsius = 0f;
        public float coldTemperatureLimitCelsius = 10f;
        public float normalTemperatureLimitCelsius = 20f;
        public float hotTemperatureLimitCelsius = 30f;
        public float coldEfficiency = 0.8f;
        public float normalEfficiency = 1f;
        public float maxStartRotProgressPct = 0.9f;
        public float warmMaxRotProgressPct = 0.75f;

        public CompProperties_PicklingJar() => compClass = typeof(CompPicklingJar);

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (saltThing == null) yield return "saltThing is required.";
            if (recipes == null || recipes.Count == 0) yield return "At least one salting recipe is required.";
            if (recipes != null && recipes.Where(recipe => recipe != null).Select(recipe => recipe.defName).Distinct().Count() != recipes.Count)
                yield return "recipes contains duplicate entries.";
            if (!(freezingPointCelsius < coldTemperatureLimitCelsius && coldTemperatureLimitCelsius <= normalTemperatureLimitCelsius
                && normalTemperatureLimitCelsius <= hotTemperatureLimitCelsius)) yield return "Temperature limits must be in increasing order.";
            if (coldEfficiency <= 0f || normalEfficiency <= 0f || updateInterval < 1) yield return "Efficiencies and updateInterval must be positive.";
            if (maxStartRotProgressPct <= 0f || maxStartRotProgressPct >= 1f || warmMaxRotProgressPct <= 0f || warmMaxRotProgressPct >= 1f)
                yield return "Rot progress thresholds must be between 0 and 1.";
        }
    }

    public sealed class CompPicklingJar : CompThingContainer
    {
        private const int TicksPerDay = 60000;
        private string selectedRecipeDefName;
        private string batchIngredientDefName;
        private int processingTicks;
        private bool manuallyPaused;
        private bool autoRepeat = true;
        private bool batchSpoiled;
        private bool batchCompleted;

        private new CompProperties_PicklingJar Props => (CompProperties_PicklingJar)props;
        public IEnumerable<SaltingRecipeDef> Recipes => Props.recipes ?? Enumerable.Empty<SaltingRecipeDef>();
        public SaltingRecipeDef SelectedRecipe
        {
            get
            {
                SaltingRecipeDef selected = string.IsNullOrWhiteSpace(selectedRecipeDefName)
                    ? null : DefDatabase<SaltingRecipeDef>.GetNamedSilentFail(selectedRecipeDefName);
                if (HasBatch || batchCompleted) return selected;
                return selected != null && selected.HasAvailableIngredients ? selected : Recipes.FirstOrDefault(recipe => recipe.HasAvailableIngredients);
            }
        }
        public bool HasBatch => IngredientCount > 0 || SaltCount > 0 || processingTicks > 0 || !string.IsNullOrEmpty(batchIngredientDefName);
        public bool CanLoad => !manuallyPaused && !batchSpoiled && !batchCompleted;
        public bool ManuallyPaused => manuallyPaused;

        private int IngredientCount => innerContainer.Where(thing => thing?.def?.defName == batchIngredientDefName).Sum(thing => thing.stackCount);
        private int SaltCount => innerContainer.Where(thing => thing?.def == Props.saltThing).Sum(thing => thing.stackCount);
        private int TotalInputCount => IngredientCount;
        private bool BatchReady => SelectedRecipe != null && IngredientCount >= SelectedRecipe.ingredientCount && SaltCount >= SelectedRecipe.saltCount;

        public override bool Accepts(Thing thing) => thing != null && Accepts(thing.def);

        public override bool Accepts(ThingDef thingDef)
        {
            if (thingDef == null || batchCompleted) return false;
            if (HasBatch && thingDef.defName == batchIngredientDefName) return IngredientCount < SelectedRecipe?.ingredientCount;
            if (thingDef == Props.saltThing) return SelectedRecipe != null && SaltCount < SelectedRecipe.saltCount
                && IngredientCount >= SelectedRecipe.ingredientCount;
            return !HasBatch || string.IsNullOrEmpty(batchIngredientDefName)
                ? CanLoad && SelectedRecipe?.AcceptsIngredient(thingDef) == true
                : thingDef.defName == batchIngredientDefName && SelectedRecipe?.AcceptsIngredient(thingDef) == true
                    && IngredientCount < SelectedRecipe.ingredientCount;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref selectedRecipeDefName, "historicalPicklingSelectedRecipe");
            Scribe_Values.Look(ref batchIngredientDefName, "historicalPicklingBatchIngredient");
            Scribe_Values.Look(ref processingTicks, "historicalPicklingProcessingTicks", 0);
            Scribe_Values.Look(ref manuallyPaused, "historicalPicklingManuallyPaused", false);
            Scribe_Values.Look(ref autoRepeat, "historicalPicklingAutoRepeat", true);
            Scribe_Values.Look(ref batchSpoiled, "historicalPicklingBatchSpoiled", false);
            Scribe_Values.Look(ref batchCompleted, "historicalPicklingBatchCompleted", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                processingTicks = Math.Max(0, processingTicks);
                if (innerContainer == null) innerContainer = new ThingOwner<Thing>(this);
                if (string.IsNullOrEmpty(batchIngredientDefName))
                    batchIngredientDefName = innerContainer.FirstOrDefault(thing => thing?.def != Props.saltThing)?.def?.defName;
            }
        }

        public override void CompTick()
        {
            base.CompTick(); // Keep original ingredient freshness ticking inside the jar.
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
            if (!HasBatch || batchSpoiled) return;
            if (!ValidateLoadedBatch())
            {
                SpoilBatch();
                return;
            }
            if (!BatchReady || manuallyPaused) return;
            float temperature = GenTemperature.GetTemperatureForCell(parent.Position, parent.Map);
            float efficiency = temperature < Props.freezingPointCelsius || temperature > Props.hotTemperatureLimitCelsius ? 0f
                : temperature < Props.coldTemperatureLimitCelsius ? Props.coldEfficiency : Props.normalEfficiency;
            if (efficiency <= 0f) return;
            processingTicks += Mathf.Max(1, Mathf.RoundToInt(Props.updateInterval * efficiency));
            int requiredTicks = Mathf.Max(1, Mathf.RoundToInt(SelectedRecipe.processingDays * TicksPerDay));
            if (processingTicks >= requiredTicks) TryFinishBatch(temperature >= Props.normalTemperatureLimitCelsius);
        }

        private bool ValidateLoadedBatch()
        {
            SaltingRecipeDef recipe = SelectedRecipe;
            if (recipe == null || Props.saltThing == null || IngredientCount < 1) return false;
            foreach (Thing ingredient in innerContainer.Where(thing => thing?.def != Props.saltThing))
            {
                CompRottable rottable = ingredient.TryGetComp<CompRottable>();
                float maxRot = CurrentTemperature() >= Props.normalTemperatureLimitCelsius ? Props.warmMaxRotProgressPct : 1f;
                if (!recipe.AcceptsIngredient(ingredient.def) || rottable == null || rottable.Stage != RotStage.Fresh
                    || rottable.RotProgressPct >= maxRot) return false;
            }
            return IngredientCount <= recipe.ingredientCount && SaltCount <= recipe.saltCount;
        }

        private float CurrentTemperature() => parent?.Map == null ? 0f : GenTemperature.GetTemperatureForCell(parent.Position, parent.Map);

        private void TryFinishBatch(bool warm)
        {
            SaltingRecipeDef recipe = SelectedRecipe;
            if (recipe == null || !ValidateLoadedBatch()) { SpoilBatch(); return; }
            List<Thing> inputs = innerContainer.Where(thing => thing?.def != Props.saltThing).ToList();
            float inputNutrition = inputs.Sum(thing => thing.GetStatValue(StatDefOf.Nutrition) * thing.stackCount);
            float outputNutrition = recipe.outputThing.GetStatValueAbstract(StatDefOf.Nutrition);
            int outputCount = Math.Min(recipe.outputCount, TotalInputCount);
            if (outputNutrition > 0f) outputCount = Math.Min(outputCount, Mathf.FloorToInt(inputNutrition / outputNutrition));
            if (outputCount < 1) { SpoilBatch(); return; }

            float freshnessPct = inputs.Select(thing => thing.TryGetComp<CompRottable>()?.RotProgressPct ?? 1f).DefaultIfEmpty(1f).Max();
            Thing output = ThingMaker.MakeThing(recipe.outputThing);
            output.stackCount = outputCount;
            CompRottable outputRot = output.TryGetComp<CompRottable>();
            if (outputRot != null) outputRot.RotProgress = Mathf.Clamp01(freshnessPct) * outputRot.PropsRot.TicksToRotStart;
            if (!innerContainer.TryAdd(output))
            {
                output.Destroy(DestroyMode.Vanish);
                return; // Keep every input and the completed progress for a safe retry.
            }
            foreach (Thing input in inputs.ToList())
            {
                innerContainer.Remove(input);
                if (!input.Destroyed) input.Destroy(DestroyMode.Vanish);
            }
            foreach (Thing salt in innerContainer.Where(thing => thing?.def == Props.saltThing).ToList())
            {
                innerContainer.Remove(salt);
                if (!salt.Destroyed) salt.Destroy(DestroyMode.Vanish);
            }
            batchIngredientDefName = recipe.outputThing.defName;
            batchCompleted = true;
        }

        private void SpoilBatch()
        {
            innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
            processingTicks = 0;
            batchIngredientDefName = null;
            batchCompleted = false;
            batchSpoiled = true;
            Messages.Message("HR_Pickling_BatchSpoiled".Translate(), parent, MessageTypeDefOf.NegativeEvent, false);
        }

        public int CountToLoad(Thing thing)
        {
            SaltingRecipeDef recipe = SelectedRecipe;
            if (!CanLoad || recipe == null || thing == null || thing.Destroyed || thing.stackCount < 1 || thing.IsBurning()) return 0;
            if (thing.def == Props.saltThing)
                return IngredientCount >= recipe.ingredientCount ? Math.Min(recipe.saltCount - SaltCount, thing.stackCount) : 0;
            if (!recipe.AcceptsIngredient(thing.def)) return 0;
            if (!string.IsNullOrEmpty(batchIngredientDefName) && batchIngredientDefName != thing.def.defName) return 0;
            CompRottable rot = thing.TryGetComp<CompRottable>();
            if (rot == null || rot.Stage != RotStage.Fresh || rot.RotProgressPct >= Props.maxStartRotProgressPct) return 0;
            return Math.Min(recipe.ingredientCount - IngredientCount, thing.stackCount);
        }

        public bool TryLoadCarried(Pawn pawn, out string error)
        {
            error = null;
            Thing carried = pawn?.carryTracker?.CarriedThing;
            int count = carried?.stackCount ?? 0;
            if (!CanLoad || carried == null || count < 1 || CountToLoad(carried) < count)
            {
                error = "HR_Pickling_LoadRejected".Translate().ToString();
                return false;
            }
            if (carried.def != Props.saltThing)
            {
                if (string.IsNullOrEmpty(batchIngredientDefName)) batchIngredientDefName = carried.def.defName;
                CompRottable rot = carried.TryGetComp<CompRottable>();
                if (rot == null || rot.Stage != RotStage.Fresh || rot.RotProgressPct >= Props.maxStartRotProgressPct)
                {
                    error = "HR_Pickling_LoadRejected".Translate().ToString();
                    return false;
                }
            }
            if (!innerContainer.TryAddOrTransfer(carried, true))
            {
                error = "HR_Pickling_LoadRejected".Translate().ToString();
                return false;
            }
            batchSpoiled = false;
            return true;
        }

        public void SelectRecipe(SaltingRecipeDef recipe)
        {
            if (recipe != null && !HasBatch && Recipes.Contains(recipe)) selectedRecipeDefName = recipe.defName;
        }
        public void TogglePause() => manuallyPaused = !manuallyPaused;
        public void ToggleAutoRepeat() => autoRepeat = !autoRepeat;

        public bool TryCancelBatch(out string error)
        {
            error = null;
            if (parent?.Map == null) { error = "HR_Pickling_NoMap".Translate().ToString(); return false; }
            if (innerContainer.Any && !innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near))
            {
                error = "HR_Pickling_ReturnBlocked".Translate().ToString();
                return false;
            }
            ResetBatch();
            return true;
        }

        public override string CompInspectStringExtra()
        {
            string containerText = base.CompInspectStringExtra();
            string recipe = SelectedRecipe?.LabelCap.ToString() ?? "HR_Pickling_NoRecipe".Translate().ToString();
            float temperature = CurrentTemperature();
            float efficiency = temperature < Props.freezingPointCelsius || temperature > Props.hotTemperatureLimitCelsius ? 0f
                : temperature < Props.coldTemperatureLimitCelsius ? Props.coldEfficiency : Props.normalEfficiency;
            int requiredTicks = SelectedRecipe == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(SelectedRecipe.processingDays * TicksPerDay));
            int pct = requiredTicks == 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(processingTicks * 100f / requiredTicks), 0, 100);
            string status = batchSpoiled ? "HR_Pickling_Status_Spoiled".Translate().ToString()
                : batchCompleted ? "HR_Pickling_Status_Completed".Translate().ToString()
                : manuallyPaused ? "HR_Pickling_Status_ManualPause".Translate().ToString()
                : temperature < Props.freezingPointCelsius ? "HR_Pickling_Status_Cold".Translate().ToString()
                : temperature > Props.hotTemperatureLimitCelsius ? "HR_Pickling_Status_Hot".Translate().ToString()
                : BatchReady ? "HR_Pickling_Status_Working".Translate().ToString()
                : "HR_Pickling_Status_Waiting".Translate().ToString();
            string details = "HR_Pickling_Recipe".Translate(recipe)
                + "\n" + "HR_Pickling_Ingredients".Translate(IngredientCount, SelectedRecipe?.ingredientCount ?? 0, SaltCount, SelectedRecipe?.saltCount ?? 0)
                + "\n" + "HR_Pickling_Progress".Translate(pct, temperature.ToString("0.0"), (efficiency * 100f).ToString("0"))
                + "\n" + status;
            return string.IsNullOrWhiteSpace(containerText) ? details : containerText + "\n" + details;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (parent?.Faction != Faction.OfPlayer) yield break;
            Command_Action choose = new Command_Action { defaultLabel = "HR_Pickling_ChooseRecipe".Translate(), defaultDesc = "HR_Pickling_ChooseRecipeDesc".Translate(), icon = TexCommand.ForbidOff, action = OpenRecipeMenu };
            choose.Disable(HasBatch ? "HR_Pickling_CannotChange".Translate() : null);
            yield return choose;
            yield return new Command_Action { defaultLabel = manuallyPaused ? "HR_Pickling_Continue".Translate() : "HR_Pickling_Pause".Translate(), icon = manuallyPaused ? TexCommand.ForbidOff : TexCommand.ForbidOn, action = TogglePause };
            yield return new Command_Action { defaultLabel = autoRepeat ? "HR_Pickling_AutoRepeatOn".Translate() : "HR_Pickling_AutoRepeatOff".Translate(), icon = TexCommand.DesirePower, action = ToggleAutoRepeat };
            if (HasBatch || batchSpoiled || batchCompleted)
                yield return new Command_Action { defaultLabel = "HR_Pickling_Cancel".Translate(), icon = TexCommand.ForbidOn, action = () => { if (!TryCancelBatch(out string error)) Messages.Message(error, MessageTypeDefOf.RejectInput, false); } };
        }

        private void OpenRecipeMenu()
        {
            List<FloatMenuOption> options = Recipes.Where(recipe => recipe.HasAvailableIngredients)
                .Select(recipe => new FloatMenuOption(recipe.LabelCap.ToString(), () => SelectRecipe(recipe))).ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ResetBatch()
        {
            batchIngredientDefName = null;
            processingTicks = 0;
            batchSpoiled = false;
            batchCompleted = false;
        }
    }

    public sealed class WorkGiver_LoadPicklingJar : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                ThingDef jar = DefDatabase<ThingDef>.GetNamedSilentFail("HR_PicklingJar");
                return jar == null ? ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial) : ThingRequest.ForDef(jar);
            }
        }
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;
        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompPicklingJar jar = thing?.TryGetComp<CompPicklingJar>();
            return jar != null && pawn?.Map != null && pawn.IsColonistPlayerControlled
                && (forced || (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && pawn.workSettings != null && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)))
                && jar.CanLoad && !thing.IsForbidden(pawn) && pawn.CanReserveAndReach(thing, PathEndMode.Touch, MaxPathDanger(pawn))
                && FindItem(pawn, jar) != null;
        }
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompPicklingJar jar = thing?.TryGetComp<CompPicklingJar>();
            Thing item = jar == null ? null : FindItem(pawn, jar);
            int count = item == null ? 0 : jar.CountToLoad(item);
            if (count < 1) return null;
            Job job = JobMaker.MakeJob(PicklingJarJobDefOf.HR_LoadPicklingJar, thing, item);
            job.count = count;
            return job;
        }
        private static Thing FindItem(Pawn pawn, CompPicklingJar jar) => pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
            .Where(item => item != null && item.Spawned && !item.Destroyed && !item.IsForbidden(pawn) && jar.CountToLoad(item) > 0
                && pawn.CanReserveAndReach(item, PathEndMode.ClosestTouch, Danger.Some))
            .OrderBy(item => pawn.Position.DistanceToSquared(item.Position)).FirstOrDefault();
    }

    [DefOf]
    public static class PicklingJarJobDefOf
    {
        public static JobDef HR_LoadPicklingJar;
        static PicklingJarJobDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(PicklingJarJobDefOf));
    }

    public sealed class JobDriver_LoadPicklingJar : JobDriver
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
                CompPicklingJar jar = TargetA.Thing?.TryGetComp<CompPicklingJar>();
                string error = null;
                if (jar == null || !jar.TryLoadCarried(pawn, out error))
                {
                    Messages.Message(error ?? "HR_Pickling_LoadRejected".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                    EndJobWith(JobCondition.Incompletable);
                }
            });
        }
    }
}
