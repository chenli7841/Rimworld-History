using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Centralized, saved erosion and repair state for human construction on permanent ancient soil.</summary>
    public sealed class MapComponent_ForestSoilErosion : MapComponent
    {
        private const int CurrentDataVersion = 1;
        private const int SoilCellsPerUpdate = 256;
        private const int FloorsPerUpdate = 64;
        private const int BuildingsPerUpdate = 32;

        private int dataVersion;
        private int soilScanCursor;
        private int floorCursor;
        private int buildingCursor;
        private List<IntVec3> ancientSoilCells;
        private List<FloorErosionState> floorStates = new List<FloorErosionState>();
        private List<int> maintenanceCellIndexes = new List<int>();
        private List<int> buildingIds = new List<int>();
        private List<float> buildingDamageRemainders = new List<float>();
        private List<int> buildingLastTicks = new List<int>();
        private readonly Dictionary<int, FloorErosionState> floorsByIndex = new Dictionary<int, FloorErosionState>();
        private readonly Dictionary<int, FloorMaintenanceMode> maintenanceModes = new Dictionary<int, FloorMaintenanceMode>();
        private readonly Dictionary<int, float> buildingDamageById = new Dictionary<int, float>();
        private readonly Dictionary<int, int> buildingLastTickById = new Dictionary<int, int>();
        private readonly HashSet<int> exposedBuildingIds = new HashSet<int>();
        private readonly HashSet<int> repairRequestedCellIndexes = new HashSet<int>();
        private readonly List<IntVec3> overlayLightCells = new List<IntVec3>();
        private readonly List<IntVec3> overlayMediumCells = new List<IntVec3>();
        private readonly List<IntVec3> overlaySevereCells = new List<IntVec3>();
        private bool initialized;

        public static bool ShowErosionOverlay { get; set; }

        public MapComponent_ForestSoilErosion(Map map) : base(map)
        {
            ForestSoilErosionRegistry.Register(map);
        }

        public int TrackedFloorCount => floorsByIndex.Count;
        public int RepairRequestedCount => repairRequestedCellIndexes.Count;
        public int ExposedBuildingCount => exposedBuildingIds.Count;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ForestSoilErosionRegistry.Register(map);
            EnsureInitialized();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            if ((tick + map.ConstantRandSeed) % ForestSoilErosionUtility.ErosionUpdateInterval != 0) return;

            EnsureInitialized();
            DiscoverAncientSoilStructures(tick);
            ProcessFloorBatch(tick);
            ProcessBuildingBatch(tick);
            if (ShowErosionOverlay) RebuildErosionOverlay();
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            if (!ShowErosionOverlay || !HistoricalScenarioManager.IsScenarioActive) return;
            if (overlayLightCells.Count > 0) GenDraw.DrawFieldEdges(overlayLightCells, new Color(0.9f, 0.85f, 0.15f, 0.7f), null, null, 0);
            if (overlayMediumCells.Count > 0) GenDraw.DrawFieldEdges(overlayMediumCells, new Color(1f, 0.45f, 0.05f, 0.75f), null, null, 0);
            if (overlaySevereCells.Count > 0) GenDraw.DrawFieldEdges(overlaySevereCells, new Color(0.95f, 0.08f, 0.05f, 0.8f), null, null, 0);
        }

        public override void MapRemoved()
        {
            ForestSoilErosionRegistry.Unregister(map);
            base.MapRemoved();
        }

        public void NotifyTerrainChanged(IntVec3 cell)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || !cell.InBounds(map)
                || map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(cell) != true)
                return;

            int index = map.cellIndices.CellToIndex(cell);
            if (ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor))
                EnsureFloorState(index, floor, Find.TickManager?.TicksGame ?? 0);
            else
                RemoveFloorState(index);

            RefreshBuildingAt(cell.GetEdifice(map) as Building);
            RebuildRepairQueue();
        }

        public void NotifyBuildingSpawned(Building building)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || building == null || building.Map != map) return;
            RefreshBuildingAt(building);
        }

        public void NotifyBuildingDespawned(Building building)
        {
            if (building == null) return;
            exposedBuildingIds.Remove(building.thingIDNumber);
            buildingDamageById.Remove(building.thingIDNumber);
            buildingLastTickById.Remove(building.thingIDNumber);
        }

        public FloorMaintenanceMode GetMaintenanceMode(IntVec3 cell)
        {
            int index = IndexOf(cell);
            return index >= 0 && maintenanceModes.TryGetValue(index, out FloorMaintenanceMode mode)
                ? mode : FloorMaintenanceMode.Normal;
        }

        public void SetMaintenanceMode(IntVec3 cell, FloorMaintenanceMode mode)
        {
            int index = IndexOf(cell);
            if (index < 0 || map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(cell) != true) return;
            if (mode == FloorMaintenanceMode.Normal) maintenanceModes.Remove(index);
            else maintenanceModes[index] = mode;
            if (floorsByIndex.TryGetValue(index, out FloorErosionState state))
            {
                state.MaintenanceMode = mode;
                if (mode == FloorMaintenanceMode.Disabled && !state.ManualRepairRequested)
                    state.RepairRequested = false;
            }
            RebuildRepairQueue();
        }

        public void RequestManualRepair(IntVec3 cell)
        {
            int index = IndexOf(cell);
            if (index < 0 || !ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor)) return;
            FloorErosionState state = EnsureFloorState(index, floor, Find.TickManager?.TicksGame ?? 0);
            state.ManualRepairRequested = true;
            state.RepairRequested = true;
            repairRequestedCellIndexes.Add(index);
        }

        public bool IsRepairRequested(IntVec3 cell)
        {
            int index = IndexOf(cell);
            return index >= 0 && repairRequestedCellIndexes.Contains(index)
                && floorsByIndex.TryGetValue(index, out FloorErosionState state) && state.RepairRequested
                && ForestSoilErosionUtility.IsConstructedFloor(map, cell, out _);
        }

        public IEnumerable<IntVec3> GetRepairWorkCells()
        {
            foreach (int index in repairRequestedCellIndexes.ToArray())
            {
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                if (IsRepairRequested(cell)) yield return cell;
            }
        }

        /// <summary>Returns true when this cell's floor repair is complete or no longer valid.</summary>
        public bool ApplyRepairWork(IntVec3 cell, float workAmount)
        {
            int index = IndexOf(cell);
            if (index < 0 || !floorsByIndex.TryGetValue(index, out FloorErosionState state)
                || !ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor))
            {
                RemoveFloorState(index);
                return true;
            }
            if (!state.RepairRequested) return true;
            if (!string.Equals(state.TerrainDefName, floor.defName, StringComparison.Ordinal))
            {
                state = EnsureFloorState(index, floor, Find.TickManager?.TicksGame ?? 0);
                return true;
            }

            float repairFraction = Math.Max(0f, workAmount) / ForestSoilErosionUtility.FullFloorRepairWork;
            state.ErosionProgress = Math.Max(0f, state.ErosionProgress - repairFraction);
            if (state.ErosionProgress <= 0.0001f)
            {
                state.ErosionProgress = 0f;
                state.RepairRequested = false;
                state.ManualRepairRequested = false;
                repairRequestedCellIndexes.Remove(index);
                return true;
            }
            return false;
        }

        public bool TrySetErosionProgress(IntVec3 cell, float progress)
        {
            int index = IndexOf(cell);
            if (index < 0 || !ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor)) return false;
            FloorErosionState state = EnsureFloorState(index, floor, Find.TickManager?.TicksGame ?? 0);
            state.ErosionProgress = Mathf.Clamp01(progress);
            if (state.ErosionProgress <= 0f)
            {
                state.RepairRequested = false;
                state.ManualRepairRequested = false;
            }
            else UpdateRepairRequest(state);
            RebuildRepairQueue();
            if (state.ErosionProgress >= 1f) DestroyFloorAt(cell, state);
            return true;
        }

        public int SimulateFloorErosionDays(float days)
        {
            int changed = 0;
            foreach (FloorErosionState state in floorStates.ToArray())
            {
                if (state == null) continue;
                IntVec3 cell = map.cellIndices.IndexToCell(state.CellIndex);
                if (!ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor)) continue;
                float dailyRate = 1f / ForestSoilErosionUtility.FloorDurabilityDays(floor);
                state.ErosionProgress = Mathf.Clamp01(state.ErosionProgress + dailyRate * Math.Max(0f, days)
                    * ForestSoilErosionUtility.RootPressureFactor(map, cell)
                    * ForestSoilErosionUtility.MoistureFactor(map));
                UpdateRepairRequest(state);
                changed++;
                if (state.ErosionProgress >= 1f) DestroyFloorAt(cell, state);
            }
            RebuildRepairQueue();
            return changed;
        }

        public void RebuildErosionOverlay()
        {
            overlayLightCells.Clear(); overlayMediumCells.Clear(); overlaySevereCells.Clear();
            foreach (FloorErosionState state in floorsByIndex.Values)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(state.CellIndex);
                if (state.ErosionProgress >= 0.8f) overlaySevereCells.Add(cell);
                else if (state.ErosionProgress >= 0.5f) overlayMediumCells.Add(cell);
                else if (state.ErosionProgress >= 0.25f) overlayLightCells.Add(cell);
            }
        }

        public int ForceRepairRequestedFloors()
        {
            int count = 0;
            foreach (int index in repairRequestedCellIndexes.ToArray())
            {
                if (!floorsByIndex.TryGetValue(index, out FloorErosionState state)) continue;
                state.ErosionProgress = 0f;
                state.RepairRequested = false;
                state.ManualRepairRequested = false;
                count++;
            }
            repairRequestedCellIndexes.Clear();
            return count;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                dataVersion = CurrentDataVersion;
                floorStates = floorsByIndex.Values.ToList();
                maintenanceCellIndexes = maintenanceModes.Keys.ToList();
                buildingIds = buildingDamageById.Keys.ToList();
                buildingDamageRemainders = buildingIds.Select(id => buildingDamageById[id]).ToList();
                buildingLastTicks = buildingIds.Select(id => buildingLastTickById.TryGetValue(id, out int tick) ? tick : 0).ToList();
            }

            Scribe_Values.Look(ref dataVersion, "forestSoilErosionDataVersion", 0);
            Scribe_Values.Look(ref soilScanCursor, "forestSoilErosionSoilScanCursor", 0);
            Scribe_Values.Look(ref floorCursor, "forestSoilErosionFloorCursor", 0);
            Scribe_Values.Look(ref buildingCursor, "forestSoilErosionBuildingCursor", 0);
            Scribe_Collections.Look(ref floorStates, "forestSoilFloorErosionStates", LookMode.Deep);
            Scribe_Collections.Look(ref maintenanceCellIndexes, "forestSoilMaintenanceCellIndexes", LookMode.Value);
            Scribe_Collections.Look(ref buildingIds, "forestSoilBuildingIds", LookMode.Value);
            Scribe_Collections.Look(ref buildingDamageRemainders, "forestSoilBuildingDamageRemainders", LookMode.Value);
            Scribe_Collections.Look(ref buildingLastTicks, "forestSoilBuildingLastTicks", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (dataVersion != CurrentDataVersion)
                {
                    floorStates = new List<FloorErosionState>();
                    maintenanceCellIndexes = new List<int>();
                    buildingIds = new List<int>();
                    buildingDamageRemainders = new List<float>();
                    buildingLastTicks = new List<int>();
                    dataVersion = CurrentDataVersion;
                }
                RebuildRuntimeIndexes();
                ancientSoilCells = null;
                initialized = false;
                ForestSoilErosionRegistry.Register(map);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            MapComponent_AncientForestSoil soil = map.GetComponent<MapComponent_AncientForestSoil>();
            if (soil == null) return;
            ancientSoilCells = soil.GetAncientForestSoilCells().ToList();
            int tick = Find.TickManager?.TicksGame ?? 0;
            foreach (IntVec3 cell in ancientSoilCells)
            {
                if (ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor))
                    EnsureFloorState(IndexOf(cell), floor, tick);
            }
            RebuildBuildingsFromMap();
            RebuildRepairQueue();
            initialized = true;
        }

        private void DiscoverAncientSoilStructures(int tick)
        {
            if (ancientSoilCells == null || ancientSoilCells.Count == 0) return;
            int count = Math.Min(SoilCellsPerUpdate, ancientSoilCells.Count);
            for (int i = 0; i < count; i++)
            {
                int cursor = soilScanCursor++;
                if (soilScanCursor >= ancientSoilCells.Count) soilScanCursor = 0;
                IntVec3 cell = ancientSoilCells[cursor];
                int index = IndexOf(cell);
                if (ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor))
                    EnsureFloorState(index, floor, tick);
                else
                    RemoveFloorState(index);
                RefreshBuildingAt(cell.GetEdifice(map) as Building);
            }
        }

        private void ProcessFloorBatch(int tick)
        {
            if (floorStates.Count == 0) return;
            int count = Math.Min(FloorsPerUpdate, floorStates.Count);
            for (int i = 0; i < count && floorStates.Count > 0; i++)
            {
                if (floorCursor >= floorStates.Count) floorCursor = 0;
                FloorErosionState state = floorStates[floorCursor++];
                if (state == null) continue;
                IntVec3 cell = map.cellIndices.IndexToCell(state.CellIndex);
                if (!ForestSoilErosionUtility.IsExposedAncientSoil(map, cell)
                    || !ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor)
                    || !string.Equals(floor.defName, state.TerrainDefName, StringComparison.Ordinal))
                {
                    if (ForestSoilErosionUtility.IsConstructedFloor(map, cell, out floor))
                        EnsureFloorState(state.CellIndex, floor, tick);
                    else RemoveFloorState(state.CellIndex);
                    continue;
                }

                int elapsed = Math.Max(0, tick - state.LastUpdatedTick);
                state.LastUpdatedTick = tick;
                if (elapsed == 0) continue;
                float dailyRate = 1f / ForestSoilErosionUtility.FloorDurabilityDays(floor);
                float root = ForestSoilErosionUtility.RootPressureFactor(map, cell);
                float moisture = ForestSoilErosionUtility.MoistureFactor(map);
                state.ErosionProgress = Mathf.Clamp01(state.ErosionProgress
                    + dailyRate * (elapsed / (float)ForestSoilErosionUtility.TicksPerDay) * root * moisture);
                UpdateRepairRequest(state);
                if (state.ErosionProgress >= 1f) DestroyFloorAt(cell, state);
            }
            RebuildRepairQueue();
        }

        private void ProcessBuildingBatch(int tick)
        {
            if (exposedBuildingIds.Count == 0) return;
            int[] ids = exposedBuildingIds.ToArray();
            if (ids.Length == 0) return;
            int count = Math.Min(BuildingsPerUpdate, ids.Length);
            for (int i = 0; i < count; i++)
            {
                if (buildingCursor >= ids.Length) buildingCursor = 0;
                int id = ids[buildingCursor++];
                Building building = FindBuilding(id);
                if (!IsEligibleExposedBuilding(building))
                {
                    exposedBuildingIds.Remove(id);
                    buildingDamageById.Remove(id);
                    buildingLastTickById.Remove(id);
                    continue;
                }

                int lastTick = buildingLastTickById.TryGetValue(id, out int savedTick) ? savedTick : tick;
                int elapsed = Math.Max(0, tick - lastTick);
                if (elapsed <= 0) continue;
                buildingLastTickById[id] = tick;
                float exposure = GetExposureRatio(building);
                if (exposure <= 0f) continue;
                IntVec3 exposedCell = building.OccupiedRect().Cells
                    .FirstOrDefault(cell => ForestSoilErosionUtility.IsExposedAncientSoil(map, cell));
                float root = ForestSoilErosionUtility.RootPressureFactor(map, exposedCell);
                float moisture = ForestSoilErosionUtility.MoistureFactor(map);
                float damage = building.def.BaseMaxHitPoints
                    * ForestSoilErosionUtility.BuildingDamageFractionPerDay(building)
                    * exposure * root * moisture
                    * (elapsed / (float)ForestSoilErosionUtility.TicksPerDay);
                float total = (buildingDamageById.TryGetValue(id, out float remainder) ? remainder : 0f) + damage;
                int wholeDamage = Mathf.FloorToInt(total);
                buildingDamageById[id] = total - wholeDamage;
                if (wholeDamage > 0 && !building.Destroyed)
                    building.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, wholeDamage));
            }
        }

        private FloorErosionState EnsureFloorState(int index, TerrainDef floor, int tick)
        {
            if (index < 0 || floor == null) return null;
            FloorMaintenanceMode mode = maintenanceModes.TryGetValue(index, out FloorMaintenanceMode savedMode)
                ? savedMode : FloorMaintenanceMode.Normal;
            if (floorsByIndex.TryGetValue(index, out FloorErosionState existing))
            {
                if (string.Equals(existing.TerrainDefName, floor.defName, StringComparison.Ordinal))
                {
                    existing.MaintenanceMode = mode;
                    return existing;
                }
                RemoveFloorState(index);
            }

            FloorErosionState state = new FloorErosionState(index, floor.defName, mode, tick);
            floorsByIndex[index] = state;
            floorStates.Add(state);
            return state;
        }

        private void RemoveFloorState(int index)
        {
            if (index < 0) return;
            repairRequestedCellIndexes.Remove(index);
            if (floorsByIndex.TryGetValue(index, out FloorErosionState state))
            {
                floorsByIndex.Remove(index);
                floorStates.Remove(state);
                if (floorCursor >= floorStates.Count) floorCursor = 0;
            }
        }

        private void UpdateRepairRequest(FloorErosionState state)
        {
            if (state == null) return;
            if (state.MaintenanceMode != FloorMaintenanceMode.Disabled)
            {
                float threshold = state.MaintenanceMode == FloorMaintenanceMode.Priority
                    ? ForestSoilErosionUtility.PriorityRepairThreshold : ForestSoilErosionUtility.DefaultRepairThreshold;
                if (state.ErosionProgress >= threshold) state.RepairRequested = true;
            }
            else if (!state.ManualRepairRequested)
            {
                state.RepairRequested = false;
            }
        }

        private void RebuildRepairQueue()
        {
            repairRequestedCellIndexes.Clear();
            foreach (FloorErosionState state in floorStates)
                if (state?.RepairRequested == true) repairRequestedCellIndexes.Add(state.CellIndex);
        }

        private void DestroyFloorAt(IntVec3 cell, FloorErosionState state)
        {
            if (!ForestSoilErosionUtility.IsConstructedFloor(map, cell, out _))
            {
                RemoveFloorState(state.CellIndex);
                return;
            }

            map.terrainGrid.RemoveTopLayer(cell, false);
            TerrainDef activeSoil = DefDatabase<TerrainDef>.GetNamedSilentFail("HR_AncientLivingForestSoil");
            if (activeSoil != null && map.terrainGrid.TopTerrainAt(cell) != activeSoil)
                map.terrainGrid.SetTerrain(cell, activeSoil);
            RemoveFloorState(state.CellIndex);
            RefreshBuildingAt(cell.GetEdifice(map) as Building);
            Log.Message("[Historical RimWorld] Ancient-soil erosion destroyed floor " + state.TerrainDefName + " at " + cell + ".");
        }

        private void RefreshBuildingAt(Building building)
        {
            if (building == null) return;
            int id = building.thingIDNumber;
            if (!IsEligibleExposedBuilding(building))
            {
                exposedBuildingIds.Remove(id);
                buildingDamageById.Remove(id);
                buildingLastTickById.Remove(id);
                return;
            }

            exposedBuildingIds.Add(id);
            if (!buildingLastTickById.ContainsKey(id)) buildingLastTickById[id] = Find.TickManager?.TicksGame ?? 0;
            if (!buildingDamageById.ContainsKey(id)) buildingDamageById[id] = 0f;
        }

        private bool IsEligibleExposedBuilding(Building building)
        {
            if (building == null || building.Destroyed || !building.Spawned || building.Map != map
                || building.def?.building == null || !building.def.building.isEdifice
                || building.def.building.isNaturalRock)
                return false;
            return GetExposureRatio(building) > 0f;
        }

        private float GetExposureRatio(Building building)
        {
            if (building == null) return 0f;
            int total = 0;
            int exposed = 0;
            foreach (IntVec3 cell in building.OccupiedRect().Cells)
            {
                if (!cell.InBounds(map)) continue;
                total++;
                if (ForestSoilErosionUtility.IsExposedAncientSoil(map, cell)) exposed++;
            }
            return total == 0 ? 0f : exposed / (float)total;
        }

        private Building FindBuilding(int id)
        {
            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                if (thing?.thingIDNumber == id) return thing as Building;
            return null;
        }

        private void RebuildRuntimeIndexes()
        {
            floorsByIndex.Clear(); maintenanceModes.Clear(); buildingDamageById.Clear();
            buildingLastTickById.Clear(); exposedBuildingIds.Clear(); repairRequestedCellIndexes.Clear();
            floorStates = floorStates ?? new List<FloorErosionState>();
            foreach (FloorErosionState state in floorStates.ToArray())
            {
                int cellCount = map.Size.x * map.Size.z;
                if (state == null || (uint)state.CellIndex >= (uint)cellCount || floorsByIndex.ContainsKey(state.CellIndex))
                {
                    floorStates.Remove(state);
                    continue;
                }
                IntVec3 cell = map.cellIndices.IndexToCell(state.CellIndex);
                if (!cell.InBounds(map) || map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(cell) != true
                    || !ForestSoilErosionUtility.IsConstructedFloor(map, cell, out TerrainDef floor))
                {
                    floorStates.Remove(state);
                    continue;
                }
                if (!string.Equals(state.TerrainDefName, floor.defName, StringComparison.Ordinal))
                {
                    state.TerrainDefName = floor.defName;
                    state.ErosionProgress = 0f;
                    state.RepairRequested = false;
                    state.ManualRepairRequested = false;
                    state.LastUpdatedTick = Find.TickManager?.TicksGame ?? 0;
                }
                floorsByIndex[state.CellIndex] = state;
                if (state.MaintenanceMode != FloorMaintenanceMode.Normal)
                    maintenanceModes[state.CellIndex] = state.MaintenanceMode;
                if (state.RepairRequested) repairRequestedCellIndexes.Add(state.CellIndex);
            }

            if (maintenanceCellIndexes != null)
                foreach (int index in maintenanceCellIndexes)
                    if (!maintenanceModes.ContainsKey(index)) maintenanceModes[index] = FloorMaintenanceMode.Priority;

            if (buildingIds != null && buildingDamageRemainders != null)
                for (int i = 0; i < Math.Min(buildingIds.Count, buildingDamageRemainders.Count); i++)
                {
                    int id = buildingIds[i];
                    buildingDamageById[id] = Mathf.Clamp(buildingDamageRemainders[i], 0f, 0.9999f);
                    buildingLastTickById[id] = buildingLastTicks != null && i < buildingLastTicks.Count ? buildingLastTicks[i] : 0;
                }
            floorStates = floorsByIndex.Values.ToList();
            maintenanceCellIndexes = new List<int>(); buildingIds = new List<int>();
            buildingDamageRemainders = new List<float>(); buildingLastTicks = new List<int>();
        }

        private void RebuildBuildingsFromMap()
        {
            exposedBuildingIds.Clear();
            HashSet<int> liveBuildingIds = new HashSet<int>();
            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                if (thing is Building building)
                {
                    liveBuildingIds.Add(building.thingIDNumber);
                    RefreshBuildingAt(building);
                }
            foreach (int id in buildingDamageById.Keys.Where(id => !liveBuildingIds.Contains(id)).ToArray())
            {
                buildingDamageById.Remove(id);
                buildingLastTickById.Remove(id);
            }
        }

        private int IndexOf(IntVec3 cell) => cell.InBounds(map) ? map.cellIndices.CellToIndex(cell) : -1;
    }
}
