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
        public float DiffusionDifficulty = 1f;
        public float EmergenceChancePerYear = 0.12f;
        public float DiffusionChancePerInterval = 0.12f;
        public float WidespreadThreshold = 0.7f;
        public bool RequiresBlueprint = true;
        public bool RequiresPractice;
        public bool AllowTradeDiffusion = true;
        public bool AllowNeighborDiffusion = true;
        public bool IsRestrictedMilitaryTechnology;
        public bool NotifyPlayerOnEmergence = true;
        public List<string> OriginFactionTags;
        public List<string> PreferredCultureTags;
        public List<string> PracticeRequirementIds;
        public List<string> TechnologyBonusIds;
    }
}
