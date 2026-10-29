using System;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Genealogy;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public static class HistoricalCharacterManager
    {
        public static bool TryGetPawn(string personId, out Pawn pawn) => HistoricalGenealogyManager.TryGetPawn(personId, out pawn);

        public static bool TryGetPersonId(Pawn pawn, out string personId) => HistoricalGenealogyManager.TryGetPersonId(pawn, out personId);

        public static bool TryGetCurrentHistoricalAge(string personId, out int age)
        {
            age = 0;
            HistoricalPersonRecord person = HistoricalGenealogyManager.FindPerson(personId);
            if (person == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate)) return false;
            HistoricalDate effectiveDate = person.IsAlive ? currentDate : (person.DeathDate ?? currentDate);
            age = HistoricalAgeUtility.GetHistoricalAgeYears(person.BirthDate, effectiveDate);
            return true;
        }
    }
}
