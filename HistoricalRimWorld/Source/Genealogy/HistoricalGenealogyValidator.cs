using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Characters;

namespace HistoricalRimWorld.Genealogy
{
    public static class HistoricalGenealogyValidator
    {
        public static bool CanAddRelation(string personIdA, string personIdB, HistoricalKinshipType type,
            IEnumerable<HistoricalPersonRecord> people, IEnumerable<HistoricalKinshipRelation> relations, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(personIdA) || string.IsNullOrWhiteSpace(personIdB)) { error = "Both person IDs are required."; return false; }
            if (personIdA == personIdB) { error = "A person cannot have a relationship with themselves."; return false; }
            if (!(people ?? Enumerable.Empty<HistoricalPersonRecord>()).Any(person => person?.PersonId == personIdA)
                || !people.Any(person => person?.PersonId == personIdB)) { error = "Both historical persons must exist."; return false; }

            List<HistoricalKinshipRelation> existing = (relations ?? Enumerable.Empty<HistoricalKinshipRelation>()).Where(item => item != null).ToList();
            bool reversed = type == HistoricalKinshipType.Spouse;
            if (existing.Any(item => item.IsActive && item.Type == type &&
                ((item.PersonIdA == personIdA && item.PersonIdB == personIdB)
                    || (reversed && item.PersonIdA == personIdB && item.PersonIdB == personIdA))))
            { error = "This relationship already exists."; return false; }

            if (type == HistoricalKinshipType.BiologicalParent)
            {
                if (existing.Count(item => item.Type == type && item.PersonIdB == personIdB && item.IsActive) >= 2)
                { error = "A person can have at most two recorded biological parents."; return false; }
                if (HasDescendantPath(personIdB, personIdA, existing))
                { error = "This biological parent relation would form a cycle."; return false; }
            }
            if (type == HistoricalKinshipType.AdoptiveParent
                && existing.Any(item => item.Type == HistoricalKinshipType.BiologicalParent
                    && item.PersonIdA == personIdA && item.PersonIdB == personIdB))
            { error = "An adoptive relation cannot replace an existing biological parent relation."; return false; }
            return true;
        }

        private static bool HasDescendantPath(string startId, string targetId, IEnumerable<HistoricalKinshipRelation> relations)
        {
            HashSet<string> visited = new HashSet<string>();
            Stack<string> pending = new Stack<string>();
            pending.Push(startId);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!visited.Add(current)) continue;
                if (current == targetId) return true;
                foreach (HistoricalKinshipRelation child in relations.Where(item => item.Type == HistoricalKinshipType.BiologicalParent && item.PersonIdA == current))
                    pending.Push(child.PersonIdB);
            }
            return false;
        }
    }
}
