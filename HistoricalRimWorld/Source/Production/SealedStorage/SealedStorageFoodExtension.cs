using Verse;

namespace HistoricalRimWorld.Production.SealedStorage
{
    /// <summary>Opt-in marker for food whose rot rate may be modified while it occupies a sealed jar.</summary>
    public sealed class SealedStorageFoodExtension : DefModExtension
    {
        public bool eligibleForSealedStorage;
        public float rotMultiplierOverride = -1f;
    }
}
