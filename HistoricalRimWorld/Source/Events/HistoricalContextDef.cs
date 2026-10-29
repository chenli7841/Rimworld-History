using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Events
{
    /// <summary>Continuing evidence-backed conditions; deliberately not a dated occurrence or scheduler input.</summary>
    public sealed class HistoricalContextDef : Def
    {
        public Scenarios.HistoricalScenarioDef scenario;
        public int fromBceYear;
        public int throughBceYear;
        public string regionId;
        public string polityId;
        public HistoricalEventProvenance provenance = HistoricalEventProvenance.HistoricalContextSimulation;
        public HistoricalChronologyTradition chronologyTradition = HistoricalChronologyTradition.TraditionalReceivedChronology;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;
        public string sourceNotes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (fromBceYear <= 0 || throughBceYear <= 0 || fromBceYear < throughBceYear)
                yield return "A BCE context period must use positive years with fromBceYear >= throughBceYear.";
            if (provenance != HistoricalEventProvenance.HistoricalContextSimulation)
                yield return "Historical context records must use HistoricalContextSimulation provenance.";
            if (!System.Enum.IsDefined(typeof(HistoricalChronologyTradition), chronologyTradition))
                yield return "chronologyTradition is invalid.";
            if (string.IsNullOrWhiteSpace(sourceNotes)) yield return "sourceNotes is required.";
            if (string.IsNullOrWhiteSpace(label)) yield return "a localized label key is required.";
            if (string.IsNullOrWhiteSpace(description)) yield return "a localized description key is required.";
            if (regionId != null && DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
                yield return "regionId references a missing historical region Def.";
            if (polityId != null && DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polityId) == null)
                yield return "polityId references a missing historical polity Def.";
        }
    }
}
