using HistoricalRimWorld;
using Verse;

namespace HistoricalRimWorld.UI
{
    internal static class HistoricalCalendarDebugVisibility
    {
        public static bool ShouldShow()
        {
            if (Prefs.DevMode)
            {
                return true;
            }

            HistoricalRimWorldSettings settings = LoadedModManager.GetMod<HistoricalRimWorldMod>()?.GetSettings<HistoricalRimWorldSettings>();
            return settings != null && settings.EnableDebugLogging;
        }
    }
}
