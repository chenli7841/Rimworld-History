using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Forest;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Scenarios.EasternZhou;
using HistoricalRimWorld.World;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.WorldGeneration
{
    /// <summary>Runs only on a newly generated map whose world tile contains a configured historical landmark.</summary>
    public sealed class GenStep_HistoricalLandmarkMap : GenStep
    {
        public override int SeedPart => 912731;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null || !HistoricalLandmarkMapGenerator.TryResolve(map, out HistoricalLandmarkDef primary,
                out List<HistoricalLandmarkDef> colocated)) return;
            HistoricalLandmarkMapGenerator.Generate(map, primary, colocated);
        }
    }

    public static class HistoricalLandmarkMapGenerator
    {
        private static readonly string[] RockFallbacks = { "Granite", "Limestone", "Sandstone", "Slate", "Marble" };

        internal static bool TryResolve(Map map, out HistoricalLandmarkDef primary,
            out List<HistoricalLandmarkDef> colocated)
        {
            primary = null;
            colocated = new List<HistoricalLandmarkDef>();
            if (map?.Tile.Valid != true || Find.World?.worldObjects == null) return false;
            IEnumerable<WorldObject> markers = Find.World.worldObjects.AllWorldObjects
                .Where(item => item is IHistoricalMapLocation)
                .Where(item => item != null && item.Tile.tileId == map.Tile.tileId
                    && !string.IsNullOrWhiteSpace(((IHistoricalMapLocation)item).HistoricalLandmarkDefName));
            foreach (WorldObject worldObject in markers)
            {
                IHistoricalMapLocation marker = (IHistoricalMapLocation)worldObject;
                HistoricalLandmarkDef landmark = DefDatabase<HistoricalLandmarkDef>.GetNamedSilentFail(marker.HistoricalLandmarkDefName);
                if (landmark == null || landmark.scenario == null || !IsSelectedScenario(landmark.scenario)) continue;
                if (!colocated.Contains(landmark)) colocated.Add(landmark);
            }
            colocated = colocated.OrderBy(item => item.defName, StringComparer.Ordinal).ToList();
            IHistoricalMapLocation selectedParent = map.Parent as IHistoricalMapLocation;
            if (!string.IsNullOrWhiteSpace(selectedParent?.HistoricalLandmarkDefName))
                primary = colocated.FirstOrDefault(item => item.defName == selectedParent.HistoricalLandmarkDefName);
            primary = primary ?? colocated.FirstOrDefault();
            return primary?.mapTemplate != null;
        }

        private static bool IsSelectedScenario(HistoricalScenarioDef scenario)
        {
            if (HistoricalScenarioManager.IsScenarioActive)
                return HistoricalScenarioManager.ActiveScenarioDefName == scenario.defName;
            return HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)
                && setup?.scenario?.defName == scenario.defName;
        }

        internal static void Generate(Map map, HistoricalLandmarkDef landmark,
            List<HistoricalLandmarkDef> colocated)
        {
            HistoricalMapSceneComponent state = map.GetComponent<HistoricalMapSceneComponent>();
            if (state == null)
            {
                Log.Error("[Historical RimWorld] Landmark map generation skipped: scene MapComponent is unavailable.");
                return;
            }
            if (state.GenerationApplied) return;
            HistoricalMapTemplateDef template = landmark?.mapTemplate;
            if (template == null)
            {
                Log.Warning("[Historical RimWorld] Landmark map has no template; leaving RimWorld's generated map unchanged.");
                return;
            }

            int seed = map.ConstantRandSeed ^ (map.Tile.tileId * 7919) ^ StableHash(template.defName);
            if (Math.Min(map.Size.x, map.Size.z) < template.minimumMapDimension)
            {
                state.Initialize(landmark.scenario, template, colocated, seed);
                Log.Warning("[Historical RimWorld] Map for " + landmark.landmarkId + " is smaller than the template minimum; used the safe metadata-only fallback.");
                return;
            }

            TerrainDef road = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel")
                ?? DefDatabase<TerrainDef>.GetNamedSilentFail("Soil");
            if (template.terrainProfile == "LishanBattlefield")
                GenerateLishanBattlefield(map, template, road, seed);
            else if (template.terrainProfile == "HaojingCapital")
                GenerateHaojingCapital(map, road);
            else
                Log.Warning("[Historical RimWorld] Unknown terrain profile '" + template.terrainProfile + "'; applying scene metadata only.");

            state.Initialize(landmark.scenario, template, colocated, seed);
            Log.Message("[Historical RimWorld] Generated historical landmark map profile " + template.defName
                + " at tile " + map.Tile.tileId + " (seed " + seed + "). Applied metadata for "
                + string.Join(", ", colocated.Select(item => item.landmarkId)) + ".");
        }

        private static void GenerateLishanBattlefield(Map map, HistoricalMapTemplateDef template,
            TerrainDef roadTerrain, int seed)
        {
            int width = map.Size.x, height = map.Size.z;
            int centerX = width / 2;
            // A maintained north-to-south foothill route and two branch gullies provide continuous approaches.
            DrawPath(map, roadTerrain, centerX, 0, centerX, height - 1, 3);
            DrawPath(map, roadTerrain, 0, (int)(height * 0.43f), centerX, (int)(height * 0.48f), 2);
            DrawPath(map, roadTerrain, centerX, (int)(height * 0.48f), width - 1, (int)(height * 0.56f), 2);

            // Irregular southern foothill ridges leave three passes; only fresh natural cells are eligible.
            ThingDef rockDef = ResolveNaturalRock(template.mountainRockDefName);
            if (rockDef == null)
            {
                Log.Warning("[Historical RimWorld] No natural rock ThingDef was found for the Lishan ridge; routes and scene zones were still generated.");
                return;
            }
            Random random = new Random(seed);
            int ridgeBase = (int)(height * 0.70f);
            for (int x = 0; x < width; x++)
            {
                double wave = Math.Sin(x * 0.075d) * height * 0.035d + Math.Sin(x * 0.19d) * height * 0.012d;
                int ridgeZ = Math.Max((int)(height * 0.62f), Math.Min(height - 4,
                    ridgeBase + (int)Math.Round(wave) + random.Next(-1, 2)));
                bool pass = Math.Abs(x - centerX) < Math.Max(5, width / 16)
                    || Math.Abs(x - (int)(width * 0.19f)) < Math.Max(3, width / 30)
                    || Math.Abs(x - (int)(width * 0.81f)) < Math.Max(3, width / 30);
                if (pass) continue;
                int ridgeDepth = Math.Max(3, height / 18);
                for (int z = ridgeZ; z < Math.Min(height - 1, ridgeZ + ridgeDepth); z++)
                {
                    if (NearPath(x, z, centerX, 0, centerX, height - 1, 4)
                        || map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(new IntVec3(x, 0, z)) == true)
                        continue;
                    if (random.NextDouble() > 0.72d || !map.thingGrid.ThingsListAtFast(new IntVec3(x, 0, z)).NullOrEmpty()) continue;
                    Thing rock = ThingMaker.MakeThing(rockDef);
                    GenSpawn.Spawn(rock, new IntVec3(x, 0, z), map, WipeMode.Vanish);
                }
            }
        }

        private static void GenerateHaojingCapital(Map map, TerrainDef roadTerrain)
        {
            int width = map.Size.x, height = map.Size.z;
            // A representative settlement grid: central public space, two broad roads and a loop street.
            DrawPath(map, roadTerrain, width / 2, 0, width / 2, height - 1, 2);
            DrawPath(map, roadTerrain, 0, height / 2, width - 1, height / 2, 2);
            DrawPath(map, roadTerrain, (int)(width * 0.18f), (int)(height * 0.22f),
                (int)(width * 0.82f), (int)(height * 0.78f), 1);
            DrawPath(map, roadTerrain, (int)(width * 0.82f), (int)(height * 0.22f),
                (int)(width * 0.18f), (int)(height * 0.78f), 1);
        }

        private static void DrawPath(Map map, TerrainDef terrain, int x0, int z0, int x1, int z1, int halfWidth)
        {
            if (terrain == null) return;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dz = -Math.Abs(z1 - z0), sz = z0 < z1 ? 1 : -1;
            int error = dx + dz;
            while (true)
            {
                for (int offset = -halfWidth; offset <= halfWidth; offset++)
                {
                    int x = Math.Abs(x1 - x0) >= Math.Abs(z1 - z0) ? x0 : x0 + offset;
                    int z = Math.Abs(x1 - x0) >= Math.Abs(z1 - z0) ? z0 + offset : z0;
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map) || cell.GetEdifice(map) != null || terrain.IsWater) continue;
                    if (map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(cell) == true) continue;
                    TerrainDef current = map.terrainGrid.TerrainAt(cell);
                    if (current != null && !current.IsWater && current.natural) map.terrainGrid.SetTerrain(cell, terrain);
                }
                if (x0 == x1 && z0 == z1) break;
                int twiceError = 2 * error;
                if (twiceError >= dz) { error += dz; x0 += sx; }
                if (twiceError <= dx) { error += dx; z0 += sz; }
            }
        }

        private static bool NearPath(int x, int z, int x0, int z0, int x1, int z1, int radius)
        {
            if (x0 == x1) return Math.Abs(x - x0) <= radius;
            // The main Lishan route is vertical; this branch intentionally checks only that guaranteed corridor.
            return false;
        }

        private static ThingDef ResolveNaturalRock(string requested)
        {
            ThingDef candidate = DefDatabase<ThingDef>.GetNamedSilentFail(requested ?? string.Empty);
            if (candidate?.building != null && candidate.building.isNaturalRock) return candidate;
            foreach (string name in RockFallbacks)
            {
                candidate = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (candidate?.building != null && candidate.building.isNaturalRock) return candidate;
            }
            return null;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value ?? string.Empty) hash = hash * 31 + character;
                return hash;
            }
        }
    }
}
