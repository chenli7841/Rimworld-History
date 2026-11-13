using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Events;
using Verse;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.World
{
    public enum HistoricalLandmarkType
    {
        HistoricalMountainBattlefield,
        HistoricalCapitalSettlement,
        HistoricalNaturalLandmark
    }

    /// <summary>Historical place identity and real-world coordinate; tile IDs are resolved per generated world.</summary>
    public sealed class HistoricalLandmarkDef : Def
    {
        public string landmarkId;
        public string mapLocationId;
        public string historicalName;
        public float latitude;
        public float longitude;
        public HistoricalLandmarkType landmarkType;
        public HistoricalScenarioDef scenario;
        public HistoricalMapTemplateDef mapTemplate;
        public List<string> relatedEventIds = new List<string>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(landmarkId)) yield return "landmarkId is required.";
            if (string.IsNullOrWhiteSpace(mapLocationId)) yield return "mapLocationId is required.";
            if (string.IsNullOrWhiteSpace(historicalName)) yield return "historicalName is required.";
            if (scenario == null) yield return "scenario is required.";
            if (latitude < -90f || latitude > 90f) yield return "latitude must be between -90 and 90.";
            if (longitude < -180f || longitude > 180f) yield return "longitude must be between -180 and 180.";
            if (!Enum.IsDefined(typeof(HistoricalLandmarkType), landmarkType)) yield return "landmarkType is invalid.";
            if (mapTemplate == null) yield return "mapTemplate is required.";
            else if (mapTemplate.landmark != this) yield return "mapTemplate must refer back to this landmark.";
            HistoricalWorldMapDef map = DefDatabase<HistoricalWorldMapDef>.AllDefsListForReading.FirstOrDefault(item =>
                item != null && item.locations != null && item.locations.Any(location => location != null
                    && location.locationId == mapLocationId && location.landmarkDefName == defName));
            if (map == null) yield return "No HistoricalWorldMapDef location references this landmark Def.";
            else
            {
                HistoricalWorldMapLocation location = map.locations.First(item => item != null && item.locationId == mapLocationId);
                if (Math.Abs(location.latitude - latitude) > 0.0001f || Math.Abs(location.longitude - longitude) > 0.0001f)
                    yield return "Landmark coordinates must match its world-map location coordinates.";
            }
        }
    }

    /// <summary>Ordered, normalized scene areas; these are gameplay layout labels, not archaeological plans.</summary>
    public sealed class HistoricalSceneZoneDef
    {
        public string zoneId;
        public float minX;
        public float minZ;
        public float maxX = 1f;
        public float maxZ = 1f;
    }

    public sealed class HistoricalSceneApproachDef
    {
        public string approachId;
        public float x;
        public float z;
    }

    /// <summary>Map-generation profile attached to a historical landmark.</summary>
    public sealed class HistoricalMapTemplateDef : Def
    {
        public string templateId;
        public HistoricalLandmarkDef landmark;
        public string terrainProfile;
        public string layoutProfile;
        public string mountainRockDefName = "Granite";
        public int templateVersion = 1;
        public int minimumMapDimension = 80;
        public List<HistoricalSceneZoneDef> zones = new List<HistoricalSceneZoneDef>();
        public List<HistoricalSceneApproachDef> approaches = new List<HistoricalSceneApproachDef>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(templateId)) yield return "templateId is required.";
            if (landmark == null) yield return "landmark is required.";
            if (terrainProfile != "LishanBattlefield" && terrainProfile != "HaojingCapital")
                yield return "terrainProfile must be a supported profile: LishanBattlefield or HaojingCapital.";
            if (templateVersion < 1) yield return "templateVersion must be positive.";
            if (minimumMapDimension < 40) yield return "minimumMapDimension must be at least 40.";
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (HistoricalSceneZoneDef zone in zones ?? Enumerable.Empty<HistoricalSceneZoneDef>())
            {
                if (string.IsNullOrWhiteSpace(zone?.zoneId)) yield return "Each scene zone requires a zoneId.";
                else if (!ids.Add(zone.zoneId)) yield return "Scene zone ID '" + zone.zoneId + "' is duplicated.";
                if (zone == null || zone.minX < 0f || zone.minZ < 0f || zone.maxX > 1f || zone.maxZ > 1f
                    || zone.minX >= zone.maxX || zone.minZ >= zone.maxZ)
                    yield return "Scene zone bounds must be normalized, non-empty coordinates in 0..1.";
            }
            ids.Clear();
            foreach (HistoricalSceneApproachDef approach in approaches ?? Enumerable.Empty<HistoricalSceneApproachDef>())
            {
                if (string.IsNullOrWhiteSpace(approach?.approachId)) yield return "Each approach requires an approachId.";
                else if (!ids.Add(approach.approachId)) yield return "Approach ID '" + approach.approachId + "' is duplicated.";
                if (approach == null || approach.x < 0f || approach.x > 1f || approach.z < 0f || approach.z > 1f)
                    yield return "Approach coordinates must be normalized in 0..1.";
            }
        }
    }

    /// <summary>Optional period/event layer metadata. Layer ordering is numeric, never string-based.</summary>
    public sealed class HistoricalSceneLayerDef : Def
    {
        public HistoricalLandmarkDef landmark;
        public HistoricalMapTemplateDef template;
        public int minimumAstronomicalYear = int.MinValue;
        public int maximumAstronomicalYear = int.MaxValue;
        public string historicalEventId;
        public int minimumEventStageIndex = -1;
        public int maximumEventStageIndex = int.MaxValue;
        public int order;
        public List<string> enabledFeatures = new List<string>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (landmark == null) yield return "landmark is required.";
            if (template == null) yield return "template is required.";
            if (template != null && landmark != null && template.landmark != landmark)
                yield return "template must belong to the same landmark.";
            if (minimumAstronomicalYear > maximumAstronomicalYear) yield return "Historical year range is reversed.";
            if (!string.IsNullOrEmpty(historicalEventId))
            {
                HistoricalEventDef eventDef = DefDatabase<HistoricalEventDef>.AllDefsListForReading.FirstOrDefault(item =>
                    item != null && item.eventId == historicalEventId);
                if (eventDef == null) yield return "historicalEventId does not resolve to a HistoricalEventDef.";
                else if (minimumEventStageIndex > maximumEventStageIndex)
                    yield return "Event stage index range is reversed.";
                else if (minimumEventStageIndex >= 0 && maximumEventStageIndex >= 0
                    && maximumEventStageIndex >= eventDef.stages.Count)
                    yield return "maximumEventStageIndex exceeds the event's declared stage order.";
            }
            if (minimumEventStageIndex < -1 || maximumEventStageIndex < -1)
                yield return "Event stage indices must be -1 (unrestricted) or non-negative.";
        }
    }
}
