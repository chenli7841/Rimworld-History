using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Forest
{
    public enum AncientWoodcuttingToolTier
    {
        None = 0,
        Stone = 1,
        Bronze = 2,
        Iron = 3
    }

    public sealed class HR_WoodcuttingToolExtension : DefModExtension
    {
        public AncientWoodcuttingToolTier toolTier;
        public float ancientTreeCutSpeedFactor = 1f;
        public float workPerHitPoint = 500f;
    }

    public sealed class AncientTreeRareDrop
    {
        public ThingDef resourceDef;
        public float chance;
        public IntRange countRange = new IntRange(1, 1);
    }

    /// <summary>Data marker and future-facing tuning fields for ancient tree PlantDefs.</summary>
    public sealed class HR_AncientTreeExtension : DefModExtension
    {
        public bool isAncientTree = true;
        public string forestSpeciesId;
        public int requiredToolTier;
        public float erosionResistance;
        public string rareResourcePoolId;
        public float cutWorkMultiplier = 1f;
        public float woodYieldMultiplier = 1f;
        // Mature yield of the ordinary tree used as the species baseline.
        public float ordinaryTreeHarvestYield = 46f;
        public List<AncientTreeRareDrop> rareResourceDrops;
    }
}
