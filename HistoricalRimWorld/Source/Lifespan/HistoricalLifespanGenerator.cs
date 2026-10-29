using System;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Characters;
using Verse;

namespace HistoricalRimWorld.Lifespan
{
    public static class HistoricalLifespanGenerator
    {
        public static int GenerateLifespanYears(HistoricalPersonRecord person)
        {
            HistoricalLifespanPolicyDef policy = DefDatabase<HistoricalLifespanPolicyDef>.AllDefs
                .OrderBy(item => item.defName, StringComparer.Ordinal).FirstOrDefault();
            if (policy == null)
            {
                Log.Warning("[Historical RimWorld] No HistoricalLifespanPolicyDef was loaded; using fallback gameplay parameters (55–95, mean 72, SD 9).");
                return Clamp((int)Math.Round(Rand.Gaussian(72f, 9f)), 55, 95);
            }
            if (!policy.IsValid(out string error))
            {
                Log.Error("[Historical RimWorld] Invalid lifespan policy " + policy.defName + ": " + error + ". Using bounded fallback parameters.");
                return Clamp((int)Math.Round(Rand.Gaussian(72f, 9f)), 55, 95);
            }
            return Clamp((int)Math.Round(Rand.Gaussian(policy.meanYears, policy.standardDeviationYears)),
                policy.minimumYears, policy.maximumYears);
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
