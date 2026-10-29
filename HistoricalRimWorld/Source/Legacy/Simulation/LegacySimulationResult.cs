using System.Collections.Generic;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Legacy;

namespace HistoricalRimWorld.Legacy.Simulation
{
    public sealed class LegacySimulationResult
    {
        public string SourceArchiveId;
        public string TargetScenarioId;
        public int SimulationSeed;
        public HistoricalDateReference TargetDate;
        public List<LegacyPersonData> GeneratedPersons = new List<LegacyPersonData>();
        public List<LegacyKinshipData> GeneratedKinships = new List<LegacyKinshipData>();
        public List<LegacyChronicleData> GeneratedChronicle = new List<LegacyChronicleData>();
        public List<LegacyDescendantCandidate> Candidates = new List<LegacyDescendantCandidate>();
        public HistoricalLegacyArchive SimulatedArchive;
    }
}
