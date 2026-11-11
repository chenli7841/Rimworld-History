using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Optional ecology tuning attached to existing vanilla animal ThingDefs.</summary>
    public sealed class HR_ForestWildlifeSpeciesExtension : DefModExtension
    {
        public int minimumForestArea = 100;
        public float minimumTreeDensity = 0.10f;
        public float minimumIntegrity = 20f;
        public float baseCapacity = 3f;
        public int maximumPopulation = 8;
        public int activityRadius = 35;
        public bool isPredator;
        public bool canHuntLivestock;
        public float baseLivestockHuntChance = 0.05f;
        public List<string> preferredBiomeKeywords = new List<string>();
    }
}
