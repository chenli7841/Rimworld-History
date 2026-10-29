using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Characters;
using Verse;

namespace HistoricalRimWorld.Genealogy
{
    public static class HistoricalGenealogyManager
    {
        public static IEnumerable<HistoricalPersonRecord> Persons => GetComponent()?.Persons ?? Enumerable.Empty<HistoricalPersonRecord>();
        public static IEnumerable<HistoricalFamilyRecord> Families => GetComponent()?.Families ?? Enumerable.Empty<HistoricalFamilyRecord>();
        public static IEnumerable<HistoricalKinshipRelation> Relations => GetComponent()?.Relations ?? Enumerable.Empty<HistoricalKinshipRelation>();
        public static HistoricalPersonRecord FindPerson(string personId) => GetComponent()?.FindPerson(personId);
        public static HistoricalFamilyRecord FindFamily(string familyId) => GetComponent()?.FindFamily(familyId);
        public static IEnumerable<HistoricalKinshipRelation> RelationsFor(string personId) => Relations
            .Where(item => item != null && (item.PersonIdA == personId || item.PersonIdB == personId));

        internal static bool TryInitializeForScenario(string scenarioId, out string error)
        {
            error = null;
            HistoricalGenealogyGameComponent component = GetComponent();
            if (component == null) { error = "HistoricalGenealogyGameComponent is unavailable."; return false; }
            if (!component.InitializeForScenario(scenarioId)) { error = "Genealogy is already bound to another scenario."; return false; }
            return true;
        }

        public static bool TryGetPawn(string personId, out Pawn pawn)
        {
            pawn = GetComponent()?.FindPawn(personId);
            return pawn != null;
        }

        public static bool TryGetPersonId(Pawn pawn, out string personId)
        {
            personId = GetComponent()?.FindPersonId(pawn);
            return personId != null;
        }

        public static bool TryAddRelation(string personIdA, string personIdB, HistoricalKinshipType type,
            out string error)
        {
            HistoricalGenealogyGameComponent component = GetComponent();
            if (component == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
            { error = "The historical genealogy or calendar is inactive."; return false; }
            return component.TryAddKinshipRelation(personIdA, personIdB, type, date, out error);
        }

        public static bool TryEndRelation(string personIdA, string personIdB, HistoricalKinshipType type,
            out string error)
        {
            HistoricalGenealogyGameComponent component = GetComponent();
            if (component == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
            { error = "The historical genealogy or calendar is inactive."; return false; }
            return component.TryEndKinshipRelation(personIdA, personIdB, type, date, out error);
        }

        public static bool TrySetDisplayName(string personId, string displayName, out string error)
        {
            HistoricalGenealogyGameComponent component = GetComponent();
            if (component == null) { error = "Historical genealogy is inactive."; return false; }
            return component.TrySetDisplayName(personId, displayName, out error);
        }

        private static HistoricalGenealogyGameComponent GetComponent() => Current.Game == null
            ? null
            : Current.Game.GetComponent<HistoricalGenealogyGameComponent>();
    }
}
