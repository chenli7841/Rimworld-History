using Verse;

namespace HistoricalRimWorld.Legacy.Simulation
{
    public sealed class LegacyPopulationPolicyDef : Def
    {
        public string targetScenarioId;
        public float annualBirthProbability = 0.12f;
        public float annualAdultMortality = 0.012f;
        public float annualChildMortality = 0.035f;
        public float annualMarriageProbability = 0.12f;
        public float annualMigrationProbability = 0.006f;
        public int minimumMarriageAge = 18;
        public int maximumChildbearingAge = 40;
        public int maximumGeneratedPersons = 500;
        public int maximumActiveDescendants = 200;
        public int maximumSimulationYears = 1000;
        public int minimumNaturalLifespanYears = 50;
        public int maximumNaturalLifespanYears = 95;
        public float meanNaturalLifespanYears = 72f;
        public float naturalLifespanStandardDeviation = 9f;

        public bool IsValid(out string error)
        {
            error = null;
            if (!Probability(annualBirthProbability) || !Probability(annualAdultMortality) || !Probability(annualChildMortality)
                || !Probability(annualMarriageProbability) || !Probability(annualMigrationProbability)) error = "All annual probabilities must be finite and between zero and one.";
            else if (minimumMarriageAge < 14 || maximumChildbearingAge < minimumMarriageAge || maximumGeneratedPersons < 1
                || maximumActiveDescendants < 1 || maximumSimulationYears < 0 || minimumNaturalLifespanYears < 1
                || maximumNaturalLifespanYears < minimumNaturalLifespanYears || meanNaturalLifespanYears < minimumNaturalLifespanYears
                || meanNaturalLifespanYears > maximumNaturalLifespanYears || naturalLifespanStandardDeviation < 0f)
                error = "Population policy limits are inconsistent.";
            return error == null;
        }
        private static bool Probability(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
    }
}
