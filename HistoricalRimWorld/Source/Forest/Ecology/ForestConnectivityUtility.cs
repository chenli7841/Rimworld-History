using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    public sealed class ForestConnectivityPath
    {
        public int TargetNodeIndex;
        public List<int> CellIndexes;
    }

    /// <summary>One source of truth for forest cells, ecological links, and artificial barriers.</summary>
    public static class ForestConnectivityUtility
    {
        public const int ForestConnectivityRadius = 3;
        public const int MaximumPathSteps = ForestConnectivityRadius + 1;

        private static readonly int[] OffsetX = { 0, 0, -1, 1 };
        private static readonly int[] OffsetZ = { -1, 1, 0, 0 };

        public static bool IsTree(Plant plant)
        {
            return plant != null && !plant.Destroyed && plant.Spawned
                && plant.def?.plant?.IsTree == true && plant.Growth > 0f;
        }

        public static bool IsAncientTree(Plant plant)
        {
            return IsTree(plant) && plant.def.GetModExtension<HR_AncientTreeExtension>() != null;
        }

        public static bool IsMatureAncientTree(Plant plant)
        {
            return IsAncientTree(plant) && plant.Growth >= 0.8f;
        }

        public static bool IsWildUnderstoryPlant(Plant plant, Map map)
        {
            if (plant == null || map == null || plant.Destroyed || !plant.Spawned || plant.Growth < 0.15f
                || plant.def?.plant == null || plant.def.plant.IsTree) return false;
            PlantProperties properties = plant.def.plant;
            return properties.wildClusterWeight > 0f
                && (properties.sowTags == null || properties.sowTags.Count == 0)
                && map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(plant.Position) == true
                && IsNaturalForestCell(plant.Position, map);
        }

        public static bool IsForestVegetationCell(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map) || !IsNaturalForestCell(cell, map)) return false;
            Plant plant = cell.GetPlant(map);
            return IsTree(plant) || IsWildUnderstoryPlant(plant, map);
        }

        /// <summary>
        /// Natural cells may carry a short ecological connection but are not counted as forest area
        /// unless they contain a tree or suitable wild understory. Floors, roads, water and edifices
        /// stop the connection search, regardless of tree radius.
        /// </summary>
        public static bool IsNaturalForestCell(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map)) return false;
            TerrainDef terrain = map.terrainGrid.TopTerrainAt(cell);
            if (terrain == null || !terrain.natural || terrain.IsWater
                || ForestSoilErosionUtility.IsConstructedFloor(map, cell, out _)) return false;
            if (terrain.tags != null && (terrain.tags.Contains("Floor") || terrain.tags.Contains("Road"))) return false;
            // All edifice objects form a hard barrier. This includes walls and natural rock;
            // wildlife and plants are not edifices and remain traversable nodes.
            return cell.GetEdifice(map) == null;
        }

        public static List<ForestConnectivityPath> FindLinksFromNode(Map map, int sourceCellIndex,
            int sourceNodeIndex, int width, int height, Dictionary<int, int> nodeIndexByCell)
        {
            return FindLinks(sourceCellIndex, sourceNodeIndex, width, height, nodeIndexByCell,
                index => IsNaturalForestCell(new IntVec3(index % width, 0, index / width), map));
        }

        public static bool HasNaturalTreeWithinConnectivityPath(Map map, int sourceCellIndex,
            HashSet<int> treeCellIndexes)
        {
            if (map == null || treeCellIndexes == null || treeCellIndexes.Count == 0) return false;
            int width = map.Size.x;
            int height = map.Size.z;
            int[] queue = new int[MaximumSearchCells];
            int[] distances = new int[MaximumSearchCells];
            var visited = new HashSet<int>();
            int head = 0;
            int tail = 0;
            queue[tail++] = sourceCellIndex;
            visited.Add(sourceCellIndex);
            while (head < tail)
            {
                int current = queue[head];
                int distance = distances[head++];
                if (distance >= MaximumPathSteps) continue;
                int x = current % width;
                int z = current / width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + OffsetX[direction];
                    int nz = z + OffsetZ[direction];
                    if ((uint)nx >= (uint)width || (uint)nz >= (uint)height) continue;
                    int next = nz * width + nx;
                    if (visited.Contains(next) || !IsNaturalForestCell(new IntVec3(nx, 0, nz), map)) continue;
                    if (treeCellIndexes.Contains(next) && !OnlyDiagonalContact(sourceCellIndex, next, width)) return true;
                    visited.Add(next);
                    if (distance + 1 < MaximumPathSteps && tail < queue.Length)
                    {
                        queue[tail] = next;
                        distances[tail++] = distance + 1;
                    }
                }
            }
            return false;
        }

        private static List<ForestConnectivityPath> FindLinks(int sourceCellIndex, int sourceNodeIndex,
            int width, int height, Dictionary<int, int> nodeIndexByCell, Func<int, bool> canTraverse)
        {
            var links = new List<ForestConnectivityPath>();
            if (canTraverse == null || nodeIndexByCell == null || width <= 0 || height <= 0) return links;
            int[] queue = new int[MaximumSearchCells];
            int[] queueDistance = new int[MaximumSearchCells];
            int head = 0;
            int tail = 0;
            var visited = new Dictionary<int, int>(MaximumSearchCells);
            visited[sourceCellIndex] = -1;
            queueDistance[0] = 0;
            queue[tail++] = sourceCellIndex;

            while (head < tail)
            {
                int current = queue[head];
                int currentDistance = queueDistance[head++];
                if (currentDistance >= MaximumPathSteps) continue;
                int x = current % width;
                int z = current / width;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + OffsetX[d];
                    int nz = z + OffsetZ[d];
                    if ((uint)nx >= (uint)width || (uint)nz >= (uint)height) continue;
                    int next = nz * width + nx;
                    if (visited.ContainsKey(next)) continue;
                    if (!canTraverse(next)) continue;
                    visited[next] = current;
                    int nextDistance = currentDistance + 1;
                    if (nodeIndexByCell.TryGetValue(next, out int targetNodeIndex)
                        && targetNodeIndex > sourceNodeIndex && !OnlyDiagonalContact(sourceCellIndex, next, width))
                    {
                        var path = new List<int>();
                        int pathCell = next;
                        while (pathCell >= 0)
                        {
                            path.Add(pathCell);
                            pathCell = visited[pathCell];
                        }
                        path.Reverse();
                        links.Add(new ForestConnectivityPath { TargetNodeIndex = targetNodeIndex, CellIndexes = path });
                    }
                    if (nextDistance < MaximumPathSteps && tail < queue.Length)
                    {
                        queueDistance[tail] = nextDistance;
                        queue[tail++] = next;
                    }
                }
            }
            return links;
        }

        public static bool RunCompatibilitySelfTest(out string report)
        {
            bool threeCellGap = FixtureConnects(new IntVec3(1, 0, 4), new IntVec3(5, 0, 4), 9, 9,
                new HashSet<int>());

            var twoCellRoad = new HashSet<int>();
            for (int z = 0; z < 9; z++)
            {
                twoCellRoad.Add(z * 9 + 3);
                twoCellRoad.Add(z * 9 + 4);
            }
            bool roadBarrierSplits = !FixtureConnects(new IntVec3(1, 0, 4), new IntVec3(5, 0, 4), 9, 9, twoCellRoad);

            var singleRoadCell = new HashSet<int> { 4 * 9 + 3 };
            bool singleCellCanBeBypassed = FixtureConnects(new IntVec3(2, 0, 4), new IntVec3(4, 0, 4), 9, 9, singleRoadCell);

            bool diagonalOnlyStaysSeparate = !FixtureConnects(new IntVec3(3, 0, 3), new IntVec3(4, 0, 4), 9, 9,
                new HashSet<int>());

            bool passed = threeCellGap && roadBarrierSplits && singleCellCanBeBypassed && diagonalOnlyStaysSeparate;
            report = "3-cell natural gap=" + threeCellGap + "; 2-cell continuous road barrier splits=" + roadBarrierSplits
                + "; isolated road tile can be bypassed=" + singleCellCanBeBypassed
                + "; diagonal-only contact stays separate=" + diagonalOnlyStaysSeparate;
            return passed;
        }

        private static bool FixtureConnects(IntVec3 first, IntVec3 second, int width, int height, HashSet<int> barriers)
        {
            int firstIndex = first.z * width + first.x;
            int secondIndex = second.z * width + second.x;
            var nodes = new Dictionary<int, int> { [firstIndex] = 0, [secondIndex] = 1 };
            return FindLinks(firstIndex, 0, width, height, nodes, index => !barriers.Contains(index))
                .Exists(path => path.TargetNodeIndex == 1);
        }

        // The BFS frontier cannot contain more cells than the radius-4 Manhattan diamond.
        private const int MaximumSearchCells = 64;

        private static bool OnlyDiagonalContact(int first, int second, int width)
        {
            int dx = Math.Abs(first % width - second % width);
            int dz = Math.Abs(first / width - second / width);
            return dx == 1 && dz == 1;
        }

        public static float GetHabitatSuitability(ForestEcologyRegion region, ThingDef animalDef)
        {
            if (region == null || animalDef?.race?.Animal != true) return 0f;
            return Math.Max(0f, Math.Min(1f, region.HabitatCapacityScore / 100f));
        }

        public static float BiomeFactor(Map map)
        {
            string biome = map?.Biome?.defName ?? string.Empty;
            if (biome.IndexOf("TropicalRainforest", StringComparison.OrdinalIgnoreCase) >= 0) return 1f;
            if (biome.IndexOf("TemperateForest", StringComparison.OrdinalIgnoreCase) >= 0) return 1f;
            if (biome.IndexOf("BorealForest", StringComparison.OrdinalIgnoreCase) >= 0) return 0.9f;
            if (biome.IndexOf("Swamp", StringComparison.OrdinalIgnoreCase) >= 0) return 0.8f;
            if (biome.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0) return 0.8f;
            return 0.55f;
        }
    }
}
