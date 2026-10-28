using RimWorld;

namespace HistoricalRimWorld.Calendar
{
    public static class HistoricalCalendarConstants
    {
        public const int MonthsPerYear = 12;
        public const int DaysPerMonth = 5;

        // Reuse the public RimWorld 1.6 time constants rather than duplicating them.
        public const int DaysPerYear = GenDate.DaysPerYear;
        public const int TicksPerDay = GenDate.TicksPerDay;
        public const long TicksPerYear = (long)TicksPerDay * DaysPerYear;
    }
}
