namespace HistoricalRimWorld.Calendar
{
    public static class HistoricalCalendarDefaults
    {
        public const int StartAstronomicalYear = -769;
        public const int StartMonth = 1;
        public const int StartDay = 1;

        public static HistoricalDate StartDate =>
            new HistoricalDate(StartAstronomicalYear, StartMonth, StartDay);
    }
}
