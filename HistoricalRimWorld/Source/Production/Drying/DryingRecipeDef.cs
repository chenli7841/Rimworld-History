using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Production.Drying
{
    public enum DryingFoodKind
    {
        Meat,
        Fish,
        Vegetables
    }

    /// <summary>One configured food-drying batch. Ingredient freshness is carried by the original Thing stacks.</summary>
    public sealed class DryingRecipeDef : Def
    {
        public DryingFoodKind foodKind;
        public List<ThingDef> allowedIngredientDefs;
        public ThingDef outputThing;
        public int inputCount = 20;
        public int minimumInputCount = 5;
        public int outputCount = 12;
        public float baseDryingDays = 2f;
        private bool checkedIngredientAvailability;
        private bool hasAvailableIngredients;
        public bool HasAvailableIngredients
        {
            get
            {
                if (!checkedIngredientAvailability)
                {
                    hasAvailableIngredients = DefDatabase<ThingDef>.AllDefsListForReading.Any(Accepts);
                    checkedIngredientAvailability = true;
                }
                return hasAvailableIngredients;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (outputThing == null) yield return "outputThing is required.";
            if (inputCount < 1) yield return "inputCount must be positive.";
            if (minimumInputCount < 1 || minimumInputCount > inputCount) yield return "minimumInputCount must be between 1 and inputCount.";
            if (outputCount < 1) yield return "outputCount must be positive.";
            if (baseDryingDays <= 0f) yield return "baseDryingDays must be positive.";
            if (foodKind == DryingFoodKind.Vegetables && (allowedIngredientDefs == null || !allowedIngredientDefs.Any(item => item != null)))
                yield return "Vegetable recipes require an explicit ingredient whitelist.";
        }

        public bool Accepts(ThingDef ingredient)
        {
            if (ingredient?.ingestible == null) return false;
            CompProperties_Rottable rotProperties = ingredient.GetCompProperties<CompProperties_Rottable>();
            if (rotProperties == null) return false;

            switch (foodKind)
            {
                case DryingFoodKind.Meat:
                    return HasFoodType(ingredient, FoodTypeFlags.Meat)
                        && !HasFoodType(ingredient, FoodTypeFlags.Corpse)
                        && !HasForbiddenMeatName(ingredient.defName)
                        && ingredient.defName.IndexOf("Fish", System.StringComparison.OrdinalIgnoreCase) < 0;
                case DryingFoodKind.Fish:
                    return HasFoodType(ingredient, FoodTypeFlags.Meat)
                        && ingredient.defName.IndexOf("Fish", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && !HasForbiddenMeatName(ingredient.defName);
                case DryingFoodKind.Vegetables:
                    return allowedIngredientDefs != null && allowedIngredientDefs.Contains(ingredient)
                        && HasFoodType(ingredient, FoodTypeFlags.VegetableOrFruit);
                default:
                    return false;
            }
        }

        public int OutputCountFor(int actualInputCount)
        {
            if (actualInputCount <= 0) return 0;
            return System.Math.Max(1, (int)System.Math.Floor((double)outputCount * actualInputCount / inputCount));
        }

        private static bool HasFoodType(ThingDef thing, FoodTypeFlags type) => (thing.ingestible.foodType & type) != 0;

        private static bool HasForbiddenMeatName(string defName)
        {
            string name = defName ?? string.Empty;
            return name.IndexOf("Human", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Insect", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Megaspider", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Megascarab", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Spelopede", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
