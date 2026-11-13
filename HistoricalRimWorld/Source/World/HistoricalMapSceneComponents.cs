using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.World
{
    public sealed class HistoricalMapSceneComponent : MapComponent
    {
        private const int DataVersion = 1;
        private int dataVersion;
        private string scenarioId;
        private string templateId;
        private List<string> landmarkIds = new List<string>();
        private List<HistoricalSceneZoneState> zones = new List<HistoricalSceneZoneState>();
        private List<HistoricalSceneApproachState> approaches = new List<HistoricalSceneApproachState>();
        private List<string> appliedLayerIds = new List<string>();
        private int generationSeed;
        private bool generationApplied;
        private int lastLayerRefreshTick = -1;

        public HistoricalMapSceneComponent(Map map) : base(map) { }
        public bool GenerationApplied => generationApplied;
        public string ScenarioId => scenarioId;
        public string TemplateId => templateId;
        public int GenerationSeed => generationSeed;
        public IEnumerable<string> LandmarkIds => landmarkIds ?? Enumerable.Empty<string>();
        public IEnumerable<string> AppliedLayerIds => appliedLayerIds ?? Enumerable.Empty<string>();

        internal void Initialize(HistoricalScenarioDef scenario, HistoricalMapTemplateDef template,
            IEnumerable<HistoricalLandmarkDef> landmarks, int seed)
        {
            scenarioId = scenario?.defName;
            templateId = template?.defName;
            landmarkIds = (landmarks ?? Enumerable.Empty<HistoricalLandmarkDef>())
                .Where(item => item != null).Select(item => item.defName).Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal).ToList();
            generationSeed = seed;
            zones = new List<HistoricalSceneZoneState>();
            foreach (HistoricalSceneZoneDef zone in template?.zones ?? new List<HistoricalSceneZoneDef>())
                zones.Add(HistoricalSceneZoneState.FromNormalized(zone, map.Size.x, map.Size.z));
            approaches = new List<HistoricalSceneApproachState>();
            foreach (HistoricalSceneApproachDef approach in template?.approaches ?? new List<HistoricalSceneApproachDef>())
                approaches.Add(HistoricalSceneApproachState.FromNormalized(approach, map.Size.x, map.Size.z));
            generationApplied = true;
            RefreshSceneLayers(true);
        }

        public bool TryGetZone(string zoneId, out CellRect rect)
        {
            HistoricalSceneZoneState state = zones?.FirstOrDefault(item => item != null && item.ZoneId == zoneId);
            rect = state == null ? CellRect.Empty : state.ToCellRect(map);
            return state != null && !rect.IsEmpty;
        }

        public bool TryGetApproachCell(string approachId, out IntVec3 cell)
        {
            HistoricalSceneApproachState state = approaches?.FirstOrDefault(item => item != null && item.ApproachId == approachId);
            cell = state == null ? IntVec3.Invalid : state.ToCell(map);
            return state != null && cell.IsValid;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!generationApplied || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            if (tick % 250 == 0 && Find.CurrentMap == map
                && landmarkIds != null && landmarkIds.Contains("HR_Landmark_Lishan"))
                HistoricalEventIntegrationCoordinator.OnHistoricalMapActivated("HR_Landmark_Lishan", map);
            if (tick % 2500 == 0 && tick != lastLayerRefreshTick) RefreshSceneLayers(false);
        }

        private void RefreshSceneLayers(bool force)
        {
            if (!force && (Find.TickManager == null || !HistoricalScenarioManager.IsScenarioActive
                || scenarioId != HistoricalScenarioManager.ActiveScenarioDefName)) return;
            if (!force && Find.TickManager.TicksGame == lastLayerRefreshTick) return;
            lastLayerRefreshTick = Find.TickManager?.TicksGame ?? -1;
            HistoricalLandmarkDef primary = (landmarkIds ?? new List<string>())
                .Select(DefDatabase<HistoricalLandmarkDef>.GetNamedSilentFail)
                .FirstOrDefault(item => item != null);
            if (primary == null) { appliedLayerIds = new List<string>(); return; }

            int year = primary.scenario?.startAstronomicalYear ?? 0;
            if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate current)) year = current.AstronomicalYear;
            HistoricalEventRuntimeState eventState = primary.relatedEventIds == null
                ? null : HistoricalEventManager.GetEvent(primary.relatedEventIds.FirstOrDefault());
            int stageIndex = -1;
            if (eventState != null && !string.IsNullOrEmpty(eventState.CurrentStageId))
            {
                HistoricalEventDef eventDef = DefDatabase<HistoricalEventDef>.AllDefsListForReading
                    .FirstOrDefault(item => item != null && string.Equals(item.eventId, primary.relatedEventIds?.FirstOrDefault(), StringComparison.Ordinal));
                stageIndex = eventDef?.stages?.FindIndex(item => item != null && item.stageId == eventState.CurrentStageId) ?? -1;
            }

            appliedLayerIds = DefDatabase<HistoricalSceneLayerDef>.AllDefsListForReading
                .Where(layer => layer != null && layer.landmark == primary && layer.template != null
                    && year >= layer.minimumAstronomicalYear && year <= layer.maximumAstronomicalYear
                    && (string.IsNullOrEmpty(layer.historicalEventId)
                        || eventState != null && stageIndex >= layer.minimumEventStageIndex && stageIndex <= layer.maximumEventStageIndex))
                .OrderBy(layer => layer.order).ThenBy(layer => layer.defName, StringComparer.Ordinal)
                .Select(layer => layer.defName).ToList();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "historicalMapSceneDataVersion", 0);
            Scribe_Values.Look(ref scenarioId, "historicalMapSceneScenarioId");
            Scribe_Values.Look(ref templateId, "historicalMapSceneTemplateId");
            Scribe_Values.Look(ref generationSeed, "historicalMapSceneGenerationSeed");
            Scribe_Values.Look(ref generationApplied, "historicalMapSceneGenerationApplied", false);
            Scribe_Collections.Look(ref landmarkIds, "historicalMapSceneLandmarkIds", LookMode.Value);
            Scribe_Collections.Look(ref zones, "historicalMapSceneZones", LookMode.Deep);
            Scribe_Collections.Look(ref approaches, "historicalMapSceneApproaches", LookMode.Deep);
            Scribe_Collections.Look(ref appliedLayerIds, "historicalMapSceneAppliedLayers", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.Saving) dataVersion = DataVersion;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                landmarkIds = landmarkIds ?? new List<string>();
                zones = zones ?? new List<HistoricalSceneZoneState>();
                approaches = approaches ?? new List<HistoricalSceneApproachState>();
                appliedLayerIds = appliedLayerIds ?? new List<string>();
                if (dataVersion != DataVersion) generationApplied = true; // Legacy maps are kept intact; never auto-regenerate.
            }
        }
    }

    public sealed class HistoricalSceneZoneState : IExposable
    {
        private string zoneId;
        private int minX, minZ, maxX, maxZ;
        public string ZoneId => zoneId;
        public void ExposeData()
        {
            Scribe_Values.Look(ref zoneId, "zoneId");
            Scribe_Values.Look(ref minX, "minX"); Scribe_Values.Look(ref minZ, "minZ");
            Scribe_Values.Look(ref maxX, "maxX"); Scribe_Values.Look(ref maxZ, "maxZ");
        }
        public static HistoricalSceneZoneState FromNormalized(HistoricalSceneZoneDef def, int width, int height)
        {
            if (def == null) return null;
            return new HistoricalSceneZoneState
            {
                zoneId = def.zoneId,
                minX = Clamp((int)Math.Floor(def.minX * width), 0, width - 1),
                minZ = Clamp((int)Math.Floor(def.minZ * height), 0, height - 1),
                maxX = Clamp((int)Math.Ceiling(def.maxX * width) - 1, 0, width - 1),
                maxZ = Clamp((int)Math.Ceiling(def.maxZ * height) - 1, 0, height - 1)
            };
        }
        public CellRect ToCellRect(Map map) => map == null ? CellRect.Empty : new CellRect(
            Clamp(minX, 0, map.Size.x - 1), Clamp(minZ, 0, map.Size.z - 1),
            Clamp(maxX, 0, map.Size.x - 1), Clamp(maxZ, 0, map.Size.z - 1));
        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }

    public sealed class HistoricalSceneApproachState : IExposable
    {
        private string approachId;
        private int x, z;
        public string ApproachId => approachId;
        public void ExposeData() { Scribe_Values.Look(ref approachId, "approachId"); Scribe_Values.Look(ref x, "x"); Scribe_Values.Look(ref z, "z"); }
        public static HistoricalSceneApproachState FromNormalized(HistoricalSceneApproachDef def, int width, int height) => def == null ? null : new HistoricalSceneApproachState
        {
            approachId = def.approachId,
            x = Math.Max(0, Math.Min(width - 1, (int)Math.Round(def.x * (width - 1)))),
            z = Math.Max(0, Math.Min(height - 1, (int)Math.Round(def.z * (height - 1))))
        };
        public IntVec3 ToCell(Map map) => map == null ? IntVec3.Invalid
            : new IntVec3(Math.Max(0, Math.Min(map.Size.x - 1, x)), 0, Math.Max(0, Math.Min(map.Size.z - 1, z)));
    }

    public static class HistoricalEventLocationService
    {
        public static bool TryGetLandmarkTile(string landmarkId, out int tileId)
        {
            tileId = -1;
            if (string.IsNullOrWhiteSpace(landmarkId) || Find.World?.worldObjects == null) return false;
            HistoricalLandmarkDef def = DefDatabase<HistoricalLandmarkDef>.GetNamedSilentFail(landmarkId);
            if (def == null || HistoricalScenarioManager.ActiveScenarioDefName != def.scenario?.defName) return false;
            WorldObject markerObject = Find.World.worldObjects.AllWorldObjects
                .FirstOrDefault(item => item is IHistoricalMapLocation marker
                    && marker.HistoricalLandmarkDefName == landmarkId);
            IHistoricalMapLocation marker = markerObject as IHistoricalMapLocation;
            if (marker == null || markerObject == null || !markerObject.Tile.Valid) return false;
            tileId = markerObject.Tile.tileId;
            return tileId >= 0;
        }

        public static bool IsEventLocation(string eventId, int tileId)
        {
            return DefDatabase<HistoricalLandmarkDef>.AllDefsListForReading.Any(item => item != null
                && item.scenario?.defName == HistoricalScenarioManager.ActiveScenarioDefName
                && item.relatedEventIds != null && item.relatedEventIds.Contains(eventId, StringComparer.Ordinal)
                && TryGetLandmarkTile(item.defName, out int resolved) && resolved == tileId);
        }

        public static string GetMapTemplateId(string landmarkId) =>
            DefDatabase<HistoricalLandmarkDef>.GetNamedSilentFail(landmarkId)?.mapTemplate?.defName;

        public static bool TryGetScene(Map map, out HistoricalMapSceneComponent scene)
        {
            scene = map?.GetComponent<HistoricalMapSceneComponent>();
            return scene?.GenerationApplied == true;
        }
    }
}
