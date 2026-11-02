using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Geography
{
    public enum HistoricalGeographicRegionType
    {
        MountainRange,
        Mountain,
        River,
        Lake,
        Plain,
        Basin,
        Plateau,
        Valley,
        Pass,
        Forest,
        Wetland,
        GeographicRegion,
        Desert,
        Coast,
        Peninsula,
        Island,
        Strait
    }

    public enum HistoricalGeographicGeometryType
    {
        Point,
        LineString,
        Polygon,
        MultiPolygon
    }

    /// <summary>Historical map feature, independent of political regions and tile terrain.</summary>
    public sealed class HistoricalGeographicRegionDef : Def
    {
        public string scenarioId;
        public HistoricalGeographicRegionType regionType = HistoricalGeographicRegionType.GeographicRegion;
        public string englishName;
        public string historicalName;
        public string scenarioNameOverride;
        public List<HistoricalGeographicName> historicalNames = new List<HistoricalGeographicName>();
        public HistoricalGeographicGeometryType geometryType = HistoricalGeographicGeometryType.Polygon;
        public List<HistoricalGeoVertex> coordinates = new List<HistoricalGeoVertex>();
        public List<HistoricalGeoPolygon> polygons = new List<HistoricalGeoPolygon>();
        public bool hasLabelPosition;
        public HistoricalGeoVertex labelPosition;
        public float labelRotation;
        public bool autoLabelRotation;
        public float minZoom;
        public float maxZoom = 1f;
        public int startYearBCE;
        public int endYearBCE;
        public int labelPriority = 50;
        public HistoricalBoundaryConfidence confidence = HistoricalBoundaryConfidence.Approximate;
        public string source;
        public string sourceNotes;
        public string historicalDescription;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (!Enum.IsDefined(typeof(HistoricalGeographicRegionType), regionType))
                yield return "regionType is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalGeographicGeometryType), geometryType))
                yield return "geometryType is invalid.";
            if (!Enum.IsDefined(typeof(HistoricalBoundaryConfidence), confidence))
                yield return "confidence is invalid.";
            if (string.IsNullOrWhiteSpace(englishName) && string.IsNullOrWhiteSpace(historicalName))
                yield return "englishName or historicalName is required.";
            if (string.IsNullOrWhiteSpace(sourceNotes))
                yield return "sourceNotes is required; explicitly mark approximate geometry and its provenance.";
            if (minZoom < 0f || maxZoom > 1f || minZoom > maxZoom)
                yield return "minZoom and maxZoom must be an ordered range within 0..1 camera altitude percent.";
            if (startYearBCE > 0 && endYearBCE > 0 && startYearBCE < endYearBCE)
                yield return "For BCE ranges, startYearBCE must be greater than or equal to endYearBCE.";
            if (hasLabelPosition && labelPosition == null)
                yield return "labelPosition is required when hasLabelPosition is true.";
            if (labelPosition != null)
            {
                foreach (string error in ValidateVertex(labelPosition)) yield return "labelPosition: " + error;
            }

            foreach (string error in ValidateGeometry()) yield return error;
            foreach (HistoricalGeographicName name in historicalNames ?? new List<HistoricalGeographicName>())
            {
                if (name == null) { yield return "historicalNames contains a null entry."; continue; }
                if (string.IsNullOrWhiteSpace(name.name)) yield return "historicalNames entries require a name.";
                if (name.startYearBCE > 0 && name.endYearBCE > 0 && name.startYearBCE < name.endYearBCE)
                    yield return "A BCE name period must have startYearBCE >= endYearBCE.";
            }
        }

        private IEnumerable<string> ValidateGeometry()
        {
            List<HistoricalGeoVertex> points = coordinates ?? new List<HistoricalGeoVertex>();
            switch (geometryType)
            {
                case HistoricalGeographicGeometryType.Point:
                    if (points.Count != 1) yield return "Point geometry requires exactly one coordinate.";
                    foreach (HistoricalGeoVertex point in points)
                        foreach (string error in ValidateVertex(point)) yield return "Point: " + error;
                    break;
                case HistoricalGeographicGeometryType.LineString:
                    if (points.Count < 2) yield return "LineString geometry requires at least two coordinates.";
                    foreach (HistoricalGeoVertex point in points)
                        foreach (string error in ValidateVertex(point)) yield return "LineString: " + error;
                    break;
                case HistoricalGeographicGeometryType.Polygon:
                    if (polygons != null && polygons.Count > 0)
                    {
                        foreach (HistoricalGeoPolygon polygon in polygons)
                            foreach (string error in polygon == null ? new[] { "contains a null polygon." } : polygon.Validate())
                                yield return "Polygon: " + error;
                    }
                    else
                    {
                        HistoricalGeoPolygon polygon = new HistoricalGeoPolygon { vertices = points };
                        foreach (string error in polygon.Validate()) yield return "Polygon: " + error;
                    }
                    break;
                case HistoricalGeographicGeometryType.MultiPolygon:
                    if (polygons == null || polygons.Count == 0) yield return "MultiPolygon requires at least one polygon.";
                    else foreach (HistoricalGeoPolygon polygon in polygons)
                        foreach (string error in polygon == null ? new[] { "contains a null polygon." } : polygon.Validate())
                            yield return "MultiPolygon: " + error;
                    break;
            }
        }

        private static IEnumerable<string> ValidateVertex(HistoricalGeoVertex vertex)
        {
            if (vertex == null) { yield return "coordinate is null."; yield break; }
            if (double.IsNaN(vertex.longitude) || double.IsInfinity(vertex.longitude)
                || vertex.longitude < -180d || vertex.longitude > 180d)
                yield return "longitude must be finite and within -180..180.";
            if (double.IsNaN(vertex.latitude) || double.IsInfinity(vertex.latitude)
                || vertex.latitude < -90d || vertex.latitude > 90d)
                yield return "latitude must be finite and within -90..90.";
        }
    }

    /// <summary>A period-qualified display name; BCE years are positive and descend forward in time.</summary>
    public sealed class HistoricalGeographicName : IExposable
    {
        public string name;
        public string language = "zh-CN";
        public int startYearBCE;
        public int endYearBCE;

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref language, "language", "zh-CN");
            Scribe_Values.Look(ref startYearBCE, "startYearBCE", 0);
            Scribe_Values.Look(ref endYearBCE, "endYearBCE", 0);
        }
    }

    /// <summary>Saveable runtime copy of a geographic region Def, including editor changes.</summary>
    public sealed class HistoricalGeographicRegionData : IExposable
    {
        public string defName;
        public string label;
        public string scenarioId;
        public HistoricalGeographicRegionType regionType;
        public string englishName;
        public string historicalName;
        public string scenarioNameOverride;
        public List<HistoricalGeographicName> historicalNames = new List<HistoricalGeographicName>();
        public HistoricalGeographicGeometryType geometryType;
        public List<HistoricalGeoVertex> coordinates = new List<HistoricalGeoVertex>();
        public List<HistoricalGeoPolygon> polygons = new List<HistoricalGeoPolygon>();
        public bool hasLabelPosition;
        public HistoricalGeoVertex labelPosition;
        public float labelRotation;
        public bool autoLabelRotation;
        public float minZoom;
        public float maxZoom = 1f;
        public int startYearBCE;
        public int endYearBCE;
        public int labelPriority = 50;
        public HistoricalBoundaryConfidence confidence = HistoricalBoundaryConfidence.Approximate;
        public string source;
        public string sourceNotes;
        public string historicalDescription;

        public static HistoricalGeographicRegionData FromDef(HistoricalGeographicRegionDef def)
        {
            if (def == null) return null;
            HistoricalGeographicRegionData data = new HistoricalGeographicRegionData
            {
                defName = def.defName, label = def.label, scenarioId = def.scenarioId,
                regionType = def.regionType, englishName = def.englishName,
                historicalName = def.historicalName, scenarioNameOverride = def.scenarioNameOverride,
                geometryType = def.geometryType, hasLabelPosition = def.hasLabelPosition,
                labelPosition = CloneVertex(def.labelPosition), labelRotation = def.labelRotation,
                autoLabelRotation = def.autoLabelRotation, minZoom = def.minZoom, maxZoom = def.maxZoom,
                startYearBCE = def.startYearBCE, endYearBCE = def.endYearBCE,
                labelPriority = def.labelPriority, confidence = def.confidence, source = def.source,
                sourceNotes = def.sourceNotes, historicalDescription = def.historicalDescription,
                historicalNames = (def.historicalNames ?? new List<HistoricalGeographicName>())
                    .Where(item => item != null).Select(CloneName).ToList(),
                coordinates = (def.coordinates ?? new List<HistoricalGeoVertex>())
                    .Where(item => item != null).Select(CloneVertex).ToList(),
                polygons = (def.polygons ?? new List<HistoricalGeoPolygon>()).Where(item => item != null)
                    .Select(ClonePolygon).ToList()
            };
            return data;
        }

        public HistoricalGeographicRegionData Clone()
        {
            HistoricalGeographicRegionDef proxy = new HistoricalGeographicRegionDef
            {
                defName = defName, label = label, scenarioId = scenarioId, regionType = regionType,
                englishName = englishName, historicalName = historicalName,
                scenarioNameOverride = scenarioNameOverride, historicalNames = historicalNames,
                geometryType = geometryType, coordinates = coordinates, polygons = polygons,
                hasLabelPosition = hasLabelPosition, labelPosition = labelPosition,
                labelRotation = labelRotation, autoLabelRotation = autoLabelRotation,
                minZoom = minZoom, maxZoom = maxZoom, startYearBCE = startYearBCE,
                endYearBCE = endYearBCE, labelPriority = labelPriority, confidence = confidence,
                source = source, sourceNotes = sourceNotes, historicalDescription = historicalDescription
            };
            return FromDef(proxy);
        }

        public IEnumerable<string> Validate()
        {
            HistoricalGeographicRegionDef proxy = ToDef();
            return proxy.ConfigErrors().ToList();
        }

        public HistoricalGeographicRegionDef ToDef() => new HistoricalGeographicRegionDef
        {
            defName = defName, label = label, scenarioId = scenarioId, regionType = regionType,
            englishName = englishName, historicalName = historicalName,
            scenarioNameOverride = scenarioNameOverride, historicalNames = historicalNames,
            geometryType = geometryType, coordinates = coordinates, polygons = polygons,
            hasLabelPosition = hasLabelPosition, labelPosition = labelPosition,
            labelRotation = labelRotation, autoLabelRotation = autoLabelRotation,
            minZoom = minZoom, maxZoom = maxZoom, startYearBCE = startYearBCE,
            endYearBCE = endYearBCE, labelPriority = labelPriority, confidence = confidence,
            source = source, sourceNotes = sourceNotes, historicalDescription = historicalDescription
        };

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref scenarioId, "scenarioId");
            Scribe_Values.Look(ref regionType, "regionType", HistoricalGeographicRegionType.GeographicRegion);
            Scribe_Values.Look(ref englishName, "englishName");
            Scribe_Values.Look(ref historicalName, "historicalName");
            Scribe_Values.Look(ref scenarioNameOverride, "scenarioNameOverride");
            Scribe_Collections.Look(ref historicalNames, "historicalNames", LookMode.Deep);
            Scribe_Values.Look(ref geometryType, "geometryType", HistoricalGeographicGeometryType.Polygon);
            Scribe_Collections.Look(ref coordinates, "coordinates", LookMode.Deep);
            Scribe_Collections.Look(ref polygons, "polygons", LookMode.Deep);
            Scribe_Values.Look(ref hasLabelPosition, "hasLabelPosition", false);
            Scribe_Deep.Look(ref labelPosition, "labelPosition");
            Scribe_Values.Look(ref labelRotation, "labelRotation", 0f);
            Scribe_Values.Look(ref autoLabelRotation, "autoLabelRotation", false);
            Scribe_Values.Look(ref minZoom, "minZoom", 0f);
            Scribe_Values.Look(ref maxZoom, "maxZoom", 1f);
            Scribe_Values.Look(ref startYearBCE, "startYearBCE", 0);
            Scribe_Values.Look(ref endYearBCE, "endYearBCE", 0);
            Scribe_Values.Look(ref labelPriority, "labelPriority", 50);
            Scribe_Values.Look(ref confidence, "confidence", HistoricalBoundaryConfidence.Approximate);
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref sourceNotes, "sourceNotes");
            Scribe_Values.Look(ref historicalDescription, "historicalDescription");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                historicalNames = historicalNames ?? new List<HistoricalGeographicName>();
                coordinates = coordinates ?? new List<HistoricalGeoVertex>();
                polygons = polygons ?? new List<HistoricalGeoPolygon>();
            }
        }

        private static HistoricalGeoVertex CloneVertex(HistoricalGeoVertex source) => source == null ? null
            : new HistoricalGeoVertex { latitude = source.latitude, longitude = source.longitude };

        private static HistoricalGeographicName CloneName(HistoricalGeographicName source) => new HistoricalGeographicName
        { name = source.name, language = source.language, startYearBCE = source.startYearBCE, endYearBCE = source.endYearBCE };

        private static HistoricalGeoPolygon ClonePolygon(HistoricalGeoPolygon source) => new HistoricalGeoPolygon
        { vertices = (source.vertices ?? new List<HistoricalGeoVertex>()).Where(item => item != null).Select(CloneVertex).ToList() };
    }
}
