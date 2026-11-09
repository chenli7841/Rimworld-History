using System;
using System.Collections.Generic;
using RimWorld;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Characters
{
    /// <summary>Low-frequency, shared ecology snapshot used by nature-reverent pawns.</summary>
    public sealed class HistoricalNatureEcologyMapComponent : MapComponent
    {
        private const int RefreshIntervalTicks = 2500;
        private bool hasSnapshot;
        private int lastRefreshTick = -1;
        private int maxForestArea;
        private float treeCoveragePercent;
        private int wildlifeCount;
        private int wildlifeSpeciesCount;
        private bool hasApexPredator;
        private int moodBonus;

        public HistoricalNatureEcologyMapComponent(Map map) : base(map) { }

        public bool HasSnapshot => hasSnapshot;
        public int MaxForestArea => maxForestArea;
        public float TreeCoveragePercent => treeCoveragePercent;
        public int WildlifeCount => wildlifeCount;
        public int WildlifeSpeciesCount => wildlifeSpeciesCount;
        public bool HasApexPredator => hasApexPredator;
        public int MoodBonus => moodBonus;

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive || Find.TickManager == null) return;

            int tick = Find.TickManager.TicksGame;
            if (lastRefreshTick >= 0 && tick - lastRefreshTick < RefreshIntervalTicks) return;
            lastRefreshTick = tick;
            RefreshSnapshot();
        }

        private void RefreshSnapshot()
        {
            int width = map?.Size.x ?? 0;
            int height = map?.Size.z ?? 0;
            int cellCount = width * height;
            if (cellCount <= 0)
            {
                SetSnapshot(0, 0f, 0, 0, false);
                return;
            }

            bool[] eligibleSurface = new bool[cellCount];
            bool[] treeCells = new bool[cellCount];
            int eligibleCellCount = 0;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                    if (terrain == null || terrain.IsWater || terrain.affordances == null
                        || !terrain.affordances.Contains(TerrainAffordanceDefOf.Light))
                        continue;

                    eligibleSurface[z * width + x] = true;
                    eligibleCellCount++;
                }
            }

            List<Thing> plants = map.listerThings?.ThingsInGroup(ThingRequestGroup.Plant);
            if (plants != null)
            {
                for (int i = 0; i < plants.Count; i++)
                {
                    Thing thing = plants[i];
                    Plant plant = thing as Plant;
                    if (plant == null || thing.Destroyed || !thing.Spawned || thing.def?.plant?.IsTree != true)
                        continue;

                    CellRect occupied = thing.OccupiedRect();
                    for (int z = occupied.minZ; z <= occupied.maxZ; z++)
                    {
                        for (int x = occupied.minX; x <= occupied.maxX; x++)
                        {
                            if ((uint)x >= (uint)width || (uint)z >= (uint)height) continue;
                            int index = z * width + x;
                            if (eligibleSurface[index]) treeCells[index] = true;
                        }
                    }
                }
            }

            int coveredCells = 0;
            int largestForest = FindLargestConnectedTreeArea(treeCells, width, height, out coveredCells);
            float coverage = eligibleCellCount > 0 ? coveredCells * 100f / eligibleCellCount : 0f;

            int animals = 0;
            HashSet<ThingDef> species = new HashSet<ThingDef>();
            bool apexPredator = false;
            HashSet<Pawn> temporaryPawns = new HashSet<Pawn>(PawnsFinder.Temporary ?? new List<Pawn>());
            IReadOnlyList<Pawn> mapPawns = map.mapPawns?.AllPawnsSpawned;
            if (mapPawns != null)
            {
                for (int i = 0; i < mapPawns.Count; i++)
                {
                    Pawn pawn = mapPawns[i];
                    if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned || !pawn.IsAnimal
                        || pawn.Faction != null || pawn.InContainerEnclosed || temporaryPawns.Contains(pawn))
                        continue;

                    animals++;
                    if (pawn.def != null) species.Add(pawn.def);
                    if (pawn.def?.GetModExtension<HistoricalNatureApexPredatorExtension>() != null)
                        apexPredator = true;
                }
            }

            SetSnapshot(largestForest, coverage, animals, species.Count, apexPredator);
        }

        private void SetSnapshot(int forestArea, float coverage, int animals, int species, bool apexPredator)
        {
            maxForestArea = Math.Max(0, forestArea);
            treeCoveragePercent = Math.Max(0f, Math.Min(100f, coverage));
            wildlifeCount = Math.Max(0, animals);
            wildlifeSpeciesCount = Math.Max(0, species);
            hasApexPredator = apexPredator;

            int forestBonus = maxForestArea >= 2000 ? 4
                : maxForestArea >= 1000 ? 3
                : maxForestArea >= 400 ? 2
                : maxForestArea >= 100 ? 1 : 0;
            int treeBonus = treeCoveragePercent >= 30f ? 2 : treeCoveragePercent >= 10f ? 1 : 0;
            int wildlifeBonus = wildlifeCount >= 8 && wildlifeSpeciesCount >= 3 ? 2
                : wildlifeCount >= 3 && wildlifeSpeciesCount >= 2 ? 1 : 0;

            moodBonus = Math.Min(9, forestBonus + treeBonus + wildlifeBonus + (hasApexPredator ? 1 : 0));
            hasSnapshot = true;
        }

        private static int FindLargestConnectedTreeArea(bool[] treeCells, int width, int height, out int totalCovered)
        {
            totalCovered = 0;
            bool[] visited = new bool[treeCells.Length];
            int[] queue = new int[treeCells.Length];
            int largest = 0;

            for (int start = 0; start < treeCells.Length; start++)
            {
                if (!treeCells[start] || visited[start]) continue;
                int head = 0;
                int tail = 0;
                int area = 0;
                visited[start] = true;
                queue[tail++] = start;

                while (head < tail)
                {
                    int index = queue[head++];
                    area++;
                    int x = index % width;
                    int z = index / width;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = x + dx;
                            int nz = z + dz;
                            if ((uint)nx >= (uint)width || (uint)nz >= (uint)height) continue;
                            int neighbor = nz * width + nx;
                            if (!treeCells[neighbor] || visited[neighbor]) continue;
                            visited[neighbor] = true;
                            queue[tail++] = neighbor;
                        }
                    }
                }

                totalCovered += area;
                if (area > largest) largest = area;
            }

            return largest;
        }
    }

    /// <summary>Marker extension for animal ThingDefs counted as apex predators.</summary>
    public sealed class HistoricalNatureApexPredatorExtension : DefModExtension { }

    public sealed class ThoughtWorker_NatureReverenceMood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || pawn?.story?.traits?.allTraits == null
                || !pawn.Spawned || pawn.Map == null)
                return ThoughtState.Inactive;

            bool hasTrait = false;
            for (int i = 0; i < pawn.story.traits.allTraits.Count; i++)
            {
                if (pawn.story.traits.allTraits[i]?.def?.defName == "HR_ReverenceForNature")
                {
                    hasTrait = true;
                    break;
                }
            }
            if (!hasTrait) return ThoughtState.Inactive;

            HistoricalNatureEcologyMapComponent ecology = pawn.Map.GetComponent<HistoricalNatureEcologyMapComponent>();
            if (ecology == null || !ecology.HasSnapshot || ecology.MoodBonus <= 0)
                return ThoughtState.Inactive;

            return ThoughtState.ActiveAtStage(Math.Min(8, ecology.MoodBonus - 1));
        }
    }
}
