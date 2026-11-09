using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.FogOfWar
{
    /// <summary>Shared constants and pure calculations for the historical local-fog radius.</summary>
    public static class HistoricalVisibilitySettings
    {
        public const float DayMultiplier = 1.20f;
        public const float NightMultiplier = 1.00f;

        public const float ClearWeatherMultiplier = 1.00f;
        public const float LightRainMultiplier = 0.95f;
        public const float HeavyRainMultiplier = 0.85f;
        public const float LightSnowMultiplier = 0.95f;
        public const float HeavySnowMultiplier = 0.85f;
        public const float FogMultiplier = 0.55f;

        public const int SkillThreshold = 10;
        public const int MaxSkillBonus = 5;
        public const int MinimumRadius = 3;
    }

    public static class HistoricalVisibilityRadiusUtility
    {
        public static int CalculateRadius(float baseRadius, float dayMultiplier, float weatherMultiplier,
            int effectiveSkill, int traitBonus = 0)
        {
            int skillBonus = CalculateSkillBonus(effectiveSkill);
            int environmentalRadius = Mathf.RoundToInt(
                Mathf.Max(0f, baseRadius) * Mathf.Max(0f, dayMultiplier) * Mathf.Max(0f, weatherMultiplier));

            return Mathf.Max(HistoricalVisibilitySettings.MinimumRadius,
                environmentalRadius + skillBonus + Mathf.Max(0, traitBonus));
        }

        public static int CalculateSkillBonus(int skill)
        {
            if (skill <= HistoricalVisibilitySettings.SkillThreshold) return 0;
            return Mathf.Clamp(
                Mathf.CeilToInt((skill - HistoricalVisibilitySettings.SkillThreshold) / 2f),
                0,
                HistoricalVisibilitySettings.MaxSkillBonus);
        }

        public static int GetEffectiveSkill(Pawn pawn)
        {
            return pawn?.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
        }

        /// <summary>
        /// Explicit RimWorld 1.6 Core weather mapping by stable DefName. Unknown modded
        /// weather stays neutral so third-party defs cannot unexpectedly collapse vision.
        /// </summary>
        public static float GetWeatherMultiplier(WeatherDef weather)
        {
            switch (weather?.defName)
            {
                case "Clear":
                    return HistoricalVisibilitySettings.ClearWeatherMultiplier;
                case "Rain":
                    return HistoricalVisibilitySettings.LightRainMultiplier;
                case "RainyThunderstorm":
                    return HistoricalVisibilitySettings.HeavyRainMultiplier;
                case "Fog":
                case "FoggyRain":
                    return HistoricalVisibilitySettings.FogMultiplier;
                case "SnowGentle":
                    return HistoricalVisibilitySettings.LightSnowMultiplier;
                case "SnowHard":
                    return HistoricalVisibilitySettings.HeavySnowMultiplier;
                default:
                    return HistoricalVisibilitySettings.ClearWeatherMultiplier;
            }
        }
    }
}
