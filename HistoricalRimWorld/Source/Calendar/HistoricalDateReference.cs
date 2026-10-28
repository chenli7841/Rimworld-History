using System;

namespace HistoricalRimWorld.Calendar
{
    public sealed class HistoricalDateReference
    {
        public int AstronomicalYear;
        public int? Month;
        public int? Day;
        public HistoricalDatePrecision Precision;

        public HistoricalDateReference(
            int astronomicalYear,
            int? month,
            int? day,
            HistoricalDatePrecision precision)
        {
            AstronomicalYear = astronomicalYear;
            Month = month;
            Day = day;
            Precision = precision;
            Validate();
        }

        public void Validate()
        {
            switch (Precision)
            {
                case HistoricalDatePrecision.Year:
                    if (Month.HasValue || Day.HasValue)
                    {
                        throw new ArgumentException("Year precision requires both month and day to be unknown.");
                    }
                    break;

                case HistoricalDatePrecision.Month:
                    if (!Month.HasValue || Day.HasValue)
                    {
                        throw new ArgumentException("Month precision requires a month and an unknown day.");
                    }
                    ValidateMonth(Month.Value);
                    break;

                case HistoricalDatePrecision.Day:
                    if (!Month.HasValue || !Day.HasValue)
                    {
                        throw new ArgumentException("Day precision requires both month and day.");
                    }
                    ValidateMonth(Month.Value);
                    ValidateDay(Day.Value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(Precision));
            }
        }

        private static void ValidateMonth(int month)
        {
            if (month < 1 || month > HistoricalCalendarConstants.MonthsPerYear)
            {
                throw new ArgumentOutOfRangeException(nameof(Month));
            }
        }

        private static void ValidateDay(int day)
        {
            if (day < 1 || day > HistoricalCalendarConstants.DaysPerMonth)
            {
                throw new ArgumentOutOfRangeException(nameof(Day));
            }
        }
    }
}
