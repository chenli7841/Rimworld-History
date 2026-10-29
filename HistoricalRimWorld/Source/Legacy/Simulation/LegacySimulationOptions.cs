namespace HistoricalRimWorld.Legacy.Simulation
{
    public sealed class LegacySimulationOptions
    {
        public int Seed;
        public string TargetScenarioId;
        public int MaximumGeneratedPersons = 500;
        public int MaximumActiveDescendants = 200;
        public bool AllowMigration = true;
        public int MaximumYears = 1000;
        public float AnnualBirthProbability = 0.12f;
        public float AnnualAdultMortality = 0.012f;
        public float AnnualChildMortality = 0.035f;
        public float AnnualMarriageProbability = 0.12f;
    }
}
