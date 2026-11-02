using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.Scenarios;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Geography
{
    /// <summary>Derived geographic feature and tile mappings for the active WorldGrid.</summary>
    public static class HistoricalGeographicRegionManager
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static RimWorldWorld cachedWorld;
        private static HistoricalGeographicRegionWorldComponent cachedComponent;
        private static string cachedScaleDefName;
        private static Dictionary<int, List<string>> regionsByTile = new Dictionary<int, List<string>>();
        private static Dictionary<string, List<int>> tilesByRegion = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private static List<HistoricalGeographicRegionData> regions = new List<HistoricalGeographicRegionData>();
        private static readonly Dictionary<string, PreparedGeometry> prepared = new Dictionary<string, PreparedGeometry>(StringComparer.Ordinal);

        public static void Invalidate()
        {
            cachedWorld = null;
            cachedComponent = null;
            cachedScaleDefName = null;
            regionsByTile = new Dictionary<int, List<string>>();
            tilesByRegion = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            regions = new List<HistoricalGeographicRegionData>();
            prepared.Clear();
        }

        public static IReadOnlyList<HistoricalGeographicRegionData> AllRegions
        {
            get { EnsureCache(); return regions.AsReadOnly(); }
        }

        public static IReadOnlyList<HistoricalGeographicRegionData> GetRegionsForTile(int tileId)
        {
            EnsureCache();
            if (!regionsByTile.TryGetValue(tileId, out List<string> ids))
                return new List<HistoricalGeographicRegionData>().AsReadOnly();
            return ids.Select(FindById).Where(item => item != null)
                .OrderByDescending(item => item.labelPriority)
                .ThenBy(item => ResolveName(item), StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public static IReadOnlyList<int> GetTilesForRegion(string defName)
        {
            EnsureCache();
            return tilesByRegion.TryGetValue(defName ?? string.Empty, out List<int> ids)
                ? ids.AsReadOnly() : new List<int>().AsReadOnly();
        }

        public static HistoricalGeographicRegionData FindById(string defName)
        {
            EnsureCache();
            return regions.FirstOrDefault(item => item != null && item.defName == defName);
        }

        public static string ResolveName(HistoricalGeographicRegionData region)
        {
            if (region == null) return string.Empty;
            HistoricalFactionWorldComponent faction = Find.World?.GetComponent<HistoricalFactionWorldComponent>();
            if (!string.IsNullOrWhiteSpace(region.scenarioNameOverride)
                && !string.IsNullOrWhiteSpace(region.scenarioId)
                && faction?.ScenarioDefName == region.scenarioId)
                return region.scenarioNameOverride;

            int bceYear = GetCurrentBceYear(faction?.ScenarioDefName);
            string language = LanguageDatabase.activeLanguage?.folderName ?? "ChineseSimplified";
            HistoricalGeographicName periodName = (region.historicalNames ?? new List<HistoricalGeographicName>())
                .Where(item => item != null && LanguageMatches(item.language, language)
                    && IsWithinBceRange(bceYear, item.startYearBCE, item.endYearBCE))
                .OrderByDescending(item => item.startYearBCE).FirstOrDefault();
            if (periodName != null && !string.IsNullOrWhiteSpace(periodName.name)) return periodName.name;
            if (!string.IsNullOrWhiteSpace(region.historicalName) && !IsEnglish(language)) return region.historicalName;
            if (!string.IsNullOrWhiteSpace(region.englishName) && IsEnglish(language)) return region.englishName;
            if (!string.IsNullOrWhiteSpace(region.historicalName)) return region.historicalName;
            if (!string.IsNullOrWhiteSpace(region.englishName)) return region.englishName;
            return region.label ?? region.defName;
        }

        public static string ResolveDescription(HistoricalGeographicRegionData region) =>
            region == null ? string.Empty : region.historicalDescription ?? string.Empty;

        public static bool TryGetLabelCoordinate(HistoricalGeographicRegionData region, out HistoricalGeoCoordinate coordinate)
        {
            coordinate = default(HistoricalGeoCoordinate);
            if (region == null) return false;
            if (region.hasLabelPosition && region.labelPosition != null)
            {
                try { coordinate = region.labelPosition.ToCoordinate(); return true; }
                catch { return false; }
            }
            IReadOnlyList<HistoricalGeoCoordinate> line = GetPrimaryCoordinates(region).ToList();
            if (line.Count == 0) return false;
            if (region.geometryType == HistoricalGeographicGeometryType.LineString)
            {
                int middle = (line.Count - 1) / 2;
                HistoricalGeoCoordinate first = line[middle];
                HistoricalGeoCoordinate second = line[Math.Min(middle + 1, line.Count - 1)];
                coordinate = new HistoricalGeoCoordinate((first.Latitude + second.Latitude) / 2d,
                    (first.Longitude + second.Longitude) / 2d);
                return true;
            }
            coordinate = new HistoricalGeoCoordinate(line.Average(item => item.Latitude),
                line.Average(item => item.Longitude));
            return true;
        }

        public static bool TryGetLineLabelEndpoints(HistoricalGeographicRegionData region,
            out HistoricalGeoCoordinate start, out HistoricalGeoCoordinate end)
        {
            start = end = default(HistoricalGeoCoordinate);
            IReadOnlyList<HistoricalGeoCoordinate> line = GetPrimaryCoordinates(region).ToList();
            if (region == null || line.Count < 2) return false;
            if (region.geometryType == HistoricalGeographicGeometryType.LineString)
            {
                int index = (line.Count - 1) / 2;
                start = line[index];
                end = line[Math.Min(index + 1, line.Count - 1)];
                return true;
            }
            double meanLatitude = line.Average(item => item.Latitude);
            double cosine = Math.Cos(meanLatitude * Math.PI / 180d);
            double meanX = line.Average(item => item.Longitude * cosine);
            double meanY = line.Average(item => item.Latitude);
            double xx = 0d, yy = 0d, xy = 0d;
            foreach (HistoricalGeoCoordinate point in line)
            {
                double x = point.Longitude * cosine - meanX;
                double y = point.Latitude - meanY;
                xx += x * x; yy += y * y; xy += x * y;
            }
            double angle = 0.5d * Math.Atan2(2d * xy, xx - yy);
            double axisX = Math.Cos(angle), axisY = Math.Sin(angle);
            double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
            foreach (HistoricalGeoCoordinate point in line)
            {
                double projection = (point.Longitude * cosine - meanX) * axisX
                    + (point.Latitude - meanY) * axisY;
                minimum = Math.Min(minimum, projection);
                maximum = Math.Max(maximum, projection);
            }
            if (maximum - minimum < 1e-8d) return false;
            start = new HistoricalGeoCoordinate(meanY + axisY * minimum,
                (meanX + axisX * minimum) / Math.Max(0.0001d, cosine));
            end = new HistoricalGeoCoordinate(meanY + axisY * maximum,
                (meanX + axisX * maximum) / Math.Max(0.0001d, cosine));
            return true;
        }

        public static string GetGeometryExtent(HistoricalGeographicRegionData region)
        {
            List<HistoricalGeoCoordinate> points = GetAllCoordinates(region).ToList();
            if (points.Count == 0) return "无有效几何";
            return points.Min(item => item.Longitude).ToString("0.###", Invariant) + "–"
                + points.Max(item => item.Longitude).ToString("0.###", Invariant) + "°E, "
                + points.Min(item => item.Latitude).ToString("0.###", Invariant) + "–"
                + points.Max(item => item.Latitude).ToString("0.###", Invariant) + "°N";
        }

        private static void EnsureCache()
        {
            RimWorldWorld world = Find.World;
            HistoricalFactionWorldComponent faction = world?.GetComponent<HistoricalFactionWorldComponent>();
            HistoricalMapScaleState scaleState = faction?.MapScaleState;
            string scaleId = scaleState?.ScaleDefName;
            if (world == null || faction == null || scaleState == null) return;
            if (ReferenceEquals(world, cachedWorld) && cachedComponent != null
                && cachedScaleDefName == scaleId && regions.Count > 0) return;

            HistoricalGeographicRegionWorldComponent component = world.GetComponent<HistoricalGeographicRegionWorldComponent>();
            component?.InitializeForScenario(faction.ScenarioDefName);
            cachedWorld = world;
            cachedComponent = component;
            cachedScaleDefName = scaleId;
            regions = (component?.Regions ?? Enumerable.Empty<HistoricalGeographicRegionData>())
                .Where(item => item != null && (string.IsNullOrWhiteSpace(item.scenarioId)
                    || item.scenarioId == faction.ScenarioDefName))
                .Where(item => !item.Validate().Any())
                .ToList();
            regionsByTile = new Dictionary<int, List<string>>();
            tilesByRegion = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            prepared.Clear();
            HistoricalMapScaleDef scale = DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(scaleId);
            if (world.grid?.Surface == null || scale == null || regions.Count == 0) return;
            HistoricalMapScaleTransformer transformer;
            try { transformer = new HistoricalMapScaleTransformer(scale); }
            catch (Exception exception)
            { Log.Warning("[Historical RimWorld] Geographic mapping is unavailable: " + exception.Message); return; }

            HistoricalGeoCoordinate[] tileCoordinates = new HistoricalGeoCoordinate[world.grid.TilesCount];
            for (int tileId = 0; tileId < tileCoordinates.Length; tileId++)
            {
                Vector2 game = world.grid.Surface.LongLatOf(tileId);
                try
                {
                    tileCoordinates[tileId] = transformer.ToHistoricalCoordinate(
                        new HistoricalGameGeoCoordinate(game.y, game.x));
                }
                catch { tileCoordinates[tileId] = default(HistoricalGeoCoordinate); }
            }
            double lineTolerance = Math.Sqrt(4d * Math.PI / Math.Max(1, world.grid.TilesCount))
                * 180d / Math.PI * 0.65d;
            foreach (HistoricalGeographicRegionData region in regions)
            {
                PreparedGeometry geometry = Prepare(region);
                prepared[region.defName] = geometry;
                List<int> mapped = new List<int>();
                if (geometry == null) { tilesByRegion[region.defName] = mapped; continue; }
                int nearestPointTile = -1;
                double nearestPointDistance = double.PositiveInfinity;
                for (int tileId = 0; tileId < tileCoordinates.Length; tileId++)
                {
                    HistoricalGeoCoordinate point = tileCoordinates[tileId];
                    if (geometry.Type == HistoricalGeographicGeometryType.Point)
                    {
                        double distance = HistoricalGeographicDistanceUtility.GetGreatCircleDistanceKm(point,
                            geometry.Coordinates[0]);
                        if (distance < nearestPointDistance) { nearestPointDistance = distance; nearestPointTile = tileId; }
                    }
                    else if (geometry.Contains(point, lineTolerance)) mapped.Add(tileId);
                }
                if (geometry.Type == HistoricalGeographicGeometryType.Point && nearestPointTile >= 0)
                    mapped.Add(nearestPointTile);
                tilesByRegion[region.defName] = mapped;
                foreach (int tileId in mapped)
                {
                    if (!regionsByTile.TryGetValue(tileId, out List<string> ids))
                        regionsByTile[tileId] = ids = new List<string>();
                    ids.Add(region.defName);
                }
            }
        }

        private static PreparedGeometry Prepare(HistoricalGeographicRegionData region)
        {
            List<HistoricalGeoCoordinate> coordinates = new List<HistoricalGeoCoordinate>();
            try
            {
                if (region.geometryType == HistoricalGeographicGeometryType.Point
                    || region.geometryType == HistoricalGeographicGeometryType.LineString)
                    coordinates = (region.coordinates ?? new List<HistoricalGeoVertex>()).Select(item => item.ToCoordinate()).ToList();
                else
                {
                    List<HistoricalGeoPolygon> polygons = region.polygons ?? new List<HistoricalGeoPolygon>();
                    if (polygons.Count > 0)
                        return new PreparedGeometry(region.geometryType, null, polygons.Select(item =>
                            item.vertices.Select(vertex => vertex.ToCoordinate()).ToArray()).ToList());
                    coordinates = (region.coordinates ?? new List<HistoricalGeoVertex>()).Select(item => item.ToCoordinate()).ToList();
                    return new PreparedGeometry(region.geometryType, null,
                        new List<HistoricalGeoCoordinate[]> { coordinates.ToArray() });
                }
                return new PreparedGeometry(region.geometryType, coordinates, null);
            }
            catch { return null; }
        }

        private static IEnumerable<HistoricalGeoCoordinate> GetPrimaryCoordinates(HistoricalGeographicRegionData region)
        {
            if (region == null) return Enumerable.Empty<HistoricalGeoCoordinate>();
            if ((region.geometryType == HistoricalGeographicGeometryType.Point
                || region.geometryType == HistoricalGeographicGeometryType.LineString)
                && region.coordinates != null)
                return region.coordinates.Where(item => item != null).Select(item => item.ToCoordinate()).ToList();
            if (region.polygons != null && region.polygons.Count > 0)
                return region.polygons[0].vertices.Where(item => item != null).Select(item => item.ToCoordinate()).ToList();
            return region.coordinates?.Where(item => item != null).Select(item => item.ToCoordinate()).ToList()
                ?? new List<HistoricalGeoCoordinate>();
        }

        private static IEnumerable<HistoricalGeoCoordinate> GetAllCoordinates(HistoricalGeographicRegionData region)
        {
            if (region == null) yield break;
            foreach (HistoricalGeoCoordinate point in GetPrimaryCoordinates(region)) yield return point;
            foreach (HistoricalGeoPolygon polygon in region.polygons ?? new List<HistoricalGeoPolygon>())
                foreach (HistoricalGeoVertex vertex in polygon?.vertices ?? new List<HistoricalGeoVertex>())
                    if (vertex != null) yield return vertex.ToCoordinate();
        }

        private static int GetCurrentBceYear(string scenarioId)
        {
            if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date))
                return date.AstronomicalYear < 0 ? -date.AstronomicalYear : 0;
            HistoricalScenarioDef scenario = DefDatabase<HistoricalScenarioDef>.GetNamedSilentFail(scenarioId);
            return scenario != null && scenario.startAstronomicalYear < 0 ? -scenario.startAstronomicalYear : 0;
        }

        private static bool IsWithinBceRange(int year, int start, int end) =>
            year == 0 || start <= 0 && end <= 0 || (start <= 0 || year <= start) && (end <= 0 || year >= end);

        private static bool LanguageMatches(string nameLanguage, string activeLanguage) =>
            string.IsNullOrWhiteSpace(nameLanguage) || nameLanguage == activeLanguage
            || nameLanguage == "zh" && activeLanguage.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase)
            || nameLanguage == "en" && IsEnglish(activeLanguage);

        private static bool IsEnglish(string language) => language != null
            && language.StartsWith("English", StringComparison.OrdinalIgnoreCase);

        private sealed class PreparedGeometry
        {
            public readonly HistoricalGeographicGeometryType Type;
            public readonly List<HistoricalGeoCoordinate> Coordinates;
            public readonly List<HistoricalGeoCoordinate[]> Polygons;

            public PreparedGeometry(HistoricalGeographicGeometryType type,
                List<HistoricalGeoCoordinate> coordinates, List<HistoricalGeoCoordinate[]> polygons)
            { Type = type; Coordinates = coordinates; Polygons = polygons; }

            public bool Contains(HistoricalGeoCoordinate point, double lineTolerance)
            {
                if (Type == HistoricalGeographicGeometryType.LineString)
                {
                    for (int i = 1; i < Coordinates.Count; i++)
                        if (DistanceToSegmentDegrees(point, Coordinates[i - 1], Coordinates[i]) <= lineTolerance) return true;
                    return false;
                }
                if (Type != HistoricalGeographicGeometryType.Polygon && Type != HistoricalGeographicGeometryType.MultiPolygon) return false;
                foreach (HistoricalGeoCoordinate[] polygon in Polygons)
                    if (PointInPolygon(point, polygon)) return true;
                return false;
            }

            private static bool PointInPolygon(HistoricalGeoCoordinate point, HistoricalGeoCoordinate[] vertices)
            {
                bool inside = false;
                for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                {
                    HistoricalGeoCoordinate a = vertices[j], b = vertices[i];
                    bool crosses = (a.Latitude > point.Latitude) != (b.Latitude > point.Latitude);
                    if (crosses && point.Longitude < (b.Longitude - a.Longitude)
                        * (point.Latitude - a.Latitude) / (b.Latitude - a.Latitude) + a.Longitude)
                        inside = !inside;
                }
                return inside;
            }

            private static double DistanceToSegmentDegrees(HistoricalGeoCoordinate point,
                HistoricalGeoCoordinate start, HistoricalGeoCoordinate end)
            {
                double cosine = Math.Cos(point.Latitude * Math.PI / 180d);
                double ax = start.Longitude * cosine, bx = end.Longitude * cosine, px = point.Longitude * cosine;
                double ay = start.Latitude, by = end.Latitude, py = point.Latitude;
                double dx = bx - ax, dy = by - ay;
                double lengthSquared = dx * dx + dy * dy;
                double fraction = lengthSquared < 1e-12d ? 0d : Math.Max(0d, Math.Min(1d,
                    ((px - ax) * dx + (py - ay) * dy) / lengthSquared));
                double x = px - (ax + fraction * dx), y = py - (ay + fraction * dy);
                return Math.Sqrt(x * x + y * y);
            }
        }
    }
}
