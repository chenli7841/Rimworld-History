using HarmonyLib;
using HistoricalRimWorld.Calendar;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.Patches.Calendar
{
    /// <summary>Shows the active historical date in RimWorld's main date readout.</summary>
    [HarmonyPatch(typeof(GenDate), "DateReadoutStringAt", new[] { typeof(long), typeof(Vector2) })]
    internal static class HistoricalCalendarDateReadoutPatch
    {
        private static bool loggedReadoutReplacement;

        private static void Postfix(long absTicks, ref string __result)
        {
            if (Find.TickManager == null || absTicks != Find.TickManager.TicksAbs
                || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
                return;

            __result = HistoricalDateFormatter.Format(date);
            if (!loggedReadoutReplacement)
            {
                loggedReadoutReplacement = true;
                Log.Message("[Historical RimWorld] Main game date readout is using the historical calendar.");
            }
        }
    }

}
