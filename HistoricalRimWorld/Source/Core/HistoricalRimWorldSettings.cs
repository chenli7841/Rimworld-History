using UnityEngine;
using Verse;
using HistoricalRimWorld.FogOfWar;

namespace HistoricalRimWorld
{
    public sealed class HistoricalRimWorldSettings : ModSettings
    {
        public bool EnableDebugLogging = false;
        public bool EnableLocalFogOfWar = true;
        public float DayVisionRadius = 12f;
        public float NightVisionRadius = 4f;
        public bool EnableDayNightVision = true;
        public bool EnableLineOfSightBlocking = true;
        public bool TamedAnimalsProvideVision = false;
        public bool HideUnseenHostilePawns = true;
        public bool HideUnseenWildAnimals = true;
        public float UnexploredFogOpacity = 1f;
        public float ExploredFogOpacity = 0.65f;
        public bool LegacyMapsFullyExplored = true;
        public bool EnableFogDebugVisualization = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref EnableDebugLogging, "EnableDebugLogging", false);
            Scribe_Values.Look(ref EnableLocalFogOfWar, "EnableLocalFogOfWar", true);
            Scribe_Values.Look(ref DayVisionRadius, "DayVisionRadius", 12f);
            Scribe_Values.Look(ref NightVisionRadius, "NightVisionRadius", 4f);
            Scribe_Values.Look(ref EnableDayNightVision, "EnableDayNightVision", true);
            Scribe_Values.Look(ref EnableLineOfSightBlocking, "EnableLineOfSightBlocking", true);
            Scribe_Values.Look(ref TamedAnimalsProvideVision, "TamedAnimalsProvideVision", false);
            Scribe_Values.Look(ref HideUnseenHostilePawns, "HideUnseenHostilePawns", true);
            Scribe_Values.Look(ref HideUnseenWildAnimals, "HideUnseenWildAnimals", true);
            Scribe_Values.Look(ref UnexploredFogOpacity, "UnexploredFogOpacity", 1f);
            Scribe_Values.Look(ref ExploredFogOpacity, "ExploredFogOpacity", 0.65f);
            Scribe_Values.Look(ref LegacyMapsFullyExplored, "LegacyMapsFullyExplored", true);
            Scribe_Values.Look(ref EnableFogDebugVisualization, "EnableFogDebugVisualization", false);
            base.ExposeData();
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(Translator.Translate("HR_EnableDebugLogging"), ref EnableDebugLogging);
            listing.GapLine();
            listing.Label(Translator.Translate("HR_Fog_SettingsHeader"));
            listing.CheckboxLabeled(Translator.Translate("HR_Fog_Enable"), ref EnableLocalFogOfWar);
            listing.CheckboxLabeled(Translator.Translate("HR_Fog_EnableDayNight"), ref EnableDayNightVision);
            listing.Label(Translator.Translate("HR_Fog_DayRadius") + ": " + DayVisionRadius.ToString("0.0"));
            DayVisionRadius = listing.Slider(DayVisionRadius, 1f, 30f);
            listing.Label(Translator.Translate("HR_Fog_NightRadius") + ": " + NightVisionRadius.ToString("0.0"));
            NightVisionRadius = listing.Slider(NightVisionRadius, 0f, 20f);
            listing.CheckboxLabeled(Translator.Translate("HR_Fog_LOS"), ref EnableLineOfSightBlocking);
            listing.CheckboxLabeled(Translator.Translate("HR_Fog_TamedAnimals"), ref TamedAnimalsProvideVision);
            listing.CheckboxLabeled(Translator.Translate("HR_Fog_HideHostiles"), ref HideUnseenHostilePawns);
            listing.CheckboxLabeled(Translator.Translate("HR_Fog_HideAnimals"), ref HideUnseenWildAnimals);
            listing.Label(Translator.Translate("HR_Fog_UnexploredOpacity") + ": " + UnexploredFogOpacity.ToString("0.00"));
            UnexploredFogOpacity = listing.Slider(UnexploredFogOpacity, 0f, 1f);
            listing.Label(Translator.Translate("HR_Fog_ExploredOpacity") + ": " + ExploredFogOpacity.ToString("0.00"));
            ExploredFogOpacity = listing.Slider(ExploredFogOpacity, 0f, 1f);
            listing.CheckboxLabeled(Translator.Translate("HR_Fog_LegacyMaps"), ref LegacyMapsFullyExplored);
            listing.CheckboxLabeled(Translator.Translate("HR_Fog_Debug"), ref EnableFogDebugVisualization);
            listing.End();
        }
    }

    internal static class HistoricalLocalFogSettings
    {
        public static HistoricalRimWorldSettings Current => HistoricalRimWorldMod.CurrentSettings;
        public static void NotifyChanged() => HistoricalLocalFogOfWarManager.MarkAllMapsDirty();
    }
}
