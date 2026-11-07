using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public enum HistoricalPracticeType
    {
        CraftItemCount,
        CraftItemQualityCount,
        CraftsmanshipExperience,
        BuildStructureCount,
        HarvestCount,
        MiningCount,
        MedicalTreatmentCount
    }

    public sealed class HistoricalPracticeRequirementDef : Def
    {
        public HistoricalPracticeType requirementType;
        public List<ThingDef> targetThingDefs;
        public List<string> targetTags;
        public List<ThingDef> allowedMaterials;
        public bool hasMinimumQuality;
        public QualityCategory minimumQuality;
        public int requiredCount;
        public float requiredExperience;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (requiredCount < 0) yield return "requiredCount cannot be negative.";
            if (requiredExperience < 0f) yield return "requiredExperience cannot be negative.";
            if ((targetThingDefs == null || targetThingDefs.Count == 0)
                && (targetTags == null || targetTags.Count == 0))
                yield return "At least one target ThingDef or target tag is required.";
            if (requirementType == HistoricalPracticeType.CraftItemQualityCount && !hasMinimumQuality)
                yield return "CraftItemQualityCount requires hasMinimumQuality.";
            if (targetThingDefs != null && targetThingDefs.Any(item => item == null)) yield return "targetThingDefs contains an unresolved ThingDef.";
            if (allowedMaterials != null && allowedMaterials.Any(item => item == null)) yield return "allowedMaterials contains an unresolved ThingDef.";
            if (targetTags != null && targetTags.Any(string.IsNullOrWhiteSpace)) yield return "targetTags contains a blank tag.";
        }
    }

    public sealed class HistoricalPracticeTagsExtension : DefModExtension
    {
        public List<string> tags;
    }

    public sealed class HistoricalPracticeLearningRecipeExtension : DefModExtension
    {
        public string researchDefName;
    }

    public sealed class HistoricalCraftsmanshipExperienceDef : Def
    {
        public float awful;
        public float poor = 0.5f;
        public float normal = 1f;
        public float good = 2f;
        public float excellent = 5f;
        public float masterwork = 15f;
        public float legendary = 40f;

        public float For(QualityCategory quality)
        {
            switch (quality)
            {
                case QualityCategory.Awful: return awful;
                case QualityCategory.Poor: return poor;
                case QualityCategory.Normal: return normal;
                case QualityCategory.Good: return good;
                case QualityCategory.Excellent: return excellent;
                case QualityCategory.Masterwork: return masterwork;
                case QualityCategory.Legendary: return legendary;
                default: return 0f;
            }
        }
    }
}
