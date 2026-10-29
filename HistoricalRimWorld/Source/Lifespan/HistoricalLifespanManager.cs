using System;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Genealogy;

namespace HistoricalRimWorld.Lifespan
{
    public static class HistoricalLifespanManager
    {
        public static HistoricalNaturalLifespanRecord Find(string personId) =>
            HistoricalGenealogyGameComponentForCurrentSave()?.FindLifespan(personId);

        public static long DaysRemaining(string personId, HistoricalDate currentDate)
        {
            HistoricalNaturalLifespanRecord record = Find(personId);
            return record == null ? 0 : currentDate.DaysUntil(record.NaturalDeathDate);
        }

        private static HistoricalGenealogyGameComponent HistoricalGenealogyGameComponentForCurrentSave() =>
            Verse.Current.Game == null ? null : Verse.Current.Game.GetComponent<HistoricalGenealogyGameComponent>();
    }
}
