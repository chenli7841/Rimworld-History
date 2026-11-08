using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Production.Fermentation
{
    /// <summary>One explicit, configurable food-fermentation process.</summary>
    public sealed class FermentationRecipeDef : Def
    {
        public List<ThingDef> allowedIngredientDefs;
        public ThingDef auxiliaryIngredientDef;
        public ThingDef outputThing;
        public int inputCount = 20;
        public int auxiliaryCount;
        public int outputCount = 16;
        public float processingDays = 4f;
        public float minTemperature = 5f;
        public float slowTemperature = 10f;
        public float normalTemperature = 20f;
        public float warmTemperature = 28f;
        public float maxTemperature = 35f;
        public bool enabledByDefault = true;

        private bool checkedIngredientAvailability;
        private bool hasAvailableIngredients;

        public bool HasAvailableIngredients
        {
            get
            {
                if (!checkedIngredientAvailability)
                {
                    hasAvailableIngredients = enabledByDefault && allowedIngredientDefs != null
                        && allowedIngredientDefs.Any(item => item != null && AcceptsIngredient(item));
                    checkedIngredientAvailability = true;
                }
                return hasAvailableIngredients;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (allowedIngredientDefs == null || allowedIngredientDefs.Count == 0 || allowedIngredientDefs.Any(item => item == null))
                yield return "Fermentation requires an explicit, valid ingredient whitelist.";
            if (outputThing == null) yield return "outputThing is required.";
            if (inputCount < 1 || outputCount < 1 || auxiliaryCount < 0) yield return "Recipe counts are invalid.";
            if (auxiliaryCount > 0 && auxiliaryIngredientDef == null) yield return "auxiliaryIngredientDef is required when auxiliaryCount is positive.";
            if (processingDays <= 0f) yield return "processingDays must be positive.";
            if (!(minTemperature < slowTemperature && slowTemperature < normalTemperature
                && normalTemperature < warmTemperature && warmTemperature < maxTemperature))
                yield return "Temperature thresholds must be in ascending order.";
            if (outputThing != null && outputThing.GetCompProperties<CompProperties_Rottable>() == null)
                yield return "Output must use CompRottable.";
        }

        public bool AcceptsIngredient(ThingDef ingredient)
        {
            if (!enabledByDefault || ingredient == null || allowedIngredientDefs == null || !allowedIngredientDefs.Contains(ingredient)
                || ingredient.ingestible == null || ingredient.GetCompProperties<CompProperties_Rottable>() == null) return false;
            FoodTypeFlags actualTypes = ingredient.ingestible.foodType;
            // Each recipe carries a strict whitelist. Also reject meat/corpses so a broad or
            // accidentally edited whitelist cannot turn into a generic meat fermentation path.
            return (actualTypes & (FoodTypeFlags.Meat | FoodTypeFlags.Corpse)) == 0;
        }
    }
}
