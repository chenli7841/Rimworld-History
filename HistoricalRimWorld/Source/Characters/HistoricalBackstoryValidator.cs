using System.Collections.Generic;
using System.Linq;
using System;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public static class HistoricalBackstoryValidator
    {
        public static List<string> ValidateAll()
        {
            List<string> errors = new List<string>();
            List<BackstoryDef> defs = DefDatabase<BackstoryDef>.AllDefsListForReading
                .Where(def => def?.defName != null && (def.defName.StartsWith("HR_C_", StringComparison.Ordinal)
                    || def.defName.StartsWith("HR_A_", StringComparison.Ordinal))).ToList();

            foreach (IGrouping<string, BackstoryDef> duplicate in defs.GroupBy(def => def.defName).Where(group => group.Count() > 1))
                errors.Add("Duplicate defName: " + duplicate.Key);

            foreach (BackstoryDef def in defs)
            {
                if (string.IsNullOrWhiteSpace(def.title)) errors.Add(def.defName + " has no title.");
                if (string.IsNullOrWhiteSpace(def.baseDesc)) errors.Add(def.defName + " has no description.");
                if (def.skillGains == null || def.skillGains.Count < 1 || def.skillGains.Count > 3)
                {
                    errors.Add(def.defName + " must affect 1-3 skills.");
                    continue;
                }

                int total = 0;
                foreach (SkillGain gain in def.skillGains)
                {
                    if (gain.skill == null || !DefDatabase<SkillDef>.AllDefsListForReading.Contains(gain.skill))
                        errors.Add(def.defName + " references a missing SkillDef.");
                    if (gain.amount < 0) errors.Add(def.defName + " has a negative skill gain.");
                    total += gain.amount;
                }
                if (total < 4 || total > 10) errors.Add(def.defName + " has skill total " + total + " outside 4-10.");

                HistoricalBackstoryDefExtension extension = def.GetModExtension<HistoricalBackstoryDefExtension>();
                if (extension?.identityWeights != null && extension.identityWeights.Any(item => item == null
                    || string.IsNullOrWhiteSpace(item.identity) || item.weight <= 0f))
                    errors.Add(def.defName + " has an invalid identity weight.");
            }

            int childhood = defs.Count(def => def.defName.StartsWith("HR_C_", StringComparison.Ordinal));
            int adulthood = defs.Count - childhood;
            if (childhood != 60) errors.Add("Expected 60 childhood backstories; found " + childhood + ".");
            if (adulthood != 120) errors.Add("Expected 120 adulthood backstories; found " + adulthood + ".");
            return errors;
        }
    }
}
