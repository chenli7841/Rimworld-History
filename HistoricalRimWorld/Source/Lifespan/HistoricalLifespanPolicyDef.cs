using Verse;

namespace HistoricalRimWorld.Lifespan
{
    public sealed class HistoricalLifespanPolicyDef : Def
    {
        public int minimumYears = 55;
        public int maximumYears = 95;
        public float meanYears = 72f;
        public float standardDeviationYears = 9f;

        public bool IsValid(out string error)
        {
            error = null;
            if (minimumYears < 1) error = "minimumYears must be positive.";
            else if (maximumYears < minimumYears) error = "maximumYears must be at least minimumYears.";
            else if (meanYears < minimumYears || meanYears > maximumYears) error = "meanYears must be within the configured range.";
            else if (standardDeviationYears < 0f) error = "standardDeviationYears cannot be negative.";
            return error == null;
        }
    }
}
