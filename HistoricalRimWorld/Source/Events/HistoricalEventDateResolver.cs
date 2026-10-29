using System;
using HistoricalRimWorld.Calendar;

namespace HistoricalRimWorld.Events
{
    public static class HistoricalEventDateResolver
    {
        public static HistoricalDate ResolveScheduledDate(HistoricalDateReference reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            reference.Validate();
            int month = reference.Precision == HistoricalDatePrecision.Year ? 1 : reference.Month.Value;
            int day = reference.Precision == HistoricalDatePrecision.Day ? reference.Day.Value : 1;
            return new HistoricalDate(reference.AstronomicalYear, month, day);
        }
    }
}
