using HistoricalRimWorld.Calendar;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalCalendar : MainTabWindow
    {
        public override void DoWindowContents(UnityEngine.Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label(Translator.Translate("HR_Calendar_Title"));
            listing.Gap();

            HistoricalCalendarGameComponent component = HistoricalCalendarManager.GetCurrentComponent();
            if (component == null || !component.Initialized)
            {
                listing.Label(Translator.Translate("HR_Calendar_Inactive"));
                listing.Gap();
                listing.Label(Translator.Translate("HR_Calendar_InactiveHelp"));
                if (listing.ButtonText(Translator.Translate("HR_Calendar_EnableButton")))
                {
                    if (component != null && component.TryInitializeDefaultCalendar())
                    {
                        Log.Message("[Historical RimWorld] Historical calendar enabled for this save.");
                    }
                }
            }
            else
            {
                HistoricalDate currentDate;
                HistoricalDate startDate;
                if (component.TryGetCurrentDate(out currentDate) && component.TryGetStartDate(out startDate))
                {
                    listing.Label(Translator.Translate("HR_Calendar_CurrentDate"));
                    listing.Label(HistoricalDateFormatter.Format(currentDate));
                    listing.Gap();
                    listing.Label(Translator.Translate("HR_Calendar_Rules"));
                    listing.Label(string.Format(Translator.Translate("HR_Calendar_ElapsedDays"), startDate.DaysUntil(currentDate)));

                    if (HistoricalCalendarDebugVisibility.ShouldShow())
                    {
                        listing.Gap();
                        listing.Label(Translator.Translate("HR_Calendar_DebugTitle"));
                        listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugCurrentTick"), Find.TickManager.TicksGame));
                        listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugStartTick"), component.StartGameTick));
                        listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugElapsedDays"), startDate.DaysUntil(currentDate)));
                        listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugAstronomicalYear"), currentDate.AstronomicalYear));
                        listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugAbsoluteDay"), currentDate.AbsoluteDay));
                    }
                }
            }

            listing.End();
        }
    }
}
