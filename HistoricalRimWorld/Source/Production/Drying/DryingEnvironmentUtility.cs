using RimWorld;
using Verse;

namespace HistoricalRimWorld.Production.Drying
{
    public static class DryingEnvironmentUtility
    {
        public static float Efficiency(Map map, IntVec3 cell, CompProperties_DryingRack settings, out string pauseReason, out float temperature)
        {
            pauseReason = null;
            temperature = 0f;
            if (map == null || !cell.InBounds(map))
            {
                pauseReason = "HR_Drying_Pause_NoMap";
                return 0f;
            }

            temperature = GenTemperature.GetTemperatureForCell(cell, map);
            settings = settings ?? new CompProperties_DryingRack();
            if (temperature < settings.freezingPointCelsius)
            {
                pauseReason = "HR_Drying_Pause_TooCold";
                return 0f;
            }

            bool roofed = map.roofGrid.Roofed(cell);
            bool raining = map.weatherManager != null && map.weatherManager.RainRate > 0.01f;
            bool snowing = map.weatherManager != null && map.weatherManager.SnowRate > 0.01f;
            if (!roofed && raining && settings.pauseDuringRain)
            {
                pauseReason = "HR_Drying_Pause_Rain";
                return 0f;
            }
            if (!roofed && snowing && settings.pauseDuringSnow)
            {
                pauseReason = "HR_Drying_Pause_Snow";
                return 0f;
            }

            float temperatureFactor = temperature < settings.coldTemperatureLimitCelsius ? settings.coldTemperatureEfficiency
                : temperature <= settings.moderateTemperatureLimitCelsius ? settings.normalTemperatureEfficiency
                : temperature <= settings.hotTemperatureLimitCelsius ? settings.warmTemperatureEfficiency
                : settings.hotTemperatureEfficiency;
            float dayPercent = GenLocalDate.DayPercent(map);
            float daylightFactor = dayPercent >= settings.daylightStart && dayPercent <= settings.daylightEnd ? 1f : settings.nighttimeEfficiency;
            float roofFactor = roofed ? settings.roofedEfficiency : 1f;
            if (roofed && (raining || snowing)) pauseReason = "HR_Drying_RoofedWeather";
            else if (daylightFactor < 1f) pauseReason = "HR_Drying_Night";
            return temperatureFactor * daylightFactor * roofFactor;
        }
    }
}
