using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Patches.Technology;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public static class HistoricalTechnologyBonusService
    {
        private struct CacheKey : IEquatable<CacheKey>
        {
            public string FactionId;
            public string ThingDefName;
            public string StuffDefName;
            public HistoricalTechnologyBonusType Type;
            public bool Equipped;
            public bool SharpTool;

            public bool Equals(CacheKey other) => FactionId == other.FactionId
                && ThingDefName == other.ThingDefName && StuffDefName == other.StuffDefName
                && Type == other.Type && Equipped == other.Equipped && SharpTool == other.SharpTool;
            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = FactionId?.GetHashCode() ?? 0;
                    hash = (hash * 397) ^ (ThingDefName?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ (StuffDefName?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ (int)Type;
                    hash = (hash * 397) ^ Equipped.GetHashCode();
                    return (hash * 397) ^ SharpTool.GetHashCode();
                }
            }
        }

        private static readonly Dictionary<CacheKey, float> cache = new Dictionary<CacheKey, float>();
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            HistoricalPracticeService.HistoricalTechnologyCompleted += OnTechnologyCompleted;
            ClearCache();
        }

        public static void ClearCache() => cache.Clear();

        public static float GetBonus(Faction faction, Thing equipment, HistoricalTechnologyBonusType bonusType,
            bool equipped, bool sharpTool = false)
        {
            if (!HistoricalTechnologyManager.IsActive || faction == null || equipment?.def == null) return 0f;
            CacheKey key = new CacheKey
            {
                FactionId = faction.GetUniqueLoadID(),
                ThingDefName = equipment.def.defName,
                StuffDefName = equipment.Stuff?.defName ?? string.Empty,
                Type = bonusType,
                Equipped = equipped,
                SharpTool = sharpTool
            };
            if (cache.TryGetValue(key, out float cached)) return cached;
            float total = 0f;
            foreach (HistoricalTechnologyBonusDef bonus in DefDatabase<HistoricalTechnologyBonusDef>.AllDefsListForReading)
            {
                if (bonus == null || bonus.bonusType != bonusType || !HistoricalEquipmentMatcher.Matches(equipment, bonus)) continue;
                if (bonus.requireEquipped && !equipped) continue;
                if (bonus.requireSharpMeleeTool && !sharpTool) continue;
                if (FactionHasBonusTechnology(faction, bonus)) total += bonus.bonusPercent;
            }
            float limit = DefDatabase<HistoricalTechnologyBonusLimitsDef>.GetNamedSilentFail("HR_TechnologyBonusLimits_Default")?.LimitFor(bonusType) ?? 0.25f;
            float result = Mathf.Clamp(total, -limit, limit);
            cache[key] = result;
            return result;
        }

        public static IEnumerable<HistoricalTechnologyBonusDef> BonusesForResearch(ResearchProjectDef research) =>
            BonusTechnologyPairs().Where(pair => pair.Research == research).Select(pair => pair.Bonus).Distinct();

        public static string DescribePlayerBonusesForEquipment(Thing equipment)
        {
            if (!HistoricalTechnologyManager.IsActive || equipment?.def == null) return null;
            List<string> rows = new List<string>();
            foreach (BonusTechnologyPair pair in BonusTechnologyPairs())
            {
                if (!HistoricalEquipmentMatcher.Matches(equipment, pair.Bonus)) continue;
                bool active = PlayerHasBonusTechnology(pair.Research);
                rows.Add(pair.Bonus.LabelCap + " (" + Translator.Translate("HR_TechBonus_Type_" + pair.Bonus.bonusType)
                    + "): " + FormatPercent(pair.Bonus.bonusPercent)
                    + " — " + (active ? "HR_TechBonus_Active".Translate() : "HR_TechBonus_WhenMastered".Translate()));
            }
            if (rows.Count == 0) return null;
            return "HR_TechBonus_EquipmentHeader".Translate() + "\n"
                + "HR_TechBonus_EquipmentUsage".Translate() + "\n" + string.Join("\n", rows.Distinct());
        }

        public static string DescribeBonus(HistoricalTechnologyBonusDef bonus, bool completed)
        {
            if (bonus == null) return null;
            return string.Format("HR_TechBonus_ResearchLine".Translate(), bonus.LabelCap,
                Translator.Translate("HR_TechBonus_Type_" + bonus.bonusType),
                FormatPercent(bonus.bonusPercent), completed ? "HR_TechBonus_Active".Translate() : "HR_TechBonus_WhenMastered".Translate());
        }

        internal static bool MatchesRequirementTag(ThingDef thingDef, string tag)
        {
            HistoricalPracticeTagsExtension practice = thingDef?.GetModExtension<HistoricalPracticeTagsExtension>();
            HistoricalEquipmentTagsExtension equipment = thingDef?.GetModExtension<HistoricalEquipmentTagsExtension>();
            return practice?.tags?.Contains(tag) == true || equipment?.tags?.Contains(tag) == true;
        }

        internal static bool PlayerHasBonusTechnology(ResearchProjectDef research)
        {
            if (research == null || !HistoricalTechnologyManager.IsActive) return false;
            // Phase 4 practice/mastery is authoritative; completing vanilla research alone does not grant this bonus.
            return HistoricalTechnologyManager.HasFactionMasteredTechnology(Faction.OfPlayerSilentFail, research);
        }

        private static bool FactionHasBonusTechnology(Faction faction, HistoricalTechnologyBonusDef bonus)
        {
            foreach (BonusTechnologyPair pair in BonusTechnologyPairs())
            {
                if (pair.Bonus != bonus) continue;
                if (faction == Faction.OfPlayerSilentFail)
                {
                    if (PlayerHasBonusTechnology(pair.Research)) return true;
                }
                else if (HistoricalTechnologyManager.HasFactionMasteredTechnology(faction, pair.Research)) return true;
            }
            return false;
        }

        private static IEnumerable<BonusTechnologyPair> BonusTechnologyPairs()
        {
            foreach (ResearchProjectDef project in HistoricalTechnologyTimelineLayout.HistoricalProjects)
            {
                HistoricalTechExtension extension = HistoricalTechnologyTimelineLayout.GetExtension(project);
                if (extension?.TechnologyBonusIds == null) continue;
                HistoricalTechnologyDef technology = HistoricalTechnologyTimelineLayout.GetTechnology(project);
                if (technology == null) continue;
                foreach (string id in extension.TechnologyBonusIds.Distinct(StringComparer.Ordinal))
                {
                    HistoricalTechnologyBonusDef bonus = DefDatabase<HistoricalTechnologyBonusDef>.GetNamedSilentFail(id);
                    if (bonus != null) yield return new BonusTechnologyPair(project, technology, bonus);
                }
            }
        }

        private static string FormatPercent(float amount) => (amount >= 0f ? "+" : string.Empty)
            + (amount * 100f).ToString("0.#") + "%";

        private static void OnTechnologyCompleted(ResearchProjectDef project) => ClearCache();

        private sealed class BonusTechnologyPair
        {
            public ResearchProjectDef Research { get; }
            public HistoricalTechnologyDef Technology { get; }
            public HistoricalTechnologyBonusDef Bonus { get; }
            public BonusTechnologyPair(ResearchProjectDef research, HistoricalTechnologyDef technology, HistoricalTechnologyBonusDef bonus)
            { Research = research; Technology = technology; Bonus = bonus; }
        }
    }

    public static class HistoricalEquipmentMatcher
    {
        public static bool Matches(Thing equipment, HistoricalTechnologyBonusDef bonus)
        {
            if (equipment?.def == null || bonus == null) return false;
            bool defMatches = bonus.targetThingDefs != null && bonus.targetThingDefs.Contains(equipment.def);
            bool tagMatches = bonus.targetTags != null && bonus.targetTags.Any(tag =>
                HistoricalTechnologyBonusService.MatchesRequirementTag(equipment.def, tag));
            if (!defMatches && !tagMatches) return false;
            if ((bonus.bonusType == HistoricalTechnologyBonusType.MeleeDamage
                || bonus.bonusType == HistoricalTechnologyBonusType.MeleeArmorPenetration)
                && !equipment.def.IsMeleeWeapon) return false;
            if ((bonus.bonusType == HistoricalTechnologyBonusType.RangedDamage
                || bonus.bonusType == HistoricalTechnologyBonusType.RangedArmorPenetration
                || bonus.bonusType == HistoricalTechnologyBonusType.RangedCooldown
                || bonus.bonusType == HistoricalTechnologyBonusType.RangedWarmup)
                && !equipment.def.IsRangedWeapon) return false;
            if ((bonus.bonusType == HistoricalTechnologyBonusType.ArmorSharp
                || bonus.bonusType == HistoricalTechnologyBonusType.ArmorBlunt
                || bonus.bonusType == HistoricalTechnologyBonusType.ArmorHeat)
                && equipment.def.apparel == null) return false;
            if (bonus.allowedMaterials != null && bonus.allowedMaterials.Count > 0
                && (equipment.Stuff == null || !bonus.allowedMaterials.Contains(equipment.Stuff))) return false;
            return true;
        }
    }
}
