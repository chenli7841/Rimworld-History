using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Production.Drying
{
    public sealed class CompProperties_DryingRack : CompProperties_ThingContainer
    {
        public List<DryingRecipeDef> recipes = new List<DryingRecipeDef>();
        public float freezingPointCelsius = 0f;
        public float coldTemperatureLimitCelsius = 10f;
        public float moderateTemperatureLimitCelsius = 25f;
        public float hotTemperatureLimitCelsius = 35f;
        public float coldTemperatureEfficiency = 0.4f;
        public float normalTemperatureEfficiency = 1f;
        public float warmTemperatureEfficiency = 1.2f;
        public float hotTemperatureEfficiency = 0.8f;
        public float roofedEfficiency = 0.7f;
        public float nighttimeEfficiency = 0.4f;
        public float daylightStart = 0.25f;
        public float daylightEnd = 0.75f;
        public bool pauseDuringRain = true;
        public bool pauseDuringSnow = true;
        public CompProperties_DryingRack() => compClass = typeof(CompDryingRack);

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (recipes == null || recipes.Count == 0) yield return "At least one drying recipe is required.";
            if (recipes != null && recipes.Where(item => item != null).Select(item => item.defName).Distinct().Count() != recipes.Count)
                yield return "recipes contains duplicate entries.";
            if (!(freezingPointCelsius < coldTemperatureLimitCelsius && coldTemperatureLimitCelsius <= moderateTemperatureLimitCelsius
                && moderateTemperatureLimitCelsius <= hotTemperatureLimitCelsius)) yield return "Temperature limits must be in increasing order.";
            if (coldTemperatureEfficiency < 0f || normalTemperatureEfficiency < 0f || warmTemperatureEfficiency < 0f || hotTemperatureEfficiency < 0f
                || roofedEfficiency < 0f || nighttimeEfficiency < 0f) yield return "Drying efficiencies cannot be negative.";
            if (daylightStart < 0f || daylightEnd > 1f || daylightStart >= daylightEnd) yield return "daylightStart and daylightEnd must define an interval within 0..1.";
        }
    }

    public sealed class CompDryingRack : CompThingContainer
    {
        private const int TicksPerDay = 60000;
        private const int UpdateInterval = 250;
        private string selectedRecipeDefName;
        private string batchIngredientDefName;
        private int batchInputCount;
        private int dryingProgressTicks;
        private bool manuallyPaused;
        private bool autoRepeat = true;
        private bool batchSpoiled;
        private bool batchCompleted;

        private new CompProperties_DryingRack Props => (CompProperties_DryingRack)props;
        public IEnumerable<DryingRecipeDef> Recipes => Props.recipes ?? Enumerable.Empty<DryingRecipeDef>();
        public DryingRecipeDef SelectedRecipe
        {
            get
            {
                DryingRecipeDef selected = string.IsNullOrWhiteSpace(selectedRecipeDefName)
                    ? null
                    : DefDatabase<DryingRecipeDef>.GetNamedSilentFail(selectedRecipeDefName);
                if (HasBatch || batchCompleted) return selected;
                return selected != null && selected.HasAvailableIngredients
                    ? selected
                    : Recipes.FirstOrDefault(recipe => recipe.HasAvailableIngredients);
            }
        }
        public bool HasBatch => batchInputCount > 0 && !string.IsNullOrWhiteSpace(batchIngredientDefName);
        public bool CanLoad => !HasBatch && !manuallyPaused && !batchSpoiled && !innerContainer.Any;
        public bool AutoRepeat => autoRepeat;
        public bool ManuallyPaused => manuallyPaused;
        public string BatchIngredientDefName => batchIngredientDefName;
        public int BatchInputCount => batchInputCount;

        public override bool Accepts(Thing thing)
        {
            if (thing == null || thing.def == null) return false;
            if (batchCompleted) return SelectedRecipe?.outputThing == thing.def;
            if (HasBatch) return thing.def.defName == batchIngredientDefName;
            return AcceptsIngredient(thing);
        }

        public override bool Accepts(ThingDef thingDef)
        {
            if (thingDef == null) return false;
            if (batchCompleted) return SelectedRecipe?.outputThing == thingDef;
            if (HasBatch) return thingDef.defName == batchIngredientDefName;
            DryingRecipeDef recipe = SelectedRecipe;
            return CanLoad && recipe != null && recipe.Accepts(thingDef);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref selectedRecipeDefName, "historicalDryingSelectedRecipe");
            Scribe_Values.Look(ref batchIngredientDefName, "historicalDryingBatchIngredient");
            Scribe_Values.Look(ref batchInputCount, "historicalDryingBatchInputCount", 0);
            Scribe_Values.Look(ref dryingProgressTicks, "historicalDryingProgressTicks", 0);
            Scribe_Values.Look(ref manuallyPaused, "historicalDryingManuallyPaused", false);
            Scribe_Values.Look(ref autoRepeat, "historicalDryingAutoRepeat", true);
            Scribe_Values.Look(ref batchSpoiled, "historicalDryingBatchSpoiled", false);
            Scribe_Values.Look(ref batchCompleted, "historicalDryingBatchCompleted", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (SelectedRecipe != null) selectedRecipeDefName = SelectedRecipe.defName;
                batchInputCount = Math.Max(0, batchInputCount);
                dryingProgressTicks = Math.Max(0, dryingProgressTicks);
                if (innerContainer == null) innerContainer = new ThingOwner<Thing>(this);
                if (!HasBatch && innerContainer.Any)
                {
                    Thing first = innerContainer[0];
                    batchIngredientDefName = first?.def?.defName;
                    batchInputCount = innerContainer.TotalStackCount;
                }
            }
        }

        public override void CompTick()
        {
            // The vanilla container ticks its contents so CompRottable keeps advancing in the original environment.
            base.CompTick();
            if (!HasBatch || manuallyPaused || batchSpoiled || parent?.Map == null || Find.TickManager == null) return;
            if (Find.TickManager.TicksGame % UpdateInterval != 0) return;
            TickDrying(UpdateInterval);
        }

        public override void CompTickRare()
        {
            // Building ticks are normal-frequency to preserve input rot. This fallback covers calls from rare-tick paths.
            base.CompTickRare();
        }

        public bool AcceptsIngredient(Thing thing)
        {
            DryingRecipeDef recipe = SelectedRecipe;
            if (!CanLoad || recipe == null || thing == null || thing.Destroyed || thing.def == null || !recipe.Accepts(thing.def)) return false;
            CompRottable rottable = thing.TryGetComp<CompRottable>();
            return rottable != null && rottable.Stage == RotStage.Fresh && rottable.RotProgressPct < 0.9f;
        }

        public int CountToLoad(Thing ingredient)
        {
            if (!AcceptsIngredient(ingredient)) return 0;
            DryingRecipeDef recipe = SelectedRecipe;
            int count = Math.Min(recipe.inputCount, ingredient.stackCount);
            return count >= recipe.minimumInputCount ? count : 0;
        }

        public bool TryLoadCarried(Pawn pawn, out string error)
        {
            error = null;
            Thing carried = pawn?.carryTracker?.CarriedThing;
            int count = carried?.stackCount ?? 0;
            if (!CanLoad || SelectedRecipe == null || carried == null || count <= 0 || count > SelectedRecipe.inputCount
                || count < SelectedRecipe.minimumInputCount || !AcceptsIngredient(carried))
            {
                error = "HR_Drying_LoadRejected".Translate().ToString();
                return false;
            }

            string ingredientId = carried.def.defName;
            if (!innerContainer.TryAddOrTransfer(carried, true))
            {
                error = "HR_Drying_LoadRejected".Translate().ToString();
                return false;
            }

            batchIngredientDefName = ingredientId;
            batchInputCount = innerContainer.TotalStackCount;
            dryingProgressTicks = 0;
            batchSpoiled = false;
            return true;
        }

        public void SelectRecipe(DryingRecipeDef recipe)
        {
            if (recipe == null || HasBatch || !Recipes.Contains(recipe)) return;
            selectedRecipeDefName = recipe.defName;
        }

        public void TogglePause() => manuallyPaused = !manuallyPaused;
        public void ToggleAutoRepeat() => autoRepeat = !autoRepeat;

        public bool TryCancelBatch(out string error)
        {
            error = null;
            if (parent?.Map == null)
            {
                error = "HR_Drying_NoMap".Translate().ToString();
                return false;
            }

            if (innerContainer.Any && !innerContainer.TryDropAll(parent.Position, parent.Map, ThingPlaceMode.Near))
            {
                error = "HR_Drying_ReturnBlocked".Translate().ToString();
                return false;
            }
            ResetBatch();
            return true;
        }

        public override string CompInspectStringExtra()
        {
            string containerText = base.CompInspectStringExtra();
            string details;
            if (batchSpoiled) details = "HR_Drying_Status_Spoiled".Translate().ToString();
            else if (!HasBatch)
            {
                details = "HR_Drying_Status_Empty".Translate(SelectedRecipe?.LabelCap ?? "HR_Drying_NoRecipe".Translate()).ToString();
            }
            else
            {
                Map map = parent?.Map;
                float temperature;
                string reason;
                float efficiency = DryingEnvironmentUtility.Efficiency(map, parent.Position, Props, out reason, out temperature);
                int totalTicks = DurationTicks(SelectedRecipe);
                int percent = totalTicks <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(100f * dryingProgressTicks / totalTicks), 0, 100);
                float days = Mathf.Max(0f, totalTicks - dryingProgressTicks) / TicksPerDay;
                ThingDef batchThing = DefDatabase<ThingDef>.GetNamedSilentFail(batchIngredientDefName);
                string batchLabel = batchThing?.LabelCap.ToString() ?? batchIngredientDefName;
                string status = batchCompleted ? "HR_Drying_Status_Completed".Translate().ToString()
                    : manuallyPaused ? "HR_Drying_Status_ManualPause".Translate().ToString()
                    : efficiency <= 0f ? "HR_Drying_Status_Paused".Translate(reason ?? "HR_Drying_Pause_Weather".Translate()).ToString()
                    : "HR_Drying_Status_Working".Translate().ToString();
                details = "HR_Drying_Status_Batch".Translate(batchLabel, batchInputCount)
                    + "\n" + "HR_Drying_Progress".Translate(percent, days.ToString("0.0"))
                    + "\n" + "HR_Drying_Environment".Translate(temperature.ToString("0.0"), (efficiency * 100f).ToString("0"))
                    + "\n" + status;
            }
            return string.IsNullOrWhiteSpace(containerText) ? details : containerText + "\n" + details;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (parent?.Faction != Faction.OfPlayer) yield break;

            Command_Action choose = new Command_Action
            {
                defaultLabel = "HR_Drying_ChooseFood".Translate(),
                defaultDesc = "HR_Drying_ChooseFood_Desc".Translate(),
                icon = TexCommand.ForbidOff,
                action = OpenRecipeMenu
            };
            choose.Disable(HasBatch ? "HR_Drying_CannotChangeDuringBatch".Translate() : null);
            yield return choose;

            yield return new Command_Action
            {
                defaultLabel = manuallyPaused ? "HR_Drying_Continue".Translate() : "HR_Drying_Pause".Translate(),
                defaultDesc = "HR_Drying_Pause_Desc".Translate(),
                icon = manuallyPaused ? TexCommand.ForbidOff : TexCommand.ForbidOn,
                action = TogglePause
            };
            yield return new Command_Action
            {
                defaultLabel = autoRepeat ? "HR_Drying_AutoRepeatOn".Translate() : "HR_Drying_AutoRepeatOff".Translate(),
                defaultDesc = "HR_Drying_AutoRepeat_Desc".Translate(),
                icon = TexCommand.DesirePower,
                action = ToggleAutoRepeat
            };

            if (HasBatch || batchSpoiled)
            {
                yield return new Command_Action
                {
                    defaultLabel = "HR_Drying_CancelBatch".Translate(),
                    defaultDesc = "HR_Drying_CancelBatch_Desc".Translate(),
                    icon = TexCommand.ForbidOn,
                    action = () =>
                    {
                        if (!TryCancelBatch(out string error)) Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                    }
                };
            }
        }

        private void OpenRecipeMenu()
        {
            List<FloatMenuOption> options = Recipes.Where(recipe => recipe.HasAvailableIngredients)
                .Select(recipe => new FloatMenuOption(recipe.LabelCap.ToString(), () => SelectRecipe(recipe)))
                .ToList();
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private void TickDrying(int delta)
        {
            if (batchCompleted)
            {
                Thing finished = innerContainer.FirstOrDefault();
                if (finished == null)
                {
                    ResetBatch();
                    return;
                }
                if (parent?.Map != null && innerContainer.TryDrop(finished, parent.Position, parent.Map, ThingPlaceMode.Near, out _))
                {
                    ResetBatch();
                    if (!autoRepeat) manuallyPaused = true;
                }
                return;
            }
            if (innerContainer.Count == 0)
            {
                if (HasBatch)
                {
                    batchIngredientDefName = null;
                    batchInputCount = 0;
                    dryingProgressTicks = 0;
                    batchCompleted = false;
                    batchSpoiled = true;
                    Messages.Message("HR_Drying_BatchSpoiled".Translate(), parent, MessageTypeDefOf.NegativeEvent, false);
                }
                else ResetBatch();
                return;
            }

            List<Thing> inputs = innerContainer.ToList();
            if (inputs.Any(item => item == null || item.Destroyed || item.def?.defName != batchIngredientDefName
                || item.TryGetComp<CompRottable>()?.Stage != RotStage.Fresh))
            {
                innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
                batchSpoiled = true;
                batchInputCount = 0;
                Messages.Message("HR_Drying_BatchSpoiled".Translate(), parent, MessageTypeDefOf.NegativeEvent, false);
                return;
            }

            DryingRecipeDef recipe = SelectedRecipe;
            if (recipe == null || recipe.outputThing == null) return;
            float efficiency = DryingEnvironmentUtility.Efficiency(parent.Map, parent.Position, Props, out _, out _);
            if (efficiency > 0f) dryingProgressTicks += Mathf.Max(1, Mathf.RoundToInt(delta * efficiency));
            int requiredTicks = DurationTicks(recipe);
            if (dryingProgressTicks >= requiredTicks) TryFinishBatch(recipe, inputs);
        }

        private void TryFinishBatch(DryingRecipeDef recipe, List<Thing> inputs)
        {
            float inputNutrition = inputs.Sum(item => item.GetStatValue(StatDefOf.Nutrition) * item.stackCount);
            float outputNutrition = recipe.outputThing.GetStatValueAbstract(StatDefOf.Nutrition);
            int outputCount = recipe.OutputCountFor(batchInputCount);
            if (outputNutrition > 0f)
                outputCount = Math.Min(outputCount, Mathf.FloorToInt(inputNutrition / outputNutrition));
            if (outputCount < 1)
            {
                innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
                batchSpoiled = true;
                batchInputCount = 0;
                Messages.Message("HR_Drying_BatchSpoiled".Translate(), parent, MessageTypeDefOf.NegativeEvent, false);
                return;
            }

            Thing output = ThingMaker.MakeThing(recipe.outputThing);
            output.stackCount = outputCount;
            innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
            batchIngredientDefName = recipe.outputThing.defName;
            batchInputCount = outputCount;
            batchCompleted = true;
            if (!innerContainer.TryAdd(output))
            {
                output.Destroy(DestroyMode.Vanish);
                batchSpoiled = true;
                batchCompleted = false;
                batchInputCount = 0;
                Messages.Message("HR_Drying_BatchSpoiled".Translate(), parent, MessageTypeDefOf.NegativeEvent, false);
                return;
            }
            TickDrying(0);
        }

        private static int DurationTicks(DryingRecipeDef recipe) => recipe == null
            ? TicksPerDay * 2
            : Mathf.Max(1, Mathf.RoundToInt(recipe.baseDryingDays * TicksPerDay));

        private void ResetBatch()
        {
            batchIngredientDefName = null;
            batchInputCount = 0;
            dryingProgressTicks = 0;
            batchSpoiled = false;
            batchCompleted = false;
        }
    }

    public sealed class WorkGiver_LoadDryingRack : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                ThingDef rack = DefDatabase<ThingDef>.GetNamedSilentFail("HR_DryingRack");
                return rack == null ? ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial) : ThingRequest.ForDef(rack);
            }
        }
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompDryingRack rack = thing?.TryGetComp<CompDryingRack>();
            if (rack == null || pawn?.Map == null || !pawn.IsColonistPlayerControlled || (!forced && pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
                || (!forced && (pawn.workSettings == null || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)))
                || rack.ManuallyPaused || !rack.CanLoad || thing.IsForbidden(pawn)
                || !pawn.CanReserveAndReach(thing, PathEndMode.Touch, MaxPathDanger(pawn))) return false;

            return FindIngredient(pawn, rack) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompDryingRack rack = thing?.TryGetComp<CompDryingRack>();
            Thing ingredient = rack == null ? null : FindIngredient(pawn, rack);
            int count = ingredient == null ? 0 : rack.CountToLoad(ingredient);
            if (count < 1) return null;
            Job job = JobMaker.MakeJob(DryingRackJobDefOf.HR_LoadDryingRack, thing, ingredient);
            job.count = count;
            return job;
        }

        private static Thing FindIngredient(Pawn pawn, CompDryingRack rack)
        {
            return pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                .Where(item => item != null && item.Spawned && !item.Destroyed && !item.IsForbidden(pawn)
                    && !item.IsBurning() && rack.CountToLoad(item) > 0
                    && pawn.CanReserveAndReach(item, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(item => pawn.Position.DistanceToSquared(item.Position))
                .FirstOrDefault();
        }
    }

    [DefOf]
    public static class DryingRackJobDefOf
    {
        public static JobDef HR_LoadDryingRack;
        static DryingRackJobDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(DryingRackJobDefOf));
    }

    public sealed class JobDriver_LoadDryingRack : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(TargetB, job, 1, job.count, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFailCondition(() => TargetA.Thing == null || TargetB.Thing == null
                || TargetA.Thing.Destroyed || TargetB.Thing.Destroyed
                || TargetA.Thing.Map != pawn.Map || TargetB.Thing.Map != pawn.Map);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, true);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                CompDryingRack rack = TargetA.Thing?.TryGetComp<CompDryingRack>();
                string error = null;
                if (rack == null || !rack.TryLoadCarried(pawn, out error))
                {
                    Messages.Message(error ?? "HR_Drying_LoadRejected".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                    EndJobWith(JobCondition.Incompletable);
                }
            });
        }
    }
}
