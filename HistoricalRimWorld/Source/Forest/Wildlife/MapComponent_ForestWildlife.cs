using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Forest
{
    /// <summary>
    /// Low-frequency wildlife bookkeeping layered over vanilla animal pawns and HR-046 forest regions.
    /// This component never creates animals; every count is reconciled from actual living wild pawns.
    /// </summary>
    public sealed class MapComponent_ForestWildlife : MapComponent
    {
        public const int PopulationRefreshInterval = 2500;
        public const int DailyDecisionInterval = 60000;
        private const float SevereHungerThreshold = 0.18f;
        private const float LowHabitatFoodThreshold = 0.35f;
        private const int LivestockHuntCooldown = 60000;
        private const int MigrationOrderCooldown = 60000;

        private List<ForestWildlifePopulation> populations = new List<ForestWildlifePopulation>();
        private int lastRefreshTick = -1;
        private int lastDecisionTick = -1;
        private bool needsRebuild = true;
        private readonly Dictionary<Pawn, int> regionByAnimal = new Dictionary<Pawn, int>();
        private readonly List<Pawn> allWildAnimals = new List<Pawn>();
        private readonly List<Pawn> wildConfiguredAnimals = new List<Pawn>();
        private readonly HashSet<Pawn> temporaryPawns = new HashSet<Pawn>();
        private readonly List<ForestEcologyRegion> forestRegions = new List<ForestEcologyRegion>();
        private readonly List<ThingDef> configuredSpeciesCache = new List<ThingDef>();
        private bool configuredSpeciesCacheBuilt;

        public MapComponent_ForestWildlife(Map map) : base(map) { }

        public IReadOnlyList<ForestWildlifePopulation> Populations => populations;
        public int WildConfiguredAnimalCount => wildConfiguredAnimals.Count;
        public int MigratingPopulationCount => populations.Count(item => item != null && item.MigrationPressure);

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive || Find.TickManager == null || map == null) return;

            int tick = Find.TickManager.TicksGame;
            if (!needsRebuild && lastRefreshTick >= 0 && tick - lastRefreshTick < PopulationRefreshInterval) return;
            RebuildSnapshot(tick);

            if (lastDecisionTick < 0 || tick - lastDecisionTick >= DailyDecisionInterval)
            {
                lastDecisionTick = tick;
                ProcessMigrationAndPredation(tick);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            needsRebuild = true;
            lastRefreshTick = -1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastRefreshTick, "forestWildlifeLastRefreshTick", -1);
            Scribe_Values.Look(ref lastDecisionTick, "forestWildlifeLastDecisionTick", -1);
            Scribe_Collections.Look(ref populations, "forestWildlifePopulations", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                populations = populations ?? new List<ForestWildlifePopulation>();
                needsRebuild = true;
                regionByAnimal.Clear();
                wildConfiguredAnimals.Clear();
            }
        }

        public ForestWildlifePopulation GetPopulation(int forestRegionId, string speciesDefName)
        {
            return populations.FirstOrDefault(item => item.ForestRegionId == forestRegionId
                && item.SpeciesDefName == speciesDefName);
        }

        public void RequestRefresh()
        {
            needsRebuild = true;
        }

        private void RebuildSnapshot(int tick)
        {
            MapComponent_ForestEcology forest = map.GetComponent<MapComponent_ForestEcology>();
            populations = populations ?? new List<ForestWildlifePopulation>();
            populations.RemoveAll(item => item == null);
            regionByAnimal.Clear();
            allWildAnimals.Clear();
            wildConfiguredAnimals.Clear();
            forestRegions.Clear();
            if (forest != null) forestRegions.AddRange(forest.GetAllRegions().Where(region => region != null));

            temporaryPawns.Clear();
            if (PawnsFinder.Temporary != null)
                foreach (Pawn pawn in PawnsFinder.Temporary)
                    if (pawn != null) temporaryPawns.Add(pawn);

            IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn animal = pawns[i];
                    if (!IsWildAnimal(animal) || temporaryPawns.Contains(animal)) continue;
                    allWildAnimals.Add(animal);
                    ForestEcologyRegion region = FindAssociatedRegion(animal, forest);
                    if (region != null) regionByAnimal[animal] = region.RegionId;
                    if (animal.def?.GetModExtension<HR_ForestWildlifeSpeciesExtension>() != null)
                        wildConfiguredAnimals.Add(animal);
                }
            }

            var activeKeys = new HashSet<string>();
            foreach (ForestEcologyRegion region in forestRegions)
            {
                foreach (ThingDef species in ConfiguredSpecies())
                {
                    string key = PopulationKey(region.RegionId, species.defName);
                    activeKeys.Add(key);
                    ForestWildlifePopulation state = GetOrCreatePopulation(region, species);
                    state.CurrentCount = 0;
                    state.ForestRegionId = region.RegionId;
                    state.ForestRegionAnchorCell = region.AnchorCellIndex;
                    state.HabitatCenter = region.Center;
                    state.LastPopulationUpdateTick = tick;
                    state.HabitatSuitability = CalculateSpeciesHabitatSuitability(region, species, state);
                }
            }

            var herbivoreCountsByRegion = new Dictionary<int, int>();
            foreach (Pawn animal in allWildAnimals)
            {
                if (!regionByAnimal.TryGetValue(animal, out int regionId)) continue;
                if (animal.def.GetModExtension<HR_ForestWildlifeSpeciesExtension>()?.isPredator == true) continue;
                herbivoreCountsByRegion.TryGetValue(regionId, out int count);
                herbivoreCountsByRegion[regionId] = count + 1;
            }

            foreach (Pawn animal in wildConfiguredAnimals)
            {
                if (!regionByAnimal.TryGetValue(animal, out int regionId)) continue;
                ForestWildlifePopulation state = GetPopulation(regionId, animal.def.defName);
                if (state != null) state.CurrentCount++;
            }

            foreach (ForestEcologyRegion region in forestRegions)
            {
                herbivoreCountsByRegion.TryGetValue(region.RegionId, out int herbivores);
                foreach (ThingDef species in ConfiguredSpecies())
                {
                    ForestWildlifePopulation state = GetPopulation(region.RegionId, species.defName);
                    if (state == null) continue;
                    HR_ForestWildlifeSpeciesExtension settings = species.GetModExtension<HR_ForestWildlifeSpeciesExtension>();
                    state.FoodAvailability = CalculateFoodAvailability(region, herbivores, settings);
                    state.DisturbanceLevel = Math.Max(0f, Math.Min(1f, 1f - region.EcologicalIntegrity / 100f));
                    float areaFactor = (float)Math.Sqrt(Math.Max(0f, region.ForestAreaCells) / 500f);
                    float target = settings.baseCapacity * state.HabitatSuitability * areaFactor * state.FoodAvailability;
                    state.TargetCount = state.HabitatSuitability <= 0f ? 0
                        : Math.Min(settings.maximumPopulation, Math.Max(0, (int)Math.Round(target)));
                    state.MigrationPressure = state.CurrentCount > state.TargetCount
                        || (state.CurrentCount > 0 && state.HabitatSuitability <= 0f);
                }
            }

            populations.RemoveAll(item => item == null || string.IsNullOrEmpty(item.SpeciesDefName)
                || !activeKeys.Contains(PopulationKey(item.ForestRegionId, item.SpeciesDefName)));
            lastRefreshTick = tick;
            needsRebuild = false;
        }

        private ForestWildlifePopulation GetOrCreatePopulation(ForestEcologyRegion region, ThingDef species)
        {
            ForestWildlifePopulation existing = populations.FirstOrDefault(item => item != null
                && item.SpeciesDefName == species.defName
                && (item.ForestRegionId == region.RegionId || item.ForestRegionAnchorCell == region.AnchorCellIndex));
            if (existing != null) return existing;
            var created = new ForestWildlifePopulation
            {
                SpeciesDefName = species.defName,
                ForestRegionId = region.RegionId,
                ForestRegionAnchorCell = region.AnchorCellIndex,
                HabitatCenter = region.Center
            };
            populations.Add(created);
            return created;
        }

        private ForestEcologyRegion FindAssociatedRegion(Pawn animal, MapComponent_ForestEcology forest)
        {
            if (animal == null || animal.Destroyed || !animal.Spawned || map == null
                || !animal.Position.InBounds(map) || forest == null || forestRegions.Count == 0) return null;
            ForestEcologyRegion direct = forest.GetRegionAt(animal.Position);
            if (direct != null) return direct;

            HR_ForestWildlifeSpeciesExtension settings = animal.def?.GetModExtension<HR_ForestWildlifeSpeciesExtension>();
            // Unconfigured native wildlife is also mapped as prey for habitat food pressure.
            int radius = Math.Max(settings?.activityRadius ?? 35, 10);
            int maxDistanceSquared = radius * radius;
            ForestEcologyRegion nearest = null;
            int nearestDistance = int.MaxValue;
            foreach (ForestEcologyRegion region in forestRegions)
            {
                if (region == null) continue;
                int distance = animal.Position.DistanceToSquared(region.Center);
                if (distance > maxDistanceSquared || distance >= nearestDistance) continue;
                nearest = region;
                nearestDistance = distance;
            }
            return nearest;
        }

        private float CalculateSpeciesHabitatSuitability(ForestEcologyRegion region, ThingDef species,
            ForestWildlifePopulation state)
        {
            HR_ForestWildlifeSpeciesExtension settings = species.GetModExtension<HR_ForestWildlifeSpeciesExtension>();
            if (settings == null || region.ForestAreaCells < Math.Max(0, settings.minimumForestArea)
                || region.TreeDensity < settings.minimumTreeDensity || region.EcologicalIntegrity < settings.minimumIntegrity)
                return 0f;

            float biomeFactor = BiomeFactor(map.Biome?.defName, settings.preferredBiomeKeywords);
            if (biomeFactor <= 0f) return 0f;
            float areaFactor = Math.Min(1f, region.ForestAreaCells / (float)Math.Max(1, settings.minimumForestArea * 3));
            float densityFactor = Math.Min(1f, region.TreeDensity / Math.Max(0.01f, settings.minimumTreeDensity * 2f));
            float integrityFactor = Math.Min(1f, region.EcologicalIntegrity / Math.Max(1f, settings.minimumIntegrity * 1.5f));
            float capacityFactor = Math.Max(0f, Math.Min(1f, region.HabitatCapacityScore / 75f));
            return Math.Max(0f, Math.Min(1f, biomeFactor * areaFactor * densityFactor * integrityFactor
                * (0.5f + 0.5f * capacityFactor)));
        }

        private static float BiomeFactor(string biomeName, List<string> preferredKeywords)
        {
            if (string.IsNullOrEmpty(biomeName)) return 0.25f;
            if (preferredKeywords == null || preferredKeywords.Count == 0) return 0.6f;
            foreach (string keyword in preferredKeywords)
                if (!string.IsNullOrEmpty(keyword) && biomeName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return 1f;
            if (biomeName.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0) return 0.5f;
            if (biomeName.IndexOf("Swamp", StringComparison.OrdinalIgnoreCase) >= 0) return 0.4f;
            return 0.15f;
        }

        private static float CalculateFoodAvailability(ForestEcologyRegion region, int localHerbivores,
            HR_ForestWildlifeSpeciesExtension settings)
        {
            float vegetationFood = Math.Min(1f, region.TreeDensity * 1.25f + region.EcologicalIntegrity / 400f);
            float preyFood = Math.Min(1f, localHerbivores / 8f);
            // Long-term habitat supply; deliberately does not read an animal's Need_Food.
            return Math.Max(0.05f, Math.Min(1f, settings.isPredator
                ? 0.15f + preyFood * 0.85f
                : 0.2f + vegetationFood * 0.8f));
        }

        private void ProcessMigrationAndPredation(int tick)
        {
            foreach (ForestWildlifePopulation source in populations.OrderBy(item => item.SpeciesDefName)
                .ThenBy(item => item.ForestRegionId))
            {
                if (source == null || source.CurrentCount <= 0) continue;
                ThingDef species = DefDatabase<ThingDef>.GetNamedSilentFail(source.SpeciesDefName);
                HR_ForestWildlifeSpeciesExtension settings = species?.GetModExtension<HR_ForestWildlifeSpeciesExtension>();
                if (settings == null) continue;

                if (source.MigrationPressure && tick - source.LastMigrationOrderTick >= MigrationOrderCooldown)
                    TryOrderMigration(source, settings, tick);

                if (settings.canHuntLivestock && settings.isPredator
                    && source.FoodAvailability < LowHabitatFoodThreshold
                    && tick - source.LastLivestockHuntTick >= LivestockHuntCooldown)
                    TryLivestockHunt(source, settings, tick);
            }
        }

        private void TryOrderMigration(ForestWildlifePopulation source, HR_ForestWildlifeSpeciesExtension settings, int tick)
        {
            ForestEcologyRegion destination = null;
            float bestScore = 0f;
            foreach (ForestEcologyRegion region in forestRegions)
            {
                if (region.RegionId == source.ForestRegionId) continue;
                float suitability = CalculateSpeciesHabitatSuitability(region,
                    DefDatabase<ThingDef>.GetNamedSilentFail(source.SpeciesDefName), source);
                if (suitability < 0.2f) continue;
                ForestWildlifePopulation destinationPopulation = GetPopulation(region.RegionId, source.SpeciesDefName);
                if (destinationPopulation != null && destinationPopulation.CurrentCount >= destinationPopulation.TargetCount) continue;
                float score = suitability - source.HabitatCenter.DistanceToSquared(region.Center) / 100000f;
                if (score > bestScore) { bestScore = score; destination = region; }
            }
            if (destination == null) return;

            JobDef gotoJob = DefDatabase<JobDef>.GetNamedSilentFail("Goto");
            if (gotoJob == null) return;
            Pawn animal = wildConfiguredAnimals
                .Where(pawn => pawn.def.defName == source.SpeciesDefName
                    && regionByAnimal.TryGetValue(pawn, out int regionId) && regionId == source.ForestRegionId
                    && (pawn.jobs?.curJob == null || pawn.jobs.curJob.def?.defName == "Wander"))
                .OrderBy(pawn => pawn.thingIDNumber).FirstOrDefault();
            if (animal == null || animal.jobs == null || !destination.Center.IsValid || !destination.Center.InBounds(map)
                || !animal.CanReach(destination.Center, PathEndMode.OnCell, Danger.Some)) return;

            if (animal.jobs.TryTakeOrderedJob(JobMaker.MakeJob(gotoJob, destination.Center), JobTag.Misc))
                source.LastMigrationOrderTick = tick;
        }

        private void TryLivestockHunt(ForestWildlifePopulation source,
            HR_ForestWildlifeSpeciesExtension settings, int tick)
        {
            JobDef predatorHunt = DefDatabase<JobDef>.GetNamedSilentFail("PredatorHunt");
            if (predatorHunt == null) return;
            Pawn predator = wildConfiguredAnimals
                .Where(pawn => pawn.def.defName == source.SpeciesDefName && pawn.jobs != null && pawn.needs?.food != null
                    && pawn.needs.food.CurLevelPercentage <= SevereHungerThreshold
                    && pawn.jobs?.curJob?.def?.defName != "PredatorHunt"
                    && (pawn.jobs?.curJob == null || pawn.jobs.curJob.def?.defName == "Wander"))
                .OrderBy(pawn => pawn.thingIDNumber).FirstOrDefault();
            if (predator == null) return;

            Pawn livestock = (map.mapPawns?.AllPawnsSpawned ?? new List<Pawn>())
                .Where(pawn => pawn != null && pawn != predator && !pawn.Dead && !pawn.Downed && pawn.IsAnimal
                    && pawn.Faction?.IsPlayer == true && pawn.Position.DistanceToSquared(predator.Position) <= 24 * 24
                    && (pawn.jobs?.curJob == null || pawn.jobs.curJob.def?.defName != "PredatorHunt")
                    && predator.CanReach(pawn, PathEndMode.Touch, Danger.Some))
                .OrderBy(pawn => predator.Position.DistanceToSquared(pawn.Position)).FirstOrDefault();
            if (livestock == null) return;

            float hungerFactor = Math.Max(0f, Math.Min(1f,
                (SevereHungerThreshold - predator.needs.food.CurLevelPercentage) / SevereHungerThreshold));
            float pressureFactor = Math.Max(0f, Math.Min(1f,
                (LowHabitatFoodThreshold - source.FoodAvailability) / LowHabitatFoodThreshold));
            float chance = Math.Min(0.35f, settings.baseLivestockHuntChance * (0.25f + hungerFactor * 0.75f)
                * (0.25f + pressureFactor * 0.75f) * (1f + source.DisturbanceLevel));
            if (Rand.Value > chance) return;

            if (predator.jobs.TryTakeOrderedJob(JobMaker.MakeJob(predatorHunt, livestock), JobTag.Misc))
                source.LastLivestockHuntTick = tick;
        }

        private IEnumerable<ThingDef> ConfiguredSpecies()
        {
            if (!configuredSpeciesCacheBuilt)
            {
                configuredSpeciesCache.AddRange(DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def?.race?.Animal == true
                    && def.GetModExtension<HR_ForestWildlifeSpeciesExtension>() != null));
                configuredSpeciesCacheBuilt = true;
            }
            return configuredSpeciesCache;
        }

        private static bool IsWildAnimal(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned
                && pawn.def?.race?.Animal == true && pawn.Faction == null && !pawn.InContainerEnclosed;
        }

        private static string PopulationKey(int regionId, string speciesDefName) => regionId + ":" + speciesDefName;
    }
}
