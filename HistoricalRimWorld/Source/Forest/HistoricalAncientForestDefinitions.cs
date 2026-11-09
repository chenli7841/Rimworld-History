using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Data marker and future-facing tuning fields for ancient tree PlantDefs.</summary>
    public sealed class HR_AncientTreeExtension : DefModExtension
    {
        public bool isAncientTree = true;
        public string forestSpeciesId;
        public int requiredToolTier;
        public float erosionResistance;
        public string rareResourcePoolId;
    }
}
