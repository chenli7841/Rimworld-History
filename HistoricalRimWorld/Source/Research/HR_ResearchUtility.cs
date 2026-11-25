using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Research
{
    public static class HR_ResearchUtility
    {
        private const float IntellectualSkillBase = 0.08f;
        private const float SkillBonusPerLevel = 0.115f;
        private static readonly HashSet<string> ModernResearchBenchNames = new HashSet<string>
        {
            "SimpleResearchBench", "HiTechResearchBench"
        };
        private static bool configValidated;

        public static HR_ResearchProjectExtension GetExtension(ResearchProjectDef project) =>
            project?.GetModExtension<HR_ResearchProjectExtension>();

        public static bool IsHistoricalFacility(ThingDef def) =>
            def?.GetModExtension<HR_ResearchFacilityExtension>() != null;

        public static bool IsModernResearchBench(BuildableDef def) =>
            def != null && ModernResearchBenchNames.Contains(def.defName);

        public static bool IsFacilityAllowed(ResearchProjectDef project, Building_ResearchBench bench)
        {
            HR_ResearchProjectExtension projectExtension = GetExtension(project);
            if (projectExtension == null) return true;
            HR_ResearchFacilityExtension facility = bench?.def?.GetModExtension<HR_ResearchFacilityExtension>();
            if (facility == null || projectExtension.allowedFacilities == null) return false;
            return projectExtension.allowedFacilities.Contains(facility.facilityType);
        }

        public static bool CanResearchAt(Pawn pawn, ResearchProjectDef project, Building_ResearchBench bench)
        {
            if (pawn == null || project == null || bench == null || !bench.Spawned || bench.Destroyed
                || pawn.Map != bench.Map || !pawn.CanReach(bench, PathEndMode.InteractionCell, Danger.Some))
                return false;
            return IsFacilityAllowed(project, bench);
        }

        public static float SkillReplacementFactor(Pawn pawn, ResearchProjectDef project)
        {
            if (pawn?.skills == null || GetExtension(project) == null) return 1f;
            EnsureConfigurationValidated();
            List<HR_ResearchSkillWeight> weights = GetSafeWeights(GetExtension(project));
            float effectiveSkill = 0f;
            float totalWeight = 0f;
            foreach (HR_ResearchSkillWeight weight in weights)
            {
                if (weight?.skill == null || !IsValidWeight(weight.weight)) continue;
                SkillRecord record = pawn.skills.GetSkill(weight.skill);
                int level = record?.Level ?? 0;
                effectiveSkill += Math.Max(0, Math.Min(20, level)) * weight.weight;
                totalWeight += weight.weight;
            }
            if (totalWeight <= 0.0001f) return 1f;
            effectiveSkill /= totalWeight;

            SkillDef intellectual = DefDatabase<SkillDef>.GetNamedSilentFail("Intellectual");
            float intellectualLevel = intellectual == null ? 0f : Math.Max(0, Math.Min(20, pawn.skills.GetSkill(intellectual)?.Level ?? 0));
            float originalSkillFactor = IntellectualSkillBase + SkillBonusPerLevel * intellectualLevel;
            float weightedSkillFactor = IntellectualSkillBase + SkillBonusPerLevel * effectiveSkill;
            return originalSkillFactor > 0.0001f ? weightedSkillFactor / originalSkillFactor : 1f;
        }

        public static float FacilityAndRackFactor(Building_ResearchBench bench)
        {
            if (bench?.def == null) return 1f;
            HR_ResearchFacilityExtension facility = bench.def.GetModExtension<HR_ResearchFacilityExtension>();
            float factor = facility == null || !IsFinitePositive(facility.researchSpeedMultiplier)
                ? 1f : facility.researchSpeedMultiplier;
            if (facility?.facilityType == HR_ResearchFacilityType.WritingDesk)
            {
                int racks = bench.Map?.GetComponent<HR_ResearchFacilityMapComponent>()?.NearbyRackCount(bench) ?? 0;
                factor *= 1f + Math.Min(2, Math.Max(0, racks)) * 0.05f;
            }
            return factor;
        }

        public static void InvalidateMapCache(Map map) => map?.GetComponent<HR_ResearchFacilityMapComponent>()?.Invalidate();

        public static string GetFacilityDisplay(HR_ResearchFacilityType type) =>
            "HR_ResearchFacility_" + type;

        private static List<HR_ResearchSkillWeight> GetSafeWeights(HR_ResearchProjectExtension extension)
        {
            List<HR_ResearchSkillWeight> configured = extension?.skillWeights;
            if (configured == null || configured.Count == 0
                || configured.Any(item => item?.skill == null || !IsValidWeight(item.weight))
                || Math.Abs(configured.Sum(item => item.weight) - 1f) > 0.01f)
            {
                SkillDef intellectual = DefDatabase<SkillDef>.GetNamedSilentFail("Intellectual");
                return intellectual == null ? new List<HR_ResearchSkillWeight>()
                    : new List<HR_ResearchSkillWeight> { new HR_ResearchSkillWeight { skill = intellectual, weight = 1f } };
            }
            return configured;
        }

        private static bool IsValidWeight(float weight) => IsFinitePositiveOrZero(weight) && weight <= 1f;
        private static bool IsFinitePositive(float value) => IsFinitePositiveOrZero(value) && value > 0f;
        private static bool IsFinitePositiveOrZero(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static void EnsureConfigurationValidated()
        {
            if (configValidated) return;
            configValidated = true;
            foreach (ResearchProjectDef project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                HR_ResearchProjectExtension extension = GetExtension(project);
                if (extension == null) continue;
                if (extension.allowedFacilities == null || extension.allowedFacilities.Count == 0)
                    Log.Warning("[Historical RimWorld] HR-050: " + project.defName + " has no allowed research facility; it will use the intellectual-only skill fallback but cannot use a research bench.");
                if (extension.skillWeights == null || extension.skillWeights.Count == 0
                    || extension.skillWeights.Any(item => item?.skill == null || !IsValidWeight(item.weight))
                    || Math.Abs(extension.skillWeights.Sum(item => item.weight) - 1f) > 0.01f)
                    Log.Warning("[Historical RimWorld] HR-050: invalid skill weights for " + project.defName + "; using Intellectual at 100%.");
            }
        }
    }
}
