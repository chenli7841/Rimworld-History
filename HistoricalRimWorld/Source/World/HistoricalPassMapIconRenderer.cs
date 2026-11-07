using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.WorldGeneration;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.World
{
    /// <summary>Draws static pass icons in screen space so they never occupy or replace a world tile.</summary>
    public static class HistoricalPassMapIconRenderer
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> CoveredMountainPasses = new HashSet<string>();

        public static void Draw()
        {
            if (!WorldRendererUtility.WorldSelected || Find.World == null || Find.WorldCamera == null
                || Find.World.grid?.Surface == null) return;
            HistoricalFactionWorldComponent factionState = Find.World.GetComponent<HistoricalFactionWorldComponent>();
            if (factionState == null || factionState.GenerationMode != HistoricalFactionGenerationMode.HistoricalScenario
                || string.IsNullOrWhiteSpace(factionState.ScenarioDefName) || factionState.MapScaleState == null) return;
            HistoricalMapScaleDef scale = DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(
                factionState.MapScaleState.ScaleDefName);
            HistoricalScenarioDef scenario = DefDatabase<HistoricalScenarioDef>.GetNamedSilentFail(
                factionState.ScenarioDefName);
            if (scale == null || scenario == null) return;

            HistoricalDate date;
            int currentAstronomicalYear = HistoricalCalendarManager.TryGetCurrentDate(out date)
                ? date.AstronomicalYear : scenario.startAstronomicalYear;
            float zoom = Find.WorldCameraDriver?.AltitudePercent ?? 0.5f;
            HistoricalMapScaleTransformer transformer;
            try { transformer = new HistoricalMapScaleTransformer(scale); }
            catch { return; }

            List<HistoricalFortifiedPassDef> forts = DefDatabase<HistoricalFortifiedPassDef>.AllDefsListForReading
                .Where(def => def != null && def.scenario == scenario && IsVisibleAtDate(def, currentAstronomicalYear)
                    && zoom >= def.minimumZoom && zoom <= def.maximumZoom)
                .OrderByDescending(def => def.labelPriority).ThenBy(def => def.defName).ToList();
            CoveredMountainPasses.Clear();
            foreach (HistoricalFortifiedPassDef fort in forts)
            {
                if (!string.IsNullOrWhiteSpace(fort.relatedMountainPassDefName))
                    CoveredMountainPasses.Add(fort.relatedMountainPassDefName);
            }

            foreach (HistoricalFortifiedPassDef fort in forts)
                DrawIcon(fort.label, fort.texturePath, fort.latitude, fort.longitude,
                    transformer, zoom, "HR_Pass_FortifiedTooltip", "HR_Pass_FortifiedType", fort.sourceNotes);

            foreach (HistoricalMountainPassDef pass in DefDatabase<HistoricalMountainPassDef>.AllDefsListForReading
                .Where(def => def != null && def.scenario == scenario && def.showOnWorldMap
                    && zoom >= def.minimumZoom && zoom <= def.maximumZoom)
                .OrderByDescending(def => def.labelPriority).ThenBy(def => def.defName))
            {
                if (CoveredMountainPasses.Contains(pass.defName)) continue;
                DrawIcon(pass.label, pass.texturePath, pass.latitude, pass.longitude,
                    transformer, zoom, "HR_Pass_NaturalTooltip", "HR_Pass_NaturalType",
                    pass.historicalDescription);
            }
        }

        private static bool IsVisibleAtDate(HistoricalFortifiedPassDef def, int year)
        {
            if (!def.showOnWorldMap || def.establishedAstronomicalYear == int.MinValue
                || year < def.establishedAstronomicalYear) return false;
            return def.abandonedAstronomicalYear == int.MinValue || year < def.abandonedAstronomicalYear;
        }

        private static void DrawIcon(string name, string texturePath, float latitude, float longitude,
            HistoricalMapScaleTransformer transformer, float zoom, string tooltipKey, string typeKey, string detail)
        {
            HistoricalGameGeoCoordinate projected;
            try { projected = transformer.ToGameCoordinate(new HistoricalGeoCoordinate(latitude, longitude)); }
            catch { return; }
            Vector3 worldPosition = HistoricalCoverageCalculator.ToUnitVector(projected.Longitude, projected.Latitude)
                * WorldGrid.PlanetRadius;
            Vector3 screen = Find.WorldCamera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f) return;
            Vector2 point = new Vector2(screen.x, Screen.height - screen.y);
            float size = Mathf.Lerp(26f, 15f, Mathf.Clamp01(zoom));
            Rect rect = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            if (rect.xMax < 0f || rect.xMin > Screen.width || rect.yMax < 0f || rect.yMin > Screen.height) return;
            Texture2D texture = GetTexture(texturePath);
            if (texture == null) return;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            string tooltip = string.Format(Translator.Translate(tooltipKey), name,
                Translator.Translate(typeKey), detail ?? string.Empty);
            TooltipHandler.TipRegion(rect, tooltip);
        }

        private static Texture2D GetTexture(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Textures.TryGetValue(path, out Texture2D cached)) return cached;
            Texture2D texture = ContentFinder<Texture2D>.Get(path, false);
            Textures[path] = texture;
            return texture;
        }
    }
}
