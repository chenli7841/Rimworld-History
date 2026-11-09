using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Production.Granary
{
    /// <summary>Explicit whitelist and balance settings for historical grain storage.</summary>
    public sealed class GranarySettingsDef : Def
    {
        public static GranarySettingsDef Settings => DefDatabase<GranarySettingsDef>.GetNamedSilentFail("HR_GranarySettings");

        public List<ThingDef> eligibleGrains = new List<ThingDef>();
        public float outdoorAnnualLossRate = 0.12f;
        public float indoorAnnualLossRate = 0.06f;
        public float sealedJarAnnualLossRate = 0.02f;
        public float granaryAnnualLossRate = 0.015f;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (eligibleGrains == null || eligibleGrains.Count == 0 || eligibleGrains.Any(thing => thing == null))
                yield return "Granary settings require a non-empty list of valid grain ThingDefs.";
            if (eligibleGrains != null && eligibleGrains.Where(thing => thing != null).Distinct().Count() != eligibleGrains.Count(thing => thing != null))
                yield return "Granary settings contain duplicate grain ThingDefs.";
            if (!ValidRate(outdoorAnnualLossRate) || !ValidRate(indoorAnnualLossRate)
                || !ValidRate(sealedJarAnnualLossRate) || !ValidRate(granaryAnnualLossRate))
                yield return "Granary annual loss rates must be between 0 and 1.";
        }

        private static bool ValidRate(float value) => value >= 0f && value <= 1f;
    }
}
