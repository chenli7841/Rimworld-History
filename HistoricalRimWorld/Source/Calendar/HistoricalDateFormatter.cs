using System;
using Verse;

namespace HistoricalRimWorld.Calendar
{
    public static class HistoricalDateFormatter
    {
        public static string Format(HistoricalDate date)
        {
            int year;
            bool isBce;
            HistoricalYearConverter.FromAstronomicalYear(date.AstronomicalYear, out year, out isBce);

            string month = Translator.Translate("HR_Calendar_Month_" + date.Month);
            string day = Translator.Translate("HR_Calendar_Day_" + date.Day);
            string formatKey = isBce ? "HR_Calendar_Date_BCE" : "HR_Calendar_Date_CE";
            return string.Format(Translator.Translate(formatKey), year, month, day);
        }
    }
}
