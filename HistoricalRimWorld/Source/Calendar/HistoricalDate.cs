using System;

namespace HistoricalRimWorld.Calendar
{
    public readonly struct HistoricalDate : IComparable<HistoricalDate>, IEquatable<HistoricalDate>
    {
        public int AstronomicalYear { get; }
        public int Month { get; }
        public int Day { get; }

        public HistoricalDate(int astronomicalYear, int month, int day)
        {
            if (month < 1 || month > HistoricalCalendarConstants.MonthsPerYear)
            {
                throw new ArgumentOutOfRangeException(nameof(month));
            }

            if (day < 1 || day > HistoricalCalendarConstants.DaysPerMonth)
            {
                throw new ArgumentOutOfRangeException(nameof(day));
            }

            AstronomicalYear = astronomicalYear;
            Month = month;
            Day = day;
        }

        public long AbsoluteDay
        {
            get
            {
                checked
                {
                    return (long)AstronomicalYear * HistoricalCalendarConstants.DaysPerYear
                        + (long)(Month - 1) * HistoricalCalendarConstants.DaysPerMonth
                        + Day - 1;
                }
            }
        }

        public HistoricalDate AddDays(long days)
        {
            long targetDay;
            checked
            {
                targetDay = AbsoluteDay + days;
            }

            return FromAbsoluteDay(targetDay);
        }

        public HistoricalDate AddMonths(long months)
        {
            long monthIndex;
            checked
            {
                monthIndex = (long)AstronomicalYear * HistoricalCalendarConstants.MonthsPerYear
                    + Month - 1 + months;
            }

            long astronomicalYear = FloorDivide(monthIndex, HistoricalCalendarConstants.MonthsPerYear);
            int month = (int)FloorModulo(monthIndex, HistoricalCalendarConstants.MonthsPerYear) + 1;
            return new HistoricalDate(ToInt32Year(astronomicalYear), month, Day);
        }

        public HistoricalDate AddYears(int years)
        {
            long targetYear;
            checked
            {
                targetYear = (long)AstronomicalYear + years;
            }

            return new HistoricalDate(ToInt32Year(targetYear), Month, Day);
        }

        public long DaysUntil(HistoricalDate other)
        {
            checked
            {
                return other.AbsoluteDay - AbsoluteDay;
            }
        }

        public int CompareTo(HistoricalDate other)
        {
            return AbsoluteDay.CompareTo(other.AbsoluteDay);
        }

        public bool Equals(HistoricalDate other)
        {
            return AstronomicalYear == other.AstronomicalYear
                && Month == other.Month
                && Day == other.Day;
        }

        public override bool Equals(object obj)
        {
            return obj is HistoricalDate && Equals((HistoricalDate)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AstronomicalYear;
                hash = (hash * 397) ^ Month;
                return (hash * 397) ^ Day;
            }
        }

        public static bool operator <(HistoricalDate left, HistoricalDate right) => left.CompareTo(right) < 0;
        public static bool operator >(HistoricalDate left, HistoricalDate right) => left.CompareTo(right) > 0;
        public static bool operator <=(HistoricalDate left, HistoricalDate right) => left.CompareTo(right) <= 0;
        public static bool operator >=(HistoricalDate left, HistoricalDate right) => left.CompareTo(right) >= 0;
        public static bool operator ==(HistoricalDate left, HistoricalDate right) => left.Equals(right);
        public static bool operator !=(HistoricalDate left, HistoricalDate right) => !left.Equals(right);

        public override string ToString()
        {
            return HistoricalDateFormatter.Format(this);
        }

        public static HistoricalDate FromAbsoluteDay(long absoluteDay)
        {
            long astronomicalYear = FloorDivide(absoluteDay, HistoricalCalendarConstants.DaysPerYear);
            int dayOfYear = (int)FloorModulo(absoluteDay, HistoricalCalendarConstants.DaysPerYear);
            int month = dayOfYear / HistoricalCalendarConstants.DaysPerMonth + 1;
            int day = dayOfYear % HistoricalCalendarConstants.DaysPerMonth + 1;
            return new HistoricalDate(ToInt32Year(astronomicalYear), month, day);
        }

        internal static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            long remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        internal static long FloorModulo(long value, long divisor)
        {
            long remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        private static int ToInt32Year(long astronomicalYear)
        {
            if (astronomicalYear < int.MinValue || astronomicalYear > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(astronomicalYear), "The resulting date is outside the supported year range.");
            }

            return (int)astronomicalYear;
        }
    }
}
