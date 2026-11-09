using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Map-level, low-frequency dispatcher for mature-tree reproduction and living-soil recovery.</summary>
    public sealed class MapComponent_AncientForestGrowth : MapComponent
    {
        private const int CurrentDataVersion = 1;
        private int dataVersion;
        private int nextReproductionTick = -1;
        private int nextRecoveryTick = -1;
        private int manualCheckSequence;
        private List<IntVec3> overlayLivingCells = new List<IntVec3>();
        private List<IntVec3> overlayNormalCells = new List<IntVec3>();
        private List<IntVec3> overlayBlockedCells = new List<IntVec3>();

        public MapComponent_AncientForestGrowth(Map map) : base(map) { }

        public int MatureTreeCount => GetMatureTrees().Count;

        public void ResetScheduleForTesting()
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            nextReproductionTick = tick + AncientForestGrowthSettings.EffectiveReproductionInterval;
            nextRecoveryTick = tick + AncientForestGrowthSettings.EffectiveRecoveryInterval;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive || Find.TickManager == null) return;
            int tick = Find.TickManager.TicksGame;
            if ((tick + map.ConstantRandSeed) % AncientForestGrowthSettings.TickStaggerInterval != 0) return;

            if (nextReproductionTick < 0) nextReproductionTick = tick + AncientForestGrowthSettings.EffectiveReproductionInterval;
            if (nextRecoveryTick < 0) nextRecoveryTick = tick + AncientForestGrowthSettings.EffectiveRecoveryInterval;
            if (tick >= nextReproductionTick)
            {
                RunReproductionCheck();
                nextReproductionTick = tick + AncientForestGrowthSettings.EffectiveReproductionInterval;
            }
            if (tick >= nextRecoveryTick)
            {
                RunNaturalRecoveryCheck();
                nextRecoveryTick = tick + AncientForestGrowthSettings.EffectiveRecoveryInterval;
            }
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            if (!AncientForestGrowthSettings.ShowReproductionOverlay || !HistoricalScenarioManager.IsScenarioActive) return;
            if (overlayLivingCells.Count > 0) GenDraw.DrawFieldEdges(overlayLivingCells, new Color(0.1f, 0.9f, 0.2f, 0.8f), null, null, 0);
            if (overlayNormalCells.Count > 0) GenDraw.DrawFieldEdges(overlayNormalCells, new Color(0.95f, 0.78f, 0.12f, 0.8f), null, null, 0);
            if (overlayBlockedCells.Count > 0) GenDraw.DrawFieldEdges(overlayBlockedCells, new Color(0.9f, 0.15f, 0.12f, 0.75f), null, null, 0);
        }

        public int RunReproductionCheck()
        {
            List<Plant> matureTrees = GetMatureTrees();
            if (matureTrees.Count == 0)
            {
                Log.Message("[Historical RimWorld] Ancient forest reproduction: no mature trees to check.");
                return 0;
            }

            int seed = map.ConstantRandSeed ^ (Find.TickManager?.TicksGame ?? 0) ^ (map.uniqueID * 104729) ^ (++manualCheckSequence * 15485863);
            System.Random random = new System.Random(seed);
            Shuffle(matureTrees, random);
            int parentCount = Math.Min(matureTrees.Count, AncientForestGrowthSettings.MaxParentsPerBatch);
            int attempted = 0;
            int successes = 0;
            int livingSoilSuccesses = 0;
            int normalSoilSuccesses = 0;
            int blockedOrDense = 0;
            for (int i = 0; i < parentCount && successes < AncientForestGrowthSettings.MaxSuccessfulReproductionsPerBatch; i++)
            {
                Plant parent = matureTrees[i];
                if (!IsValidMatureParent(parent)) continue;
                if (!TryGetSeedCell(parent.Position, random, out IntVec3 target)) { blockedOrDense++; continue; }

                MapComponent_AncientForestSoil soil = map.GetComponent<MapComponent_AncientForestSoil>();
                bool parentLiving = soil?.IsAncientForestSoil(parent.Position) ?? false;
                bool targetLiving = soil?.IsAncientForestSoil(target) ?? false;
                if (!AncientForestReproductionUtility.CanSpawnAncientTree(target, map, parent.def)) { blockedOrDense++; continue; }
                if (!HasDensityCapacity(target, out _)) { blockedOrDense++; continue; }

                attempted++;
                int distance = Distance(parent.Position, target);
                float distanceWeight = 1f / (1f + 0.25f * distance);
                float chance = AncientForestGrowthSettings.BaseReproductionChance
                    * AncientForestReproductionUtility.GetReproductionMultiplier(parentLiving, targetLiving)
                    * distanceWeight;
                if (random.NextDouble() >= Math.Max(0f, Math.Min(1f, chance))) continue;

                if (SpawnSapling(parent.def, target))
                {
                    successes++;
                    if (targetLiving) livingSoilSuccesses++; else normalSoilSuccesses++;
                }
            }

            Log.Message("[Historical RimWorld] Ancient forest reproduction: mature checked=" + parentCount
                + ", seed attempts=" + attempted + ", success=" + successes
                + " (active soil=" + livingSoilSuccesses + ", ordinary soil=" + normalSoilSuccesses
                + "), blocked/dense=" + blockedOrDense + ".");
            return successes;
        }

        public int RunNaturalRecoveryCheck()
        {
            MapComponent_AncientForestSoil soil = map.GetComponent<MapComponent_AncientForestSoil>();
            List<IntVec3> candidates = soil?.GetAncientForestSoilCells().ToList() ?? new List<IntVec3>();
            if (candidates.Count == 0) return 0;

            int seed = map.ConstantRandSeed ^ (Find.TickManager?.TicksGame ?? 0) ^ (map.uniqueID * 130363) ^ (++manualCheckSequence * 32452843);
            System.Random random = new System.Random(seed);
            Shuffle(candidates, random);
            int checkedCandidates = 0;
            int recovered = 0;
            int limit = Math.Min(candidates.Count, AncientForestGrowthSettings.RecoveryCandidatesPerCheck);
            List<Plant> matureTrees = GetMatureTrees();
            for (int i = 0; i < candidates.Count && checkedCandidates < limit
                && recovered < AncientForestGrowthSettings.MaxRecoverySaplingsPerBatch; i++)
            {
                IntVec3 cell = candidates[i];
                if (!AncientForestReproductionUtility.CanSpawnAncientTree(cell, map, null)
                    || !HasDensityCapacity(cell, out _)
                    || HasMatureTreeWithin(cell, matureTrees, 12)) continue;
                checkedCandidates++;
                if (random.NextDouble() >= AncientForestGrowthSettings.LivingSoilRecoveryChance) continue;

                ThingDef treeDef = GenStep_AncientForest.ChooseSpeciesForMap(map, random);
                if (treeDef != null && SpawnSapling(treeDef, cell)) recovered++;
            }

            Log.Message("[Historical RimWorld] Ancient forest seed-bank recovery: candidates=" + checkedCandidates
                + ", saplings=" + recovered + ", active soil cells=" + candidates.Count + ".");
            return recovered;
        }

        public void RebuildReproductionOverlay()
        {
            overlayLivingCells.Clear();
            overlayNormalCells.Clear();
            overlayBlockedCells.Clear();
            HashSet<IntVec3> seen = new HashSet<IntVec3>();
            foreach (Plant parent in GetMatureTrees())
            {
                for (int z = -AncientForestGrowthSettings.MaximumSeedDistance; z <= AncientForestGrowthSettings.MaximumSeedDistance; z++)
                for (int x = -AncientForestGrowthSettings.MaximumSeedDistance; x <= AncientForestGrowthSettings.MaximumSeedDistance; x++)
                {
                    if (x * x + z * z < 1 || x * x + z * z > AncientForestGrowthSettings.MaximumSeedDistance * AncientForestGrowthSettings.MaximumSeedDistance) continue;
                    IntVec3 cell = parent.Position + new IntVec3(x, 0, z);
                    if (!cell.InBounds(map) || !seen.Add(cell)) continue;
                    bool living = map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(cell) ?? false;
                    if (!AncientForestReproductionUtility.CanSpawnAncientTree(cell, map, parent.def)) overlayBlockedCells.Add(cell);
                    else if (living) overlayLivingCells.Add(cell);
                    else overlayNormalCells.Add(cell);
                }
            }
            Log.Message("[Historical RimWorld] Ancient forest reproduction overlay rebuilt: active-soil candidates="
                + overlayLivingCells.Count + ", ordinary-soil candidates=" + overlayNormalCells.Count
                + ", blocked cells=" + overlayBlockedCells.Count + ".");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "ancientForestGrowthDataVersion", 0);
            Scribe_Values.Look(ref nextReproductionTick, "ancientForestNextReproductionTick", -1);
            Scribe_Values.Look(ref nextRecoveryTick, "ancientForestNextRecoveryTick", -1);
            if (Scribe.mode == LoadSaveMode.Saving) dataVersion = CurrentDataVersion;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (dataVersion != CurrentDataVersion)
                {
                    // Rebuild schedules once for old saves; do not trigger a catch-up burst.
                    nextReproductionTick = -1;
                    nextRecoveryTick = -1;
                    dataVersion = CurrentDataVersion;
                }
                overlayLivingCells = new List<IntVec3>();
                overlayNormalCells = new List<IntVec3>();
                overlayBlockedCells = new List<IntVec3>();
            }
        }

        private List<Plant> GetMatureTrees()
        {
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)
                .OfType<Plant>()
                .Where(plant => plant.def?.GetModExtension<HR_AncientTreeExtension>() != null && plant.Growth >= 1f)
                .ToList();
        }

        private bool IsValidMatureParent(Plant plant)
        {
            return plant != null && !plant.Destroyed && plant.Spawned && plant.Map == map && plant.Growth >= 1f
                && plant.CurrentDyingDamagePerTick <= 0f && plant.def?.GetModExtension<HR_AncientTreeExtension>() != null;
        }

        private bool TryGetSeedCell(IntVec3 parentCell, System.Random random, out IntVec3 target)
        {
            List<IntVec3>[] offsetsByDistance = new List<IntVec3>[AncientForestGrowthSettings.MaximumSeedDistance + 1];
            double totalWeight = 0d;
            for (int distance = AncientForestGrowthSettings.MinimumSeedDistance;
                distance <= AncientForestGrowthSettings.MaximumSeedDistance; distance++)
            {
                offsetsByDistance[distance] = new List<IntVec3>();
                for (int dz = -AncientForestGrowthSettings.MaximumSeedDistance; dz <= AncientForestGrowthSettings.MaximumSeedDistance; dz++)
                for (int dx = -AncientForestGrowthSettings.MaximumSeedDistance; dx <= AncientForestGrowthSettings.MaximumSeedDistance; dx++)
                {
                    int distanceSquared = dx * dx + dz * dz;
                    int roundedDistance = (int)Math.Ceiling(Math.Sqrt(distanceSquared));
                    if (roundedDistance == distance && distanceSquared > 0)
                        offsetsByDistance[distance].Add(new IntVec3(dx, 0, dz));
                }
                if (offsetsByDistance[distance].Count > 0)
                    totalWeight += 1d / (1d + 0.25d * distance);
            }

            double roll = random.NextDouble() * totalWeight;
            int selectedDistance = AncientForestGrowthSettings.MinimumSeedDistance;
            for (int distance = AncientForestGrowthSettings.MinimumSeedDistance;
                distance <= AncientForestGrowthSettings.MaximumSeedDistance; distance++)
            {
                if (offsetsByDistance[distance].Count == 0) continue;
                roll -= 1d / (1d + 0.25d * distance);
                selectedDistance = distance;
                if (roll <= 0d) break;
            }

            List<IntVec3> offsets = offsetsByDistance[selectedDistance];
            for (int tries = 0; tries < offsets.Count; tries++)
            {
                IntVec3 offset = offsets[random.Next(offsets.Count)];
                target = parentCell + offset;
                if (target.InBounds(map)) return true;
            }
            target = IntVec3.Invalid;
            return false;
        }

        private bool HasDensityCapacity(IntVec3 center, out float currentDensity)
        {
            int radius = AncientForestGrowthSettings.DensityRadius;
            int eligibleCells = 0;
            int ancientTrees = 0;
            int livingCells = 0;
            MapComponent_AncientForestSoil soil = map.GetComponent<MapComponent_AncientForestSoil>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map)) continue;
                if (soil?.IsAncientForestSoil(cell) == true) livingCells++;
                if (IsPotentialHabitat(cell)) eligibleCells++;
                Plant plant = cell.GetPlant(map);
                if (plant?.def?.GetModExtension<HR_AncientTreeExtension>() != null && !plant.Destroyed) ancientTrees++;
            }
            currentDensity = eligibleCells == 0 ? 1f : ancientTrees / (float)eligibleCells;
            float livingRatio = livingCells / (float)Math.Max(1, eligibleCells);
            float limit = livingRatio >= 0.60f ? AncientForestGrowthSettings.LivingCoreDensityLimit
                : livingRatio > 0f ? AncientForestGrowthSettings.LivingEdgeDensityLimit
                : AncientForestGrowthSettings.NormalSoilDensityLimit;
            return eligibleCells > 0 && (ancientTrees + 1f) / eligibleCells <= limit;
        }

        private bool IsPotentialHabitat(IntVec3 cell)
        {
            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            if (terrain == null || terrain.IsWater || terrain.affordances == null || terrain.tags == null
                || terrain.tags.Contains("Floor") || !terrain.affordances.Contains(DefDatabase<TerrainAffordanceDef>.GetNamedSilentFail("GrowSoil")))
                return false;
            if (!((map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(cell) ?? false)
                || (terrain.natural && terrain.tags.Contains("Soil") && terrain.fertility > 0.05f))) return false;
            if (cell.GetEdifice(map) != null || map.zoneManager?.ZoneAt(cell) is Zone_Growing) return false;
            return true;
        }

        private bool HasMatureTreeWithin(IntVec3 cell, List<Plant> trees, int radius)
        {
            int radiusSquared = radius * radius;
            return trees.Any(tree => IsValidMatureParent(tree)
                && tree.Position.DistanceToSquared(cell) <= radiusSquared);
        }

        private bool SpawnSapling(ThingDef treeDef, IntVec3 cell)
        {
            if (treeDef == null || !AncientForestReproductionUtility.CanSpawnAncientTree(cell, map, treeDef)) return false;
            Thing thing = ThingMaker.MakeThing(treeDef);
            Plant plant = GenSpawn.Spawn(thing, cell, map) as Plant;
            if (plant == null) return false;
            plant.Growth = 0.001f;
            return true;
        }

        private static int Distance(IntVec3 a, IntVec3 b)
        {
            int dx = a.x - b.x;
            int dz = a.z - b.z;
            return (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz));
        }

        private static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T temp = list[i]; list[i] = list[j]; list[j] = temp;
            }
        }
    }
}
