using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Generates irregular edge-biased ancient forest only on historical player maps.</summary>
    public sealed class GenStep_AncientForest : GenStep
    {
        public float targetForestCoverage = 0.30f;
        public int edgeForestPreferredWidth = 35;
        public int edgeForestMaxWidth = 70;
        public float ancientTreeDensity = 0.08f;
        public int centralClearRadius = 34;
        public int clearCorridorWidth = 12;
        public string forestSoilDefName = "HR_AncientLivingForestSoil";

        public override int SeedPart => 734119;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || map == null) return;
            GenerateForMap(map, false);
        }

        public void GenerateForMap(Map map, bool debugGenerated)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || map == null) return;
            MapComponent_AncientForestSoil soil = map.GetComponent<MapComponent_AncientForestSoil>();
            TerrainDef soilDef = DefDatabase<TerrainDef>.GetNamedSilentFail(forestSoilDefName);
            if (soil == null || soilDef == null)
            {
                Log.Error("[Historical RimWorld] Ancient forest generation skipped: soil component or TerrainDef is missing.");
                return;
            }
            if (soil.GenerationApplied)
            {
                if (debugGenerated) Log.Message("[Historical RimWorld] Ancient forest generation already ran on this map.");
                return;
            }

            int width = map.Size.x;
            int height = map.Size.z;
            List<CellCandidate> eligible = new List<CellCandidate>();
            List<CellCandidate> edgeCandidates = new List<CellCandidate>();
            int tileId = map.Tile.tileId;
            int seed = map.ConstantRandSeed ^ (tileId * 7919);
            Random random = new Random(seed);
            float[,] noise = MakeNoise(Math.Max(5, width / 18), Math.Max(5, height / 18), random);
            IntVec3 start = MapGenerator.PlayerStartSpotValid ? MapGenerator.PlayerStartSpot : new IntVec3(width / 2, 0, height / 2);
            int edgeMax = Math.Max(1, edgeForestMaxWidth);
            int edgePreferred = Math.Max(1, Math.Min(edgeForestPreferredWidth, edgeMax));
            int centerRadius = Math.Max(0, centralClearRadius);
            int corridorWidth = Math.Max(0, clearCorridorWidth);

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                    if (!IsSuitableBaseTerrain(terrain) || cell.GetEdifice(map) != null) continue;
                    CellCandidate candidate = new CellCandidate(cell, terrain, NoiseAt(noise, x, z, width, height), random.NextDouble());
                    eligible.Add(candidate);

                    int edgeDistance = Math.Min(Math.Min(x, width - 1 - x), Math.Min(z, height - 1 - z));
                    if (edgeDistance > edgeMax) continue;
                    int dx = x - start.x;
                    int dz = z - start.z;
                    if (dx * dx + dz * dz < centerRadius * centerRadius || IsInOpenCorridor(x, z, start, width, height, corridorWidth))
                        continue;
                    candidate.EdgeDistance = edgeDistance;
                    edgeCandidates.Add(candidate);
                }
            }

            GetClimateFactors(map, out float forestCoverageFactor, out float treeDensityFactor);
            int desired = Math.Min(edgeCandidates.Count,
                Math.Max(0, (int)Math.Round(Math.Max(0f, Math.Min(0.8f, targetForestCoverage * forestCoverageFactor)) * eligible.Count)));
            edgeCandidates.Sort((a, b) => b.Score(edgePreferred, edgeMax).CompareTo(a.Score(edgePreferred, edgeMax)));

            List<CellCandidate> selected = edgeCandidates.Take(desired).ToList();
            for (int i = 0; i < selected.Count; i++)
            {
                CellCandidate candidate = selected[i];
                map.terrainGrid.SetTerrain(candidate.Cell, soilDef);
                soil.AddAncientForestSoil(candidate.Cell);
            }

            ThingDef treeDef = ChooseSpeciesForMap(map, random);
            int spawnedTrees = 0;
            if (treeDef != null)
            {
                foreach (CellCandidate candidate in selected)
                {
                    if (random.NextDouble() >= Math.Max(0f, Math.Min(0.5f, ancientTreeDensity * treeDensityFactor))
                        || !soil.CanGrowAncientTree(candidate.Cell)) continue;
                    Thing tree = ThingMaker.MakeThing(treeDef);
                    Plant plant = GenSpawn.Spawn(tree, candidate.Cell, map) as Plant;
                    if (plant == null) continue;
                    plant.Growth = (float)(0.72d + random.NextDouble() * 0.28d);
                    spawnedTrees++;
                    if (debugGenerated) soil.RegisterDebugTree(plant);
                }
            }

            soil.MarkGenerationApplied();
            Log.Message("[Historical RimWorld] Generated ancient forest: active soil=" + selected.Count
                + ", giant trees=" + spawnedTrees + ", biome=" + (map.Biome?.defName ?? "unknown")
                + ", seed=" + seed + (debugGenerated ? " (debug)" : string.Empty) + ".");
        }

        private static bool IsSuitableBaseTerrain(TerrainDef terrain)
        {
            return terrain != null && !terrain.IsWater && terrain.natural && terrain.fertility > 0.05f
                && terrain.affordances != null
                && DefDatabase<TerrainAffordanceDef>.GetNamedSilentFail("GrowSoil") is TerrainAffordanceDef growSoil
                && terrain.affordances.Contains(growSoil)
                && terrain.tags != null && terrain.tags.Contains("Soil");
        }

        private static bool IsInOpenCorridor(int x, int z, IntVec3 start, int width, int height, int corridorWidth)
        {
            if (corridorWidth <= 0) return false;
            int left = x;
            int right = width - 1 - x;
            int top = z;
            int bottom = height - 1 - z;
            int nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
            int targetX = start.x;
            int targetZ = start.z;
            if (nearest == left) targetX = 0;
            else if (nearest == right) targetX = width - 1;
            else if (nearest == top) targetZ = 0;
            else targetZ = height - 1;

            int minX = Math.Min(start.x, targetX);
            int maxX = Math.Max(start.x, targetX);
            int minZ = Math.Min(start.z, targetZ);
            int maxZ = Math.Max(start.z, targetZ);
            if (start.x == targetX) return Math.Abs(x - targetX) <= corridorWidth && z >= minZ && z <= maxZ;
            return Math.Abs(z - targetZ) <= corridorWidth && x >= minX && x <= maxX;
        }

        internal static ThingDef ChooseSpeciesForMap(Map map, Random random)
        {
            string biome = map.Biome?.defName ?? string.Empty;
            HashSet<string> regions = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (HistoricalGeographicRegionData region in HistoricalGeographicRegionManager.GetRegionsForTile(map.Tile.tileId))
                    if (!string.IsNullOrWhiteSpace(region?.defName)) regions.Add(region.defName);
            }
            catch (Exception exception)
            {
                Log.Warning("[Historical RimWorld] Could not resolve ancient forest geography for this tile: " + exception.Message);
            }

            string species;
            if (biome.IndexOf("Tropical", StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("Swamp", StringComparison.OrdinalIgnoreCase) >= 0)
                species = random.NextDouble() < 0.65d ? "HR_AncientBanyan" : "HR_AncientCamphor";
            else if (regions.Contains("HR_Geography_ChuHeartland"))
                species = "HR_AncientCamphor";
            else if (regions.Contains("HR_Geography_Guanzhong") || regions.Contains("HR_Geography_Hedong"))
                species = random.NextDouble() < 0.7d ? "HR_AncientPine" : "HR_AncientOak";
            else if (biome.IndexOf("Boreal", StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("Cold", StringComparison.OrdinalIgnoreCase) >= 0)
                species = "HR_AncientPine";
            else
                species = "HR_AncientOak";

            return DefDatabase<ThingDef>.GetNamedSilentFail(species);
        }

        private static void GetClimateFactors(Map map, out float coverageFactor, out float treeDensityFactor)
        {
            coverageFactor = 1f;
            treeDensityFactor = 1f;
            string biome = map.Biome?.defName ?? string.Empty;
            float temperature = map.TileInfo.temperature;
            float rainfall = map.TileInfo.rainfall;

            if (biome.IndexOf("Desert", StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("Arid", StringComparison.OrdinalIgnoreCase) >= 0 || rainfall < 250f)
            {
                coverageFactor = 0.04f;
                treeDensityFactor = 0.15f;
            }
            if (biome.IndexOf("IceSheet", StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("SeaIce", StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("Tundra", StringComparison.OrdinalIgnoreCase) >= 0 || temperature < -5f)
            {
                coverageFactor = Math.Min(coverageFactor, 0.03f);
                treeDensityFactor = Math.Min(treeDensityFactor, 0.1f);
            }
        }

        private static float[,] MakeNoise(int columns, int rows, Random random)
        {
            float[,] result = new float[columns + 1, rows + 1];
            for (int x = 0; x <= columns; x++)
                for (int z = 0; z <= rows; z++) result[x, z] = (float)random.NextDouble();
            return result;
        }

        private static float NoiseAt(float[,] noise, int x, int z, int width, int height)
        {
            float gx = width <= 1 ? 0f : x * (noise.GetLength(0) - 1f) / (width - 1f);
            float gz = height <= 1 ? 0f : z * (noise.GetLength(1) - 1f) / (height - 1f);
            int x0 = (int)gx;
            int z0 = (int)gz;
            int x1 = Math.Min(noise.GetLength(0) - 1, x0 + 1);
            int z1 = Math.Min(noise.GetLength(1) - 1, z0 + 1);
            float tx = Smooth(gx - x0);
            float tz = Smooth(gz - z0);
            float a = Lerp(noise[x0, z0], noise[x1, z0], tx);
            float b = Lerp(noise[x0, z1], noise[x1, z1], tx);
            return Lerp(a, b, tz);
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private sealed class CellCandidate
        {
            public readonly IntVec3 Cell;
            public readonly TerrainDef BaseTerrain;
            public readonly float Noise;
            public readonly double Detail;
            public int EdgeDistance;

            public CellCandidate(IntVec3 cell, TerrainDef terrain, float noise, double detail)
            { Cell = cell; BaseTerrain = terrain; Noise = noise; Detail = detail; }

            public double Score(int preferredWidth, int maxWidth)
            {
                double edgeBias = 1d - Math.Min(1d, EdgeDistance / (double)Math.Max(1, maxWidth));
                double preferredBias = EdgeDistance <= preferredWidth ? 0.14d : 0d;
                return Noise + Detail * 0.05d + edgeBias * 0.22d + preferredBias;
            }
        }
    }
}
