using System;
using Verse;

namespace HistoricalRimWorld.Calendar
{
    public static class HistoricalCalendarManager
    {
        public static bool IsActive
        {
            get
            {
                HistoricalCalendarGameComponent component = GetCurrentComponent();
                return component != null && component.Initialized;
            }
        }

        public static HistoricalDate CurrentDate
        {
            get
            {
                HistoricalDate date;
                if (!TryGetCurrentDate(out date))
                {
                    throw new InvalidOperationException("Historical calendar is not active for the current game.");
                }

                return date;
            }
        }

        public static bool TryGetCurrentDate(out HistoricalDate date)
        {
            HistoricalCalendarGameComponent component = GetCurrentComponent();
            if (component == null || !component.Initialized)
            {
                date = default(HistoricalDate);
                return false;
            }

            return component.TryGetCurrentDate(out date);
        }

        internal static HistoricalCalendarGameComponent GetCurrentComponent()
        {
            if (Current.Game == null)
            {
                return null;
            }

            return Current.Game.GetComponent<HistoricalCalendarGameComponent>();
        }
    }
}
