using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public enum HistoricalTechnologyBonusType
    {
        MeleeDamage,
        RangedDamage,
        MeleeArmorPenetration,
        RangedArmorPenetration,
        ArmorSharp,
        ArmorBlunt,
        ArmorHeat,
        RangedCooldown,
        RangedWarmup,
        InsulationCold,
        InsulationHeat
    }

    public sealed class HistoricalTechnologyBonusDef : Def
    {
        public HistoricalTechnologyBonusType bonusType;
        public float bonusPercent;
        public List<ThingDef> targetThingDefs;
        public List<string> targetTags;
        public List<ThingDef> allowedMaterials;
        public bool requireEquipped = true;
        public bool requireSharpMeleeTool;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (bonusPercent <= -1f) yield return "bonusPercent must be greater than -1.";
            if ((targetThingDefs == null || targetThingDefs.Count == 0)
                && (targetTags == null || targetTags.Count == 0))
                yield return "At least one target ThingDef or target tag is required.";
            if (targetThingDefs != null && targetThingDefs.Any(item => item == null)) yield return "targetThingDefs contains an unresolved ThingDef.";
            if (allowedMaterials != null && allowedMaterials.Any(item => item == null)) yield return "allowedMaterials contains an unresolved ThingDef.";
            if (targetTags != null && targetTags.Any(string.IsNullOrWhiteSpace)) yield return "targetTags contains a blank tag.";
            if (requireSharpMeleeTool && bonusType != HistoricalTechnologyBonusType.MeleeArmorPenetration)
                yield return "requireSharpMeleeTool is only valid for MeleeArmorPenetration bonuses.";
            if (bonusType != HistoricalTechnologyBonusType.MeleeDamage
                && bonusType != HistoricalTechnologyBonusType.MeleeArmorPenetration
                && bonusType != HistoricalTechnologyBonusType.ArmorSharp
                && bonusType != HistoricalTechnologyBonusType.ArmorBlunt)
                yield return "This bonus type is reserved for a later implementation phase and has no runtime hook in Phase 5.";
        }
    }

    public sealed class HistoricalTechnologyBonusLimitsDef : Def
    {
        public float meleeDamage = 0.30f;
        public float rangedDamage = 0.30f;
        public float meleeArmorPenetration = 0.25f;
        public float rangedArmorPenetration = 0.25f;
        public float armor = 0.25f;
        public float rangedTiming = 0.20f;
        public float insulation = 0.25f;

        public float LimitFor(HistoricalTechnologyBonusType type)
        {
            switch (type)
            {
                case HistoricalTechnologyBonusType.MeleeDamage: return meleeDamage;
                case HistoricalTechnologyBonusType.RangedDamage: return rangedDamage;
                case HistoricalTechnologyBonusType.MeleeArmorPenetration: return meleeArmorPenetration;
                case HistoricalTechnologyBonusType.RangedArmorPenetration: return rangedArmorPenetration;
                case HistoricalTechnologyBonusType.ArmorSharp:
                case HistoricalTechnologyBonusType.ArmorBlunt:
                case HistoricalTechnologyBonusType.ArmorHeat: return armor;
                case HistoricalTechnologyBonusType.RangedCooldown:
                case HistoricalTechnologyBonusType.RangedWarmup: return rangedTiming;
                case HistoricalTechnologyBonusType.InsulationCold:
                case HistoricalTechnologyBonusType.InsulationHeat: return insulation;
                default: return 0f;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (new[] { meleeDamage, rangedDamage, meleeArmorPenetration, rangedArmorPenetration, armor, rangedTiming, insulation }
                .Any(value => value < 0f || value >= 1f))
                yield return "Bonus caps must be in the range [0, 1).";
        }
    }

    public sealed class HistoricalEquipmentTagsExtension : DefModExtension
    {
        public List<string> tags;
    }
}
