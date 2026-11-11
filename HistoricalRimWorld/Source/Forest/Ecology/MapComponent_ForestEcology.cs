using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HistoricalRimWorld.FogOfWar;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Cached, dynamic regions of actual forest vegetation. Active soil remains owned by HR-042.</summary>
    public sealed class MapComponent_ForestEcology : MapComponent
    {
        public const int RegionUpdateInterval = 2500;
        public const int SnapshotInterval = 60000;
        public const int SnapshotLimit = 30;
        public const int MinimumStatusDuration = 120000;

        private readonly Dictionary<int, ForestEcologyRegion> regions = new Dictionary<int, ForestEcologyRegion>();
        private readonly List<ForestEcologyRegion> orderedRegions = new List<ForestEcologyRegion>();
        private readonly HashSet<int> dirtyRegionIds = new HashSet<int>();
        private readonly HashSet<int> dirtyCellIndexes = new HashSet<int>();
        private readonly HashSet<int> cachedVegetationCellIndexes = new HashSet<int>();
        private readonly List<List<IntVec3>> highlightBatches = new List<List<IntVec3>>();
        private readonly List<Color> highlightColors = new List<Color>();
        private readonly List<ForestEcologyRegion> knownRegions = new List<ForestEcologyRegion>();

        private int[] regionIdByCell;
        private int nextRegionId = 1;
        private int lastConnectivityUpdateTick = -1;
        private int lastSnapshotTick = -1;
        private int lastKnowledgeRefreshTick = -1;
        private int lastHighlightRefreshTick = -1;
        private int selectedRegionId;
        private bool showAllKnownRegions;
        private bool fullRebuildRequested = true;
        private bool initialized;
        private long lastRebuildMilliseconds;
        private int lastProcessedVegetationCount;
        private List<ForestEcologyRegion> savedRegionHistory = new List<ForestEcologyRegion>();
        private readonly Dictionary<int, ForestEcologyRegion> historyByAnchor = new Dictionary<int, ForestEcologyRegion>();

        public MapComponent_ForestEcology(Map map) : base(map)
        {
            AllocateRegionIndex();
            ForestSoilErosionRegistry.Register(map);
        }

        public IReadOnlyList<ForestEcologyRegion> GetAllRegions() => orderedRegions;
        public IReadOnlyList<ForestEcologyRegion> GetKnownRegions() => knownRegions;
        public int RegionCount => regions.Count;
        public int DirtyRegionCount => dirtyRegionIds.Count;
        public long LastRebuildMilliseconds => lastRebuildMilliseconds;
        public int LastProcessedVegetationCount => lastProcessedVegetationCount;
        public int LastUpdatedTick => lastConnectivityUpdateTick;
        public int GetTotalForestArea() => regions.Values.Sum(region => region.CellCount);
        public int GetTotalTreeCount() => regions.Values.Sum(region => region.TreeCount);
        /// <summary>Aggregate logical tree coverage as a 0..1 fraction of map cells.</summary>
        public float GetTotalTreeCoverage() => map != null && map.Size.x > 0 && map.Size.z > 0
            ? Math.Min(1f, GetTotalTreeCount() / (float)(map.Size.x * map.Size.z)) : 0f;

        public ForestEcologyRegion GetRegionAt(IntVec3 cell)
        {
            int index = IndexOf(cell);
            if (index < 0 || regionIdByCell == null) return null;
            int regionId = regionIdByCell[index];
            return regionId > 0 && regions.TryGetValue(regionId, out ForestEcologyRegion region) ? region : null;
        }

        public ForestEcologyRegion GetRegionById(int regionId)
        {
            return regionId > 0 && regions.TryGetValue(regionId, out ForestEcologyRegion region) ? region : null;
        }

        public ForestEcologyRegion GetLargestForest()
        {
            ForestEcologyRegion best = null;
            foreach (ForestEcologyRegion region in regions.Values)
                if (best == null || region.CellCount > best.CellCount
                    || (region.CellCount == best.CellCount && region.RegionId < best.RegionId)) best = region;
            return best;
        }

        public void RequestFullRebuild()
        {
            fullRebuildRequested = true;
            dirtyRegionIds.Clear();
            dirtyCellIndexes.Clear();
        }

        public void MarkRegionDirty(IntVec3 cell)
        {
            int index = IndexOf(cell);
            if (index < 0) return;
            dirtyCellIndexes.Add(index);
            if (regionIdByCell == null) return;

            int width = map.Size.x;
            for (int dz = -ForestConnectivityUtility.MaximumPathSteps; dz <= ForestConnectivityUtility.MaximumPathSteps; dz++)
            for (int dx = -ForestConnectivityUtility.MaximumPathSteps; dx <= ForestConnectivityUtility.MaximumPathSteps; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dz) > ForestConnectivityUtility.MaximumPathSteps) continue;
                IntVec3 nearby = new IntVec3(cell.x + dx, 0, cell.z + dz);
                int nearbyIndex = IndexOf(nearby);
                if (nearbyIndex < 0) continue;
                int regionId = regionIdByCell[nearbyIndex];
                if (regionId > 0) dirtyRegionIds.Add(regionId);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ForestSoilErosionRegistry.Register(map);
            AllocateRegionIndex();
            fullRebuildRequested = true;
        }

        public override void MapRemoved()
        {
            ForestSoilErosionRegistry.Unregister(map);
            base.MapRemoved();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            if ((tick + map.ConstantRandSeed) % RegionUpdateInterval != 0) return;

            if (initialized && !fullRebuildRequested) DetectVegetationTopologyChanges();
            if (!initialized || fullRebuildRequested) RebuildAllRegions();
            else if (dirtyCellIndexes.Count > 0 || dirtyRegionIds.Count > 0) RebuildDirtyRegions();

            RefreshRegionMetrics(tick);
            if (lastKnowledgeRefreshTick < 0 || tick - lastKnowledgeRefreshTick >= RegionUpdateInterval)
                RefreshPlayerKnowledge(tick);
            if (lastSnapshotTick < 0 || tick - lastSnapshotTick >= SnapshotInterval)
                RecordDailySnapshots(tick);
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            if ((!showAllKnownRegions && selectedRegionId <= 0) || !HistoricalScenarioManager.IsScenarioActive) return;
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (lastHighlightRefreshTick < 0 || tick - lastHighlightRefreshTick >= RegionUpdateInterval)
                RebuildHighlightCache(tick);
            for (int i = 0; i < highlightBatches.Count; i++)
                if (highlightBatches[i].Count > 0)
                    GenDraw.DrawFieldEdges(highlightBatches[i], highlightColors[i], null, null, 0);
        }

        public void SelectRegion(int regionId)
        {
            selectedRegionId = regions.ContainsKey(regionId) ? regionId : 0;
            showAllKnownRegions = false;
            lastHighlightRefreshTick = -1;
        }

        public void ToggleAllKnownRegionHighlight()
        {
            showAllKnownRegions = !showAllKnownRegions;
            if (showAllKnownRegions) selectedRegionId = 0;
            lastHighlightRefreshTick = -1;
        }

        public void ClearHighlight()
        {
            selectedRegionId = 0;
            showAllKnownRegions = false;
            highlightBatches.Clear();
            highlightColors.Clear();
            lastHighlightRefreshTick = -1;
        }

        public bool IsRegionFullyExplored(ForestEcologyRegion region) => region?.FullyExplored == true;

        public static float GetHabitatSuitability(ForestEcologyRegion region, ThingDef animalDef)
        {
            return ForestConnectivityUtility.GetHabitatSuitability(region, animalDef);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
                savedRegionHistory = regions.Values.Select(region => region.PersistenceCopy()).ToList();
            Scribe_Values.Look(ref nextRegionId, "forestEcologyNextRegionId", 1);
            Scribe_Collections.Look(ref savedRegionHistory, "forestEcologyRegionHistory", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                savedRegionHistory = savedRegionHistory ?? new List<ForestEcologyRegion>();
                historyByAnchor.Clear();
                foreach (ForestEcologyRegion region in savedRegionHistory)
                    if (region != null && region.RegionId > 0 && region.AnchorCellIndex >= 0
                        && !historyByAnchor.ContainsKey(region.AnchorCellIndex)) historyByAnchor.Add(region.AnchorCellIndex, region);
                AllocateRegionIndex();
                RequestFullRebuild();
                initialized = false;
            }
        }

        private void RebuildAllRegions()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Dictionary<int, ForestEcologyRegion> previous = regions.ToDictionary(pair => pair.Key, pair => pair.Value);
            List<ForestNode> nodes = GatherVegetationNodes(null);
            lastProcessedVegetationCount = nodes.Count;
            List<RegionBuildBuffer> components = BuildComponents(nodes);
            regions.Clear();
            Array.Clear(regionIdByCell, 0, regionIdByCell.Length);
            AssignComponents(components, previous, true);
            RefreshCachedVegetationIndexes();
            dirtyRegionIds.Clear();
            dirtyCellIndexes.Clear();
            fullRebuildRequested = false;
            initialized = true;
            lastConnectivityUpdateTick = Find.TickManager?.TicksGame ?? 0;
            RefreshOrdering();
            stopwatch.Stop();
            lastRebuildMilliseconds = stopwatch.ElapsedMilliseconds;
            Log.Message("[Historical RimWorld][HR-046] Rebuilt " + regions.Count + " forest region(s) from "
                + lastProcessedVegetationCount + " vegetation cell(s) in " + lastRebuildMilliseconds + " ms.");
        }

        private void RebuildDirtyRegions()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var affectedIds = new HashSet<int>(dirtyRegionIds);
            int width = map.Size.x;
            var candidateIndexes = new HashSet<int>();
            foreach (int id in affectedIds)
                if (regions.TryGetValue(id, out ForestEcologyRegion oldRegion))
                    foreach (int cellIndex in oldRegion.VegetationCellIndexes) candidateIndexes.Add(cellIndex);

            foreach (int dirtyIndex in dirtyCellIndexes)
            {
                int x = dirtyIndex % width;
                int z = dirtyIndex / width;
                for (int dz = -ForestConnectivityUtility.MaximumPathSteps; dz <= ForestConnectivityUtility.MaximumPathSteps; dz++)
                for (int dx = -ForestConnectivityUtility.MaximumPathSteps; dx <= ForestConnectivityUtility.MaximumPathSteps; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dz) > ForestConnectivityUtility.MaximumPathSteps) continue;
                    int nx = x + dx;
                    int nz = z + dz;
                    if ((uint)nx >= (uint)map.Size.x || (uint)nz >= (uint)map.Size.z) continue;
                    int nearbyIndex = nz * width + nx;
                    candidateIndexes.Add(nearbyIndex);
                    int oldId = regionIdByCell[nearbyIndex];
                    if (oldId > 0 && regions.TryGetValue(oldId, out ForestEcologyRegion oldRegion))
                    {
                        affectedIds.Add(oldId);
                        foreach (int member in oldRegion.VegetationCellIndexes) candidateIndexes.Add(member);
                    }
                }
            }

            // The affected region is rebuilt as one local batch; unrelated region cells keep their indexes and IDs.
            List<ForestNode> nodes = GatherVegetationNodes(candidateIndexes);
            lastProcessedVegetationCount = nodes.Count;
            List<RegionBuildBuffer> components = BuildComponents(nodes);
            var previous = new Dictionary<int, ForestEcologyRegion>();
            foreach (int id in affectedIds)
                if (regions.TryGetValue(id, out ForestEcologyRegion oldRegion)) previous[id] = oldRegion;

            foreach (ForestEcologyRegion oldRegion in previous.Values)
            {
                foreach (int cellIndex in oldRegion.CellIndexes)
                    if ((uint)cellIndex < (uint)regionIdByCell.Length && regionIdByCell[cellIndex] == oldRegion.RegionId)
                        regionIdByCell[cellIndex] = 0;
                regions.Remove(oldRegion.RegionId);
            }

            AssignComponents(components, previous, false);
            RefreshCachedVegetationIndexes();
            dirtyRegionIds.Clear();
            dirtyCellIndexes.Clear();
            lastConnectivityUpdateTick = Find.TickManager?.TicksGame ?? 0;
            RefreshOrdering();
            stopwatch.Stop();
            lastRebuildMilliseconds = stopwatch.ElapsedMilliseconds;
        }

        private List<ForestNode> GatherVegetationNodes(HashSet<int> scope)
        {
            var treePlants = new List<Plant>();
            var understoryPlants = new List<Plant>();
            List<Thing> plants = map.listerThings?.ThingsInGroup(ThingRequestGroup.Plant);
            if (plants != null)
            {
                foreach (Thing thing in plants)
                {
                    Plant plant = thing as Plant;
                    if (plant == null || plant.Destroyed || !plant.Spawned) continue;
                    int index = IndexOf(plant.Position);
                    if (index < 0 || (scope != null && !scope.Contains(index))) continue;
                    if (ForestConnectivityUtility.IsTree(plant)) treePlants.Add(plant);
                    else if (ForestConnectivityUtility.IsWildUnderstoryPlant(plant, map)) understoryPlants.Add(plant);
                }
            }

            var treePositions = new HashSet<int>(treePlants.Select(plant => IndexOf(plant.Position)));
            var nodes = new List<ForestNode>(treePlants.Count + understoryPlants.Count);
            var occupied = new HashSet<int>();
            foreach (Plant plant in treePlants)
            {
                int index = IndexOf(plant.Position);
                if (index >= 0 && occupied.Add(index)) nodes.Add(new ForestNode(index, plant));
            }
            foreach (Plant plant in understoryPlants)
            {
                int index = IndexOf(plant.Position);
                if (index < 0 || occupied.Contains(index)
                    || !ForestConnectivityUtility.HasNaturalTreeWithinConnectivityPath(map, index, treePositions)) continue;
                occupied.Add(index);
                nodes.Add(new ForestNode(index, plant));
            }
            nodes.Sort((a, b) => a.CellIndex.CompareTo(b.CellIndex));
            return nodes;
        }

        private void DetectVegetationTopologyChanges()
        {
            List<ForestNode> currentNodes = GatherVegetationNodes(null);
            var current = new HashSet<int>(currentNodes.Select(node => node.CellIndex));
            foreach (int index in current)
                if (!cachedVegetationCellIndexes.Contains(index))
                    MarkRegionDirty(new IntVec3(index % map.Size.x, 0, index / map.Size.x));
            foreach (int index in cachedVegetationCellIndexes)
                if (!current.Contains(index))
                    MarkRegionDirty(new IntVec3(index % map.Size.x, 0, index / map.Size.x));
        }

        private void RefreshCachedVegetationIndexes()
        {
            cachedVegetationCellIndexes.Clear();
            foreach (ForestEcologyRegion region in regions.Values)
                foreach (int index in region.VegetationCellIndexes)
                    cachedVegetationCellIndexes.Add(index);
        }

        private List<RegionBuildBuffer> BuildComponents(List<ForestNode> nodes)
        {
            var buffers = new List<RegionBuildBuffer>();
            if (nodes.Count == 0) return buffers;
            var nodeByCell = new Dictionary<int, int>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++) nodeByCell[nodes[i].CellIndex] = i;
            var unionFind = new UnionFind(nodes.Count);
            var links = new List<ForestConnectivityPath>();
            for (int i = 0; i < nodes.Count; i++)
            {
                List<ForestConnectivityPath> fromNode = ForestConnectivityUtility.FindLinksFromNode(map,
                    nodes[i].CellIndex, i, map.Size.x, map.Size.z, nodeByCell);
                foreach (ForestConnectivityPath link in fromNode)
                {
                    unionFind.Union(i, link.TargetNodeIndex);
                    links.Add(link);
                }
            }

            var byRoot = new Dictionary<int, RegionBuildBuffer>();
            for (int i = 0; i < nodes.Count; i++)
            {
                int root = unionFind.Find(i);
                if (!byRoot.TryGetValue(root, out RegionBuildBuffer buffer))
                {
                    buffer = new RegionBuildBuffer();
                    byRoot.Add(root, buffer);
                    buffers.Add(buffer);
                }
                buffer.VegetationIndexes.Add(nodes[i].CellIndex);
                buffer.CellIndexes.Add(nodes[i].CellIndex);
                buffer.Plants.Add(nodes[i].Plant);
            }
            foreach (ForestConnectivityPath link in links)
            {
                int root = unionFind.Find(link.TargetNodeIndex);
                RegionBuildBuffer buffer = byRoot[root];
                foreach (int index in link.CellIndexes) buffer.CellIndexes.Add(index);
            }
            buffers.Sort((a, b) => a.MinimumCellIndex.CompareTo(b.MinimumCellIndex));
            return buffers;
        }

        private void AssignComponents(List<RegionBuildBuffer> components,
            Dictionary<int, ForestEcologyRegion> previous, bool initialOrFull)
        {
            var overlapsByComponent = new Dictionary<int, Dictionary<int, int>>();
            var primaryComponentByOldId = new Dictionary<int, int>();
            foreach (ForestEcologyRegion oldRegion in previous.Values)
            {
                int bestComponent = -1;
                for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
                {
                    int overlap = CountOverlap(components[componentIndex].CellIndexes, oldRegion.CellIndexes);
                    if (overlap > 0)
                    {
                        if (!overlapsByComponent.TryGetValue(componentIndex, out Dictionary<int, int> byOldId))
                            overlapsByComponent[componentIndex] = byOldId = new Dictionary<int, int>();
                        byOldId[oldRegion.RegionId] = overlap;
                    }
                    int componentArea = components[componentIndex].CellIndexes.Count;
                    int bestArea = bestComponent < 0 ? -1 : components[bestComponent].CellIndexes.Count;
                    if (overlap > 0 && (componentArea > bestArea || (componentArea == bestArea
                        && components[componentIndex].MinimumCellIndex < components[bestComponent].MinimumCellIndex)))
                    {
                        bestComponent = componentIndex;
                    }
                }
                if (bestComponent >= 0) primaryComponentByOldId[oldRegion.RegionId] = bestComponent;
            }

            var claimed = new HashSet<int>();
            for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                RegionBuildBuffer buffer = components[componentIndex];
                ForestEcologyRegion matched = null;
                if (overlapsByComponent.TryGetValue(componentIndex, out Dictionary<int, int> overlaps))
                {
                    matched = overlaps.Keys
                        .Where(id => primaryComponentByOldId.TryGetValue(id, out int primary) && primary == componentIndex
                            && previous.ContainsKey(id))
                        .Select(id => previous[id])
                        .OrderByDescending(region => region.CellCount)
                        .ThenBy(region => region.RegionId)
                        .FirstOrDefault(region => !claimed.Contains(region.RegionId));
                }

                if (matched == null && initialOrFull)
                {
                    matched = buffer.CellIndexes
                        .Where(historyByAnchor.ContainsKey)
                        .Select(index => historyByAnchor[index])
                        .OrderByDescending(region => region.CellCount)
                        .ThenBy(region => region.RegionId)
                        .FirstOrDefault(region => !claimed.Contains(region.RegionId));
                }

                var region = new ForestEcologyRegion();
                if (matched != null)
                {
                    region.RegionId = matched.RegionId;
                    region.CopyHistoryFrom(matched);
                    claimed.Add(matched.RegionId);
                    historyByAnchor.Remove(matched.AnchorCellIndex);
                }
                else region.RegionId = nextRegionId++;

                List<int> cells = buffer.CellIndexes.OrderBy(index => index).ToList();
                List<int> vegetation = buffer.VegetationIndexes.OrderBy(index => index).ToList();
                region.SetCells(cells, vegetation, buffer.Plants, map.Size.x);
                regions[region.RegionId] = region;
                foreach (int index in cells)
                    if ((uint)index < (uint)regionIdByCell.Length) regionIdByCell[index] = region.RegionId;
            }
        }

        private static int CountOverlap(HashSet<int> currentCells, IReadOnlyList<int> previousCells)
        {
            if (currentCells == null || previousCells == null || currentCells.Count == 0 || previousCells.Count == 0) return 0;
            int count = 0;
            if (currentCells.Count <= previousCells.Count)
            {
                foreach (int index in currentCells) if (previousCells.Contains(index)) count++;
            }
            else
            {
                var previousSet = new HashSet<int>(previousCells);
                foreach (int index in currentCells) if (previousSet.Contains(index)) count++;
            }
            return count;
        }

        private void RefreshRegionMetrics(int tick)
        {
            foreach (ForestEcologyRegion region in regions.Values)
            {
                int previousTrees = region.TreeCount;
                int trees = 0;
                int ancient = 0;
                int matureAncient = 0;
                var seen = new HashSet<int>();
                foreach (Plant plant in region.Plants)
                {
                    if (!ForestConnectivityUtility.IsTree(plant) || plant.Map != map || !seen.Add(plant.thingIDNumber)) continue;
                    trees++;
                    if (ForestConnectivityUtility.IsAncientTree(plant))
                    {
                        ancient++;
                        if (plant.Growth >= 0.8f) matureAncient++;
                    }
                }

                region.TreeCount = trees;
                region.AncientTreeCount = ancient;
                region.MatureAncientTreeCount = matureAncient;
                region.TreeDensity = region.CellCount > 0 ? Math.Min(1f, trees / (float)region.CellCount) : 0f;
                region.MatureAncientTreeRatio = ancient > 0 ? matureAncient / (float)ancient : 0f;
                region.AncientSoilAreaCells = 0;
                MapComponent_AncientForestSoil soil = map.GetComponent<MapComponent_AncientForestSoil>();
                foreach (int cellIndex in region.CellIndexes)
                    if (soil?.IsAncientForestSoil(new IntVec3(cellIndex % map.Size.x, 0, cellIndex / map.Size.x)) == true)
                        region.AncientSoilAreaCells++;

                float densityScore = Mathf.Clamp01(region.TreeDensity / 0.65f) * 100f;
                float matureScore = Mathf.Clamp01(region.MatureAncientTreeRatio) * 100f;
                float areaScore = Mathf.Clamp01(region.CellCount / 2000f) * 100f;
                CalculateDisturbance(region, out int nearbyBuildings, out int coveredCells);
                float floorInterference = region.CellCount > 0 ? Mathf.Clamp01(coveredCells / (float)region.CellCount) : 0f;
                float disturbanceScore = Mathf.Clamp(100f - floorInterference * 120f - nearbyBuildings * 2.5f, 0f, 100f);
                region.EcologicalIntegrity = Mathf.Clamp(densityScore * 0.30f + matureScore * 0.20f
                    + areaScore * 0.25f + disturbanceScore * 0.25f, 0f, 100f);

                float densityFactor = Mathf.Clamp01(region.TreeDensity / 0.45f);
                float integrityFactor = region.EcologicalIntegrity / 100f;
                region.HabitatCapacity = Math.Max(0f, region.CellCount / 500f * densityFactor
                    * integrityFactor * ForestConnectivityUtility.BiomeFactor(map));
                region.HabitatCapacityScore = Mathf.Clamp(region.HabitatCapacity / 4f * 100f, 0f, 100f);
                region.HabitatCapacityTier = region.HabitatCapacity < 0.5f ? 0
                    : region.HabitatCapacity < 1.25f ? 1
                    : region.HabitatCapacity < 2.25f ? 2
                    : region.HabitatCapacity < 3.25f ? 3 : 4;

                UpdateStatus(region, previousTrees, tick);
                region.LastUpdatedTick = tick;
            }
        }

        private void CalculateDisturbance(ForestEcologyRegion region, out int nearbyBuildings, out int coveredCells)
        {
            nearbyBuildings = 0;
            coveredCells = 0;
            if (region.CellCount <= 0) return;
            var neighborhood = new HashSet<int>();
            foreach (int index in region.CellIndexes)
            {
                int x = index % map.Size.x;
                int z = index / map.Size.x;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int nz = z + dz;
                    if ((uint)nx < (uint)map.Size.x && (uint)nz < (uint)map.Size.z)
                        neighborhood.Add(nz * map.Size.x + nx);
                }
            }
            foreach (int index in neighborhood)
            {
                IntVec3 cell = new IntVec3(index % map.Size.x, 0, index / map.Size.x);
                if (ForestSoilErosionUtility.IsConstructedFloor(map, cell, out _)) coveredCells++;
                if (cell.GetEdifice(map) != null) nearbyBuildings++;
            }
            coveredCells = Math.Min(coveredCells, region.CellCount);
            nearbyBuildings = Math.Max(0, nearbyBuildings - coveredCells);
        }

        private static void UpdateStatus(ForestEcologyRegion region, int oldTreeCount, int tick)
        {
            ForestEcologyStatus desired;
            if (region.CellCount < 100 || region.TreeDensity < 0.10f) desired = ForestEcologyStatus.Sparse;
            else if (region.EcologicalIntegrity <= 35f) desired = ForestEcologyStatus.Degraded;
            else if (region.EcologicalIntegrity >= 75f && region.MatureAncientTreeCount >= 5)
                desired = ForestEcologyStatus.Thriving;
            else if (region.Trend == ForestEcologyTrend.Recovering || oldTreeCount < region.TreeCount)
                desired = ForestEcologyStatus.Recovering;
            else desired = ForestEcologyStatus.Stable;

            if (region.LastStatusChangeTick <= 0)
            {
                region.Status = desired;
                region.LastStatusChangeTick = tick;
                return;
            }
            if (desired == region.Status) return;
            int lostTrees = Math.Max(0, oldTreeCount - region.TreeCount);
            bool severeDecline = oldTreeCount > 0 && lostTrees >= Math.Max(3, oldTreeCount / 4);
            if (severeDecline || tick - region.LastStatusChangeTick >= MinimumStatusDuration)
            {
                region.Status = desired;
                region.LastStatusChangeTick = tick;
            }
        }

        private void RefreshPlayerKnowledge(int tick)
        {
            knownRegions.Clear();
            foreach (ForestEcologyRegion region in orderedRegions)
            {
                var known = new List<int>();
                foreach (int cellIndex in region.CellIndexes)
                {
                    IntVec3 cell = new IntVec3(cellIndex % map.Size.x, 0, cellIndex / map.Size.x);
                    if (HistoricalLocalFogOfWarManager.IsCellExplored(map, cell)) known.Add(cellIndex);
                }
                region.SetKnownCells(known);
                if (known.Count > 0) knownRegions.Add(region);
            }
            lastKnowledgeRefreshTick = tick;
            lastHighlightRefreshTick = -1;
        }

        private void RebuildHighlightCache(int tick)
        {
            highlightBatches.Clear();
            highlightColors.Clear();
            if (showAllKnownRegions)
            {
                int colorIndex = 0;
                foreach (ForestEcologyRegion region in knownRegions)
                {
                    highlightBatches.Add(ToCells(region.KnownCellIndexes));
                    highlightColors.Add(RegionColor(colorIndex++));
                }
            }
            else if (selectedRegionId > 0 && regions.TryGetValue(selectedRegionId, out ForestEcologyRegion selected))
            {
                highlightBatches.Add(ToCells(selected.KnownCellIndexes));
                highlightColors.Add(new Color(0.25f, 0.95f, 0.55f, 0.72f));
            }
            lastHighlightRefreshTick = tick;
        }

        private List<IntVec3> ToCells(IReadOnlyList<int> indexes)
        {
            var result = new List<IntVec3>(indexes?.Count ?? 0);
            if (indexes == null) return result;
            foreach (int index in indexes)
                if ((uint)index < (uint)regionIdByCell.Length)
                    result.Add(new IntVec3(index % map.Size.x, 0, index / map.Size.x));
            return result;
        }

        private static Color RegionColor(int index)
        {
            Color[] colors =
            {
                new Color(0.30f, 0.85f, 0.45f, 0.62f), new Color(0.35f, 0.65f, 1f, 0.62f),
                new Color(1f, 0.72f, 0.28f, 0.62f), new Color(0.82f, 0.42f, 0.90f, 0.62f),
                new Color(0.95f, 0.42f, 0.36f, 0.62f), new Color(0.28f, 0.82f, 0.82f, 0.62f)
            };
            return colors[index % colors.Length];
        }

        private void RecordDailySnapshots(int tick)
        {
            foreach (ForestEcologyRegion region in regions.Values) region.RecordSnapshot(tick);
            lastSnapshotTick = tick;
        }

        private void RefreshOrdering()
        {
            orderedRegions.Clear();
            orderedRegions.AddRange(regions.Values.OrderBy(region => region.RegionId));
            if (selectedRegionId > 0 && !regions.ContainsKey(selectedRegionId)) selectedRegionId = 0;
            lastKnowledgeRefreshTick = -1;
            lastHighlightRefreshTick = -1;
        }

        private void AllocateRegionIndex()
        {
            int count = map == null ? 0 : map.Size.x * map.Size.z;
            if (regionIdByCell == null || regionIdByCell.Length != count) regionIdByCell = new int[count];
        }

        private int IndexOf(IntVec3 cell)
        {
            return map != null && cell.InBounds(map) ? cell.z * map.Size.x + cell.x : -1;
        }

        private sealed class ForestNode
        {
            public readonly int CellIndex;
            public readonly Plant Plant;
            public ForestNode(int index, Plant plant) { CellIndex = index; Plant = plant; }
        }

        private sealed class RegionBuildBuffer
        {
            public readonly HashSet<int> CellIndexes = new HashSet<int>();
            public readonly List<int> VegetationIndexes = new List<int>();
            public readonly List<Plant> Plants = new List<Plant>();
            public int MinimumCellIndex => VegetationIndexes.Count == 0 ? int.MaxValue : VegetationIndexes.Min();
        }

        private sealed class UnionFind
        {
            private readonly int[] parents;
            private readonly byte[] ranks;
            public UnionFind(int count)
            {
                parents = new int[count];
                ranks = new byte[count];
                for (int i = 0; i < count; i++) parents[i] = i;
            }
            public int Find(int value)
            {
                if (parents[value] != value) parents[value] = Find(parents[value]);
                return parents[value];
            }
            public void Union(int first, int second)
            {
                int a = Find(first);
                int b = Find(second);
                if (a == b) return;
                if (ranks[a] < ranks[b]) parents[a] = b;
                else if (ranks[a] > ranks[b]) parents[b] = a;
                else { parents[b] = a; ranks[a]++; }
            }
        }
    }
}
