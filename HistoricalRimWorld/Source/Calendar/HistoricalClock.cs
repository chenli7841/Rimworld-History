using System;
using Verse;

namespace HistoricalRimWorld.Calendar
{
    public sealed class HistoricalClock : IHistoricalClock
    {
        private readonly HistoricalDate startDate;
        private readonly int startGameTick;

        public HistoricalClock(HistoricalDate startDate, int startGameTick)
        {
            this.startDate = startDate;
            this.startGameTick = startGameTick;
        }

        public HistoricalDate CurrentDate => DateAtTick(GetCurrentGameTick());

        public long CurrentAbsoluteDay => CurrentDate.AbsoluteDay;

        public int TickOfDay
        {
            get
            {
                long elapsedTicks = GetElapsedTicks(GetCurrentGameTick());
                return (int)HistoricalDate.FloorModulo(elapsedTicks, HistoricalCalendarConstants.TicksPerDay);
            }
        }

        public HistoricalDate DateAtTick(int gameTick)
        {
            long elapsedTicks = GetElapsedTicks(gameTick);
            long elapsedDays = HistoricalDate.FloorDivide(elapsedTicks, HistoricalCalendarConstants.TicksPerDay);
            return startDate.AddDays(elapsedDays);
        }

        public long DaysSince(HistoricalDate date)
        {
            return date.DaysUntil(CurrentDate);
        }

        public bool TryGetCurrentDate(out HistoricalDate date)
        {
            int currentGameTick;
            if (!TryGetCurrentGameTick(out currentGameTick))
            {
                date = default(HistoricalDate);
                return false;
            }

            date = DateAtTick(currentGameTick);
            return true;
        }

        private long GetElapsedTicks(int gameTick)
        {
            return (long)gameTick - startGameTick;
        }

        private static int GetCurrentGameTick()
        {
            int tick;
            if (!TryGetCurrentGameTick(out tick))
            {
                throw new InvalidOperationException("The RimWorld game clock is not available.");
            }

            return tick;
        }

        private static bool TryGetCurrentGameTick(out int tick)
        {
            if (Current.Game == null || Find.TickManager == null)
            {
                tick = 0;
                return false;
            }

            tick = Find.TickManager.TicksGame;
            return true;
        }
    }
}
