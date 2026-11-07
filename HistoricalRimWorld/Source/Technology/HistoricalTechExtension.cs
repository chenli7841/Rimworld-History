using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Technology
{
    /// <summary>
    /// Marks a vanilla research project as part of the historical timeline and
    /// links it to the existing historical technology record.
    /// </summary>
    public sealed class HistoricalTechExtension : DefModExtension
    {
        public bool IsHistoricalTechnology = true;
        public HistoricalTechnologyDef technology;
        public string HistoricalCategory;
        public int? EarliestEvidenceYear;
        public int? SpreadStartYear;
        public int? SpreadEndYear;
        public string HistoricalDescription;
        public string HistoricalEvidence;
        public string HistoricalEvidenceLevel;
        public bool RequiresBlueprint = true;
        public bool RequiresPractice;
        public List<string> OriginFactionTags;
        public List<string> PracticeRequirementIds;
        public List<string> TechnologyBonusIds;
    }
}
