using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Genealogy;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Events
{
    public static class HistoricalLocalEventEligibility
    {
        public static bool HasMarriageCandidate(Map map, int minimumAgeYears)
        {
            if (map?.mapPawns == null || minimumAgeYears < 18) return false;
            List<Pawn> adults = map.mapPawns.FreeColonists.Where(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed
                && pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYears >= minimumAgeYears
                && pawn.relations != null && !pawn.relations.DirectRelations.Any(relation => relation.def == PawnRelationDefOf.Spouse
                    || relation.def == PawnRelationDefOf.Lover)).ToList();
            for (int i = 0; i < adults.Count; i++)
            for (int j = i + 1; j < adults.Count; j++)
            {
                bool hasA = HistoricalGenealogyManager.TryGetPersonId(adults[i], out string a);
                bool hasB = HistoricalGenealogyManager.TryGetPersonId(adults[j], out string b);
                if (hasA && hasB && AreCloseRelatives(a, b)) continue;
                return true;
            }
            return false;
        }

        private static bool AreCloseRelatives(string firstId, string secondId)
        {
            IEnumerable<HistoricalKinshipRelation> relations = HistoricalGenealogyManager.Relations.Where(item => item.IsActive
                && item.Type != HistoricalKinshipType.Spouse);
            HashSet<string> firstParents = new HashSet<string>(relations.Where(item => item.PersonIdB == firstId).Select(item => item.PersonIdA));
            HashSet<string> secondParents = new HashSet<string>(relations.Where(item => item.PersonIdB == secondId).Select(item => item.PersonIdA));
            return firstParents.Contains(secondId) || secondParents.Contains(firstId) || firstParents.Overlaps(secondParents);
        }
    }
}
