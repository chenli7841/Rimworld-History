using System;
using HistoricalRimWorld.Calendar;

namespace HistoricalRimWorld.Legacy
{
    public static class HistoricalLegacyTimeUtility
    {
        public static long GetElapsedHistoricalDays(HistoricalDate from, HistoricalDate to)
        {
            long days = to.AbsoluteDay - from.AbsoluteDay;
            if (days < 0) throw new ArgumentException("Legacy time cannot run backwards.", nameof(to));
            return days;
        }
    }
}
