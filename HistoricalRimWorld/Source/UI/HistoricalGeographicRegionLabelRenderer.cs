using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.WorldGeneration;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    /// <summary>Screen-space world-map labels for historical geographic features.</summary>
    public static class HistoricalGeographicRegionLabelRenderer
    {
        private static readonly GUIStyle LabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            richText = false
        };

        public static void Draw()
        {
            if (!WorldRendererUtility.WorldSelected || Find.World == null
                || Find.WorldCamera == null || Find.World.grid?.Surface == null) return;
            HistoricalFactionWorldComponent faction = Find.World.GetComponent<HistoricalFactionWorldComponent>();
            HistoricalMapScaleState scaleState = faction?.MapScaleState;
            HistoricalMapScaleDef scale = scaleState == null
                ? null : DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(scaleState.ScaleDefName);
            if (scale == null) return;

            float zoom = Find.WorldCameraDriver?.AltitudePercent ?? 0.5f;
            List<HistoricalGeographicRegionData> candidates = HistoricalGeographicRegionManager.AllRegions
                .Where(region => zoom >= region.minZoom && zoom <= region.maxZoom)
                .OrderByDescending(region => region.labelPriority)
                .ThenBy(region => HistoricalGeographicRegionManager.ResolveName(region))
                .ToList();
            if (candidates.Count == 0) return;

            HistoricalMapScaleTransformer transformer;
            try { transformer = new HistoricalMapScaleTransformer(scale); }
            catch { return; }
            List<Rect> occupied = new List<Rect>();
            int fontSize = Mathf.RoundToInt(Mathf.Lerp(12f, 20f, 1f - Mathf.Clamp01(zoom)));
            LabelStyle.fontSize = fontSize;
            foreach (HistoricalGeographicRegionData region in candidates)
            {
                if (!HistoricalGeographicRegionManager.TryGetLabelCoordinate(region, out HistoricalGeoCoordinate coordinate)) continue;
                HistoricalGameGeoCoordinate gameCoordinate;
                try { gameCoordinate = transformer.ToGameCoordinate(coordinate); }
                catch { continue; }
                Vector3 worldPosition = HistoricalCoverageCalculator.ToUnitVector(
                    gameCoordinate.Longitude, gameCoordinate.Latitude) * WorldGrid.PlanetRadius;
                Vector3 screen = Find.WorldCamera.WorldToScreenPoint(worldPosition);
                if (screen.z <= 0f) continue;
                Vector2 gui = new Vector2(screen.x, Screen.height - screen.y);
                string label = HistoricalGeographicRegionManager.ResolveName(region);
                if (string.IsNullOrWhiteSpace(label)) continue;
                float width = Mathf.Clamp(label.Length * fontSize * 0.78f + 28f, 72f, 320f);
                float height = fontSize + 12f;
                Rect rect = new Rect(gui.x - width / 2f, gui.y - height / 2f, width, height);
                if (rect.xMax < 0f || rect.xMin > Screen.width || rect.yMax < 0f || rect.yMin > Screen.height) continue;
                if (occupied.Any(existing => existing.Overlaps(rect))) continue;

                Matrix4x4 previousMatrix = GUI.matrix;
                float rotation = GetRotation(region, transformer, fontSize);
                if (Mathf.Abs(rotation) > 0.1f) GUIUtility.RotateAroundPivot(rotation, rect.center);
                GUIStyle style = LabelStyle;
                style.normal.textColor = ColorFor(region.regionType);
                GUI.Label(rect, label, style);
                GUI.matrix = previousMatrix;

                if (Widgets.ButtonInvisible(rect))
                    Find.WindowStack.Add(new HistoricalGeographicRegionDetailWindow(region.defName));
                occupied.Add(rect);
            }
        }

        private static float GetRotation(HistoricalGeographicRegionData region,
            HistoricalMapScaleTransformer transformer, int fontSize)
        {
            float rotation = region.labelRotation;
            if (!region.autoLabelRotation) return rotation;
            if (!HistoricalGeographicRegionManager.TryGetLineLabelEndpoints(region,
                out HistoricalGeoCoordinate start, out HistoricalGeoCoordinate end)) return rotation;
            try
            {
                HistoricalGameGeoCoordinate a = transformer.ToGameCoordinate(start);
                HistoricalGameGeoCoordinate b = transformer.ToGameCoordinate(end);
                Vector3 first = Find.WorldCamera.WorldToScreenPoint(
                    HistoricalCoverageCalculator.ToUnitVector(a.Longitude, a.Latitude) * WorldGrid.PlanetRadius);
                Vector3 second = Find.WorldCamera.WorldToScreenPoint(
                    HistoricalCoverageCalculator.ToUnitVector(b.Longitude, b.Latitude) * WorldGrid.PlanetRadius);
                if (first.z <= 0f || second.z <= 0f) return rotation;
                rotation = Mathf.Atan2(first.y - second.y, second.x - first.x) * Mathf.Rad2Deg;
                if (rotation > 90f) rotation -= 180f;
                if (rotation < -90f) rotation += 180f;
                return Mathf.Clamp(rotation, -60f, 60f);
            }
            catch { return rotation; }
        }

        private static Color ColorFor(HistoricalGeographicRegionType type)
        {
            switch (type)
            {
                case HistoricalGeographicRegionType.River: return new Color(0.55f, 0.82f, 1f, 1f);
                case HistoricalGeographicRegionType.Lake:
                case HistoricalGeographicRegionType.Wetland: return new Color(0.58f, 0.88f, 0.86f, 1f);
                case HistoricalGeographicRegionType.Mountain:
                case HistoricalGeographicRegionType.MountainRange:
                case HistoricalGeographicRegionType.Pass: return new Color(1f, 0.86f, 0.62f, 1f);
                case HistoricalGeographicRegionType.Plain:
                case HistoricalGeographicRegionType.Basin: return new Color(0.86f, 0.95f, 0.72f, 1f);
                default: return Color.white;
            }
        }
    }

    public sealed class HistoricalGeographicRegionDetailWindow : Window
    {
        private readonly string regionId;
        private Vector2 scroll;

        public HistoricalGeographicRegionDetailWindow(string regionId)
        {
            this.regionId = regionId;
            doCloseX = true;
            draggable = true;
            absorbInputAroundWindow = true;
            windowRect = new Rect(Verse.UI.screenWidth / 2f - 230f, Verse.UI.screenHeight / 2f - 170f, 460f, 340f);
        }

        public override Vector2 InitialSize => new Vector2(460f, 340f);

        public override void DoWindowContents(Rect inRect)
        {
            HistoricalGeographicRegionData region = HistoricalGeographicRegionManager.FindById(regionId);
            if (region == null) { Widgets.Label(inRect, "地理区域数据已不可用。"); return; }
            Rect view = new Rect(0f, 0f, inRect.width - 18f, 600f);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);
            listing.Label(HistoricalGeographicRegionManager.ResolveName(region));
            listing.Label("类型：" + region.regionType);
            listing.Label("历史范围：" + HistoricalGeographicRegionManager.GetGeometryExtent(region));
            listing.Label("几何：" + region.geometryType + "；覆盖 Tile："
                + HistoricalGeographicRegionManager.GetTilesForRegion(region.defName).Count);
            if (!string.IsNullOrWhiteSpace(region.historicalDescription)) listing.Label(region.historicalDescription);
            if (!string.IsNullOrWhiteSpace(region.historicalName)) listing.Label("历史名称：" + region.historicalName);
            if (!string.IsNullOrWhiteSpace(region.englishName)) listing.Label("English: " + region.englishName);
            listing.Label("边界证据等级：" + region.confidence);
            if (!string.IsNullOrWhiteSpace(region.source)) listing.Label("来源：" + region.source);
            if (!string.IsNullOrWhiteSpace(region.sourceNotes)) listing.Label("说明：" + region.sourceNotes);
            listing.End();
            Widgets.EndScrollView();
        }
    }
}
