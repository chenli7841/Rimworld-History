using System;
using Verse;

namespace HistoricalRimWorld.Calendar
{
    public sealed class HistoricalCalendarGameComponent : GameComponent
    {
        public HistoricalCalendarGameComponent(Game game) { }

        private int startAstronomicalYear;
        private int startMonth;
        private int startDay;
        private int startGameTick;
        private bool initialized;

        public bool Initialized => initialized;

        public int StartGameTick => startGameTick;

        public bool TryGetStartDate(out HistoricalDate date)
        {
            if (!initialized)
            {
                date = default(HistoricalDate);
                return false;
            }

            try
            {
                date = new HistoricalDate(startAstronomicalYear, startMonth, startDay);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                date = default(HistoricalDate);
                return false;
            }
        }

        public bool TryGetCurrentDate(out HistoricalDate date)
        {
            if (!initialized)
            {
                date = default(HistoricalDate);
                return false;
            }

            return new HistoricalClock(
                new HistoricalDate(startAstronomicalYear, startMonth, startDay),
                startGameTick).TryGetCurrentDate(out date);
        }

        public bool TryInitializeDefaultCalendar()
        {
            return TryInitializeCalendar(HistoricalCalendarDefaults.StartDate);
        }

        public bool CanInitializeCalendar(HistoricalDate startDate)
        {
            if (Current.Game == null || Find.TickManager == null)
            {
                return false;
            }

            if (!initialized)
            {
                return true;
            }

            HistoricalDate existingStartDate;
            return TryGetStartDate(out existingStartDate) && existingStartDate == startDate;
        }

        public bool TryInitializeCalendar(HistoricalDate startDate)
        {
            if (initialized)
            {
                HistoricalDate existingStartDate;
                return TryGetStartDate(out existingStartDate) && existingStartDate == startDate;
            }

            if (Current.Game == null || Find.TickManager == null)
            {
                return false;
            }

            startAstronomicalYear = startDate.AstronomicalYear;
            startMonth = startDate.Month;
            startDay = startDate.Day;
            startGameTick = Find.TickManager.TicksGame;
            initialized = true;
            return true;
        }

        public HistoricalClock CreateClock()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("The historical calendar has not been initialized for this save.");
            }

            return new HistoricalClock(
                new HistoricalDate(startAstronomicalYear, startMonth, startDay),
                startGameTick);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref startAstronomicalYear, "startAstronomicalYear");
            Scribe_Values.Look(ref startMonth, "startMonth");
            Scribe_Values.Look(ref startDay, "startDay");
            Scribe_Values.Look(ref startGameTick, "startGameTick");
            Scribe_Values.Look(ref initialized, "initialized");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && initialized && !TryGetStartDate(out _))
            {
                Log.Warning("[Historical RimWorld] Invalid saved historical calendar date; leaving the calendar inactive.");
                initialized = false;
            }
        }
    }
}
