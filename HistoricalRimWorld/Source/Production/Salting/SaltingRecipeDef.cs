using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Production.Salting
{
    public enum SaltingFoodKind
    {
        Meat,
        Fish,
        Vegetables
    }

    public sealed class SaltingRecipeDef : Def
    {
        public SaltingFoodKind foodKind;
        public List<ThingDef> allowedIngredientDefs;
        public ThingDef saltThing;
        public ThingDef outputThing;
        public int ingredientCount = 20;
        public int saltCount = 4;
        public int outputCount = 14;
        public float processingDays = 3f;
        private bool checkedIngredientAvailability;
        private bool hasAvailableIngredients;

        public bool HasAvailableIngredients
        {
            get
            {
                if (!checkedIngredientAvailability)
                {
                    hasAvailableIngredients = DefDatabase<ThingDef>.AllDefsListForReading.Any(AcceptsIngredient);
                    checkedIngredientAvailability = true;
                }
                return hasAvailableIngredients;
            }
        }

        public bool AcceptsIngredient(ThingDef ingredient)
        {
            if (ingredient?.ingestible == null || ingredient.GetCompProperties<CompProperties_Rottable>() == null) return false;
            FoodTypeFlags foodType = ingredient.ingestible.foodType;
            switch (foodKind)
            {
                case SaltingFoodKind.Meat:
                    return (foodType & FoodTypeFlags.Meat) != 0 && (foodType & FoodTypeFlags.Corpse) == 0
                        && !IsForbiddenMeat(ingredient.defName)
                        && ingredient.defName.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) < 0;
                case SaltingFoodKind.Fish:
                    return (foodType & FoodTypeFlags.Meat) != 0 && (foodType & FoodTypeFlags.Corpse) == 0
                        && ingredient.defName.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) >= 0
                        && !IsForbiddenMeat(ingredient.defName);
                case SaltingFoodKind.Vegetables:
                    return allowedIngredientDefs != null && allowedIngredientDefs.Contains(ingredient)
                        && (foodType & FoodTypeFlags.VegetableOrFruit) != 0;
                default:
                    return false;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (saltThing == null) yield return "saltThing is required.";
            if (outputThing == null) yield return "outputThing is required.";
            if (ingredientCount < 1 || saltCount < 1 || outputCount < 1) yield return "Recipe counts must be positive.";
            if (processingDays <= 0f) yield return "processingDays must be positive.";
            if (foodKind == SaltingFoodKind.Vegetables && (allowedIngredientDefs == null || allowedIngredientDefs.Count == 0))
                yield return "Vegetable recipes require an explicit ingredient whitelist.";
            if (outputThing != null && outputThing.GetCompProperties<CompProperties_Rottable>() == null)
                yield return "Output must use CompRottable.";
        }

        private static bool IsForbiddenMeat(string defName)
        {
            string name = defName ?? string.Empty;
            return name.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Insect", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Megaspider", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Megascarab", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Spelopede", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
