namespace HistoricalRimWorld.Forest
{
    /// <summary>Central balance values for ancient-forest growth and reproduction.</summary>
    public static class AncientForestGrowthSettings
    {
        public const int ReproductionCheckInterval = 120000;
        public const float BaseReproductionChance = 0.20f;
        public const float LivingToLivingMultiplier = 1.00f;
        public const float LivingToNormalMultiplier = 0.15f;
        public const float NormalToLivingMultiplier = 0.60f;
        public const float NormalToNormalMultiplier = 0.05f;
        public const int MinimumSeedDistance = 1;
        public const int MaximumSeedDistance = 5;
        public const int DensityRadius = 8;
        public const float LivingCoreDensityLimit = 0.35f;
        public const float LivingEdgeDensityLimit = 0.25f;
        public const float NormalSoilDensityLimit = 0.12f;
        public const int RecoveryCheckInterval = 600000;
        public const float LivingSoilRecoveryChance = 0.05f;
        public const int RecoveryCandidatesPerCheck = 30;
        public const int MaxRecoverySaplingsPerBatch = 3;
        public const int MaxSuccessfulReproductionsPerBatch = 20;
        public const int MaxParentsPerBatch = 60;
        public const int TickStaggerInterval = 2500;
        public const float LivingSoilGrowthMultiplier = 1.50f;

        // Debug-only controls are deliberately transient and never serialized.
        public static bool AcceleratedTesting;
        public static bool ShowReproductionOverlay;

        public static int EffectiveReproductionInterval => AcceleratedTesting ? 2500 : ReproductionCheckInterval;
        public static int EffectiveRecoveryInterval => AcceleratedTesting ? 5000 : RecoveryCheckInterval;
        public static float GetGrowthMultiplier(bool livingSoil)
        {
            float multiplier = livingSoil ? LivingSoilGrowthMultiplier : 1f;
            return AcceleratedTesting ? multiplier * 5f : multiplier;
        }
    }
}
