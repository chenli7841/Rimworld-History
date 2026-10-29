using HistoricalRimWorld.Calendar;

namespace HistoricalRimWorld.Characters
{
    public static class HistoricalAgeUtility
    {
        public static int GetHistoricalAgeYears(HistoricalDate birthDate, HistoricalDate currentDate)
        {
            long elapsedDays = birthDate.DaysUntil(currentDate);
            if (elapsedDays <= 0) return 0;
            long years = elapsedDays / HistoricalCalendarConstants.DaysPerYear;
            return years > int.MaxValue ? int.MaxValue : (int)years;
        }
    }
}
