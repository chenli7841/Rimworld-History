using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Characters
{
    /// <summary>Optional chronology and social identity weights for an HR historical backstory.</summary>
    public sealed class HistoricalBackstoryDefExtension : DefModExtension
    {
        /// <summary>Latest BCE year in which this backstory may be used (e.g. 600 means 600 BCE and later).</summary>
        public int? availableFromYearBCE;
        public List<HistoricalBackstoryIdentityWeight> identityWeights;
    }

    public sealed class HistoricalBackstoryIdentityWeight
    {
        public string identity;
        public float weight = 1f;
    }
}
