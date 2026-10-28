using System;

namespace HistoricalRimWorld.Calendar
{
    public static class HistoricalYearConverter
    {
        public static int ToAstronomicalYear(int year, bool isBce)
        {
            if (year < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(year), "Traditional year numbers start at 1; there is no year 0.");
            }

            return isBce ? checked(1 - year) : year;
        }

        public static void FromAstronomicalYear(int astronomicalYear, out int year, out bool isBce)
        {
            isBce = astronomicalYear <= 0;
            year = isBce ? checked(1 - astronomicalYear) : astronomicalYear;
        }
    }
}
