using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public sealed class HistoricalTechnologyDef : Def
    {
        public HistoricalTechnologyCategory category;
        // Earliest plausible horizon, and the latest date by which evidence confirms presence;
        // neither field is presented as a precise invention date.
        public int? earliestPossibleYear;
        public int? latestAttestedYear;
        public List<HistoricalTechnologyDef> prerequisites;
        public List<HistoricalRegionDef> initialKnownRegions;
        public List<HistoricalCultureDef> associatedCultures;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;
        public string sourceNotes;
        public ResearchProjectDef researchProject;
        public HistoricalContentPolicy contentPolicy = HistoricalContentPolicy.Restricted;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (earliestPossibleYear.HasValue && latestAttestedYear.HasValue && earliestPossibleYear > latestAttestedYear)
                yield return "earliestPossibleYear cannot be later than latestAttestedYear.";
            if (prerequisites != null && prerequisites.Any(item => item == this)) yield return "A technology cannot require itself.";
            if (prerequisites != null && prerequisites.Where(item => item != null).Select(item => item.defName).Distinct().Count() != prerequisites.Count)
                yield return "prerequisites contains duplicate entries.";
            if (HasPrerequisiteCycle(this, this, new HashSet<string>(), new HashSet<string>())) yield return "Technology prerequisites contain a dependency cycle.";
        }

        private static bool HasPrerequisiteCycle(HistoricalTechnologyDef root, HistoricalTechnologyDef current, HashSet<string> active, HashSet<string> visited)
        {
            if (current == null) return false;
            if (!active.Add(current.defName)) return current == root;
            if (visited.Add(current.defName) && current.prerequisites != null)
                foreach (HistoricalTechnologyDef prerequisite in current.prerequisites)
                    if (prerequisite == root || HasPrerequisiteCycle(root, prerequisite, active, visited)) return true;
            active.Remove(current.defName);
            return false;
        }
    }
}
