using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Production.Smoking
{
    public enum SmokingFoodKind
    {
        Meat,
        Fish
    }

    /// <summary>A configured, freshness-preserving smoking batch.</summary>
    public sealed class SmokingRecipeDef : Def
    {
        public SmokingFoodKind foodKind;
        public List<ThingDef> allowedIngredientDefs;
        public ThingDef outputThing;
        public int inputCount = 20;
        public int minimumInputCount = 5;
        public int outputCount = 15;
        public float processingDays = 1.5f;
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
            if (processingDays <= 0f) yield return "processingDays must be positive.";
            if (allowedIngredientDefs != null && allowedIngredientDefs.Any(item => item == null)) yield return "allowedIngredientDefs contains a missing ThingDef.";
            if (outputThing != null && outputThing.GetCompProperties<CompProperties_Rottable>() == null)
                yield return "Output must use CompRottable.";
        }

        public bool Accepts(ThingDef ingredient)
        {
            if (ingredient?.ingestible == null || ingredient.GetCompProperties<CompProperties_Rottable>() == null) return false;
            FoodTypeFlags types = ingredient.ingestible.foodType;
            if ((types & FoodTypeFlags.Meat) == 0 || (types & FoodTypeFlags.Corpse) != 0) return false;
            string name = ingredient.defName ?? string.Empty;
            bool fish = name.IndexOf("Fish", StringComparison.OrdinalIgnoreCase) >= 0;
            if (foodKind == SmokingFoodKind.Fish)
                return fish && !IsForbiddenMeat(name) && (allowedIngredientDefs == null || allowedIngredientDefs.Count == 0 || allowedIngredientDefs.Contains(ingredient));
            return !fish && !IsForbiddenMeat(name)
                && (allowedIngredientDefs == null || allowedIngredientDefs.Count == 0 || allowedIngredientDefs.Contains(ingredient));
        }

        public int OutputCountFor(int actualInputCount)
        {
            if (actualInputCount <= 0) return 0;
            return Math.Max(1, (int)Math.Floor((double)outputCount * actualInputCount / inputCount));
        }

        private static bool IsForbiddenMeat(string defName)
        {
            return defName.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Insect", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Megaspider", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Megascarab", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Spelopede", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
