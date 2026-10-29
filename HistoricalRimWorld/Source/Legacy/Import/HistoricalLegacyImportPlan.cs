using System.Collections.Generic;
using HistoricalRimWorld.Legacy.Simulation;

namespace HistoricalRimWorld.Legacy.Import
{
    /// <summary>An immutable preview selection. Creating a plan never changes the active game.</summary>
    public sealed class HistoricalLegacyImportPlan
    {
        internal HistoricalLegacyImportPlan(HistoricalLegacyArchive archive, LegacySimulationResult simulation,
            string familyId, IEnumerable<string> personIds, IEnumerable<string> relicIds)
        {
            Archive = archive;
            Simulation = simulation;
            FamilyId = familyId;
            PersonIds = new List<string>(personIds ?? new string[0]);
            RelicIds = new List<string>(relicIds ?? new string[0]);
        }
        public HistoricalLegacyArchive Archive { get; }
        public LegacySimulationResult Simulation { get; }
        public string FamilyId { get; }
        public IReadOnlyList<string> PersonIds { get; }
        public IReadOnlyList<string> RelicIds { get; }
    }
}
