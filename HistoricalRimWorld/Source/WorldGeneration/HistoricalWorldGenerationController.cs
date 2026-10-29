using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldGeneration
{
    public static class HistoricalWorldGenerationController
    {
        private const int CurrentMappingVersion = 1;
        private const int PrototypeRegionRadiusInTiles = 4;
        private const float MaximumPrototypePlacementErrorDegrees = 18f;

        public static bool ValidateConfiguration(HistoricalScenarioDef scenario, out string error)
        {
            error = string.Join(Environment.NewLine, HistoricalFactionManager.ValidateBindings(scenario));
            if (!string.IsNullOrEmpty(error)) return false;

            List<HistoricalSettlementPlacementDef> placements = GetPlacements(scenario.defName);
            foreach (HistoricalFactionBindingDef binding in HistoricalFactionManager.GetBindings(scenario.defName))
            {
                if (!placements.Any(item => item.polity == binding.historicalState))
                {
                    error = "No prototype settlement placement exists for " + binding.historicalState.defName + ".";
                    return false;
                }
            }
            error = null;
            return true;
        }

        public static void InitializeGeneratedWorld(RimWorldWorld world, HistoricalScenarioDef scenario,
            IEnumerable<FactionDef> factionDefs)
        {
            if (world == null || scenario == null) return;
            HistoricalFactionWorldComponent component = world.GetComponent<HistoricalFactionWorldComponent>();
            if (component == null)
            {
                Log.Error("[Historical RimWorld] HistoricalFactionWorldComponent is unavailable after world generation.");
                return;
            }

            component.Initialize(scenario.defName, CurrentMappingVersion, world.factionManager?.AllFactionsListForReading);
            if (!ValidateConfiguration(scenario, out string error))
            {
                component.MarkGenerationResult(false, error);
                Log.Error("[Historical RimWorld] Historical world generation configuration is invalid: " + error);
                return;
            }

            List<Faction> factions = world.factionManager?.AllFactionsListForReading;
            if (factions == null)
            {
                error = "FactionManager is unavailable after historical world generation.";
                component.MarkGenerationResult(false, error);
                Log.Error("[Historical RimWorld] " + error);
                return;
            }

            HistoricalFactionDiplomacyAdapter.ApplyInitialRelations(world, scenario.defName);

            bool placementComplete = PlaceHistoricalSettlements(world, scenario, factionDefs, component, out error);
            component.MarkGenerationResult(placementComplete, error);
            if (!placementComplete)
                Log.Error("[Historical RimWorld] Historical settlement placement was incomplete: " + error);

            if (placementComplete)
                Log.Message("[Historical RimWorld] Generated prototype historical factions and settlement mappings for " + scenario.defName + ".");
        }

        internal static List<FactionDef> GetHistoricalFactionDefs(string scenarioId) =>
            HistoricalFactionManager.GetBindings(scenarioId)
                .Select(item => item.rimWorldFactionDef)
                .Distinct()
                .ToList();

        private static bool PlaceHistoricalSettlements(RimWorldWorld world, HistoricalScenarioDef scenario,
            IEnumerable<FactionDef> factionDefs, HistoricalFactionWorldComponent component, out string error)
        {
            error = null;
            if (world.grid == null || world.worldObjects == null)
            {
                error = "World grid or world object holder is unavailable.";
                return false;
            }

            HashSet<FactionDef> generatedDefs = new HashSet<FactionDef>(factionDefs ?? Enumerable.Empty<FactionDef>());
            List<HistoricalFactionBindingDef> bindings = HistoricalFactionManager.GetBindings(scenario.defName)
                .Where(item => generatedDefs.Contains(item.rimWorldFactionDef)).ToList();
            List<Settlement> movable = world.worldObjects.Settlements
                .Where(item => item?.Faction != null && bindings.Any(binding => binding.rimWorldFactionDef == item.Faction.def))
                .ToList();
            HashSet<WorldObject> movableSet = new HashSet<WorldObject>(movable);
            HashSet<int> reservedTiles = new HashSet<int>();
            List<Tuple<string, string, int>> placementAnchors = new List<Tuple<string, string, int>>();

            if (!TryFindAnchor(world, out Vector2 anchor))
            {
                error = "No passable, buildable surface tile was available for the prototype layout anchor.";
                return false;
            }

            List<string> failures = new List<string>();
            foreach (HistoricalSettlementPlacementDef placement in GetPlacements(scenario.defName))
            {
                HistoricalFactionBindingDef binding = bindings.FirstOrDefault(item => item.historicalState == placement.polity);
                if (binding == null) continue;
                Faction faction = world.factionManager.AllFactionsListForReading
                    .FirstOrDefault(item => item != null && item.def == binding.rimWorldFactionDef);
                List<Settlement> factionSettlements = movable.Where(item => item.Faction == faction).ToList();
                if (factionSettlements.Count < binding.desiredSettlementCount)
                    failures.Add("Expected at least " + binding.desiredSettlementCount + " world settlement(s) for "
                        + placement.polity.defName + ", but found " + factionSettlements.Count + ".");
                if (factionSettlements.Count == 0)
                {
                    if (placement.required) failures.Add("No generated world settlement for " + placement.polity.defName + ".");
                    continue;
                }

                int placedCount = 0;
                foreach (Settlement settlement in factionSettlements)
                {
                    Vector2 target = new Vector2(
                        NormalizeLongitude(anchor.x + placement.prototypeLongitudeOffset),
                        Mathf.Clamp(anchor.y + placement.prototypeLatitudeOffset, -85f, 85f));
                    int tileId;
                    if (!TryFindPlacementTile(world, target, movableSet, reservedTiles,
                        placement.minimumDistanceFromOtherSettlements, out tileId))
                    {
                        failures.Add("No available tile near the prototype region for " + placement.polity.defName + ".");
                        continue;
                    }

                    PlanetTile destination = new PlanetTile(tileId);
                    world.worldObjects.Remove(settlement);
                    settlement.Tile = destination;
                    world.worldObjects.Add(settlement);
                    reservedTiles.Add(tileId);
                    component.RecordSettlement(settlement, placement.polity.defName, placement.region.defName, tileId);
                    placementAnchors.Add(Tuple.Create(placement.polity.defName, placement.region.defName, tileId));
                    placedCount++;
                }

                if (placement.required && placedCount == 0)
                    failures.Add("Required prototype placement failed for " + placement.polity.defName + ".");
            }

            RecordPrototypeRegions(world, component, placementAnchors);

            if (failures.Count == 0) return true;
            error = string.Join(" ", failures.Distinct());
            return false;
        }

        private static void RecordPrototypeRegions(RimWorldWorld world, HistoricalFactionWorldComponent component,
            IEnumerable<Tuple<string, string, int>> placementAnchors)
        {
            List<Tuple<string, string, int>> anchors = placementAnchors.ToList();
            for (int candidate = 0; candidate < world.grid.TilesCount; candidate++)
            {
                Tuple<string, string, float> nearest = anchors
                    .Select(anchor => Tuple.Create(anchor.Item1, anchor.Item2,
                        world.grid.ApproxDistanceInTiles(new PlanetTile(candidate), new PlanetTile(anchor.Item3))))
                    .Where(item => item.Item3 <= PrototypeRegionRadiusInTiles)
                    .OrderBy(item => item.Item3)
                    .ThenBy(item => item.Item2, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (nearest != null) component.RecordRegionTile(nearest.Item1, nearest.Item2, candidate);
            }
        }

        private static bool TryFindAnchor(RimWorldWorld world, out Vector2 anchor)
        {
            anchor = default(Vector2);
            float bestDistance = float.PositiveInfinity;
            for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
            {
                Tile tile = world.grid[tileId];
                if (!IsBuildable(tile)) continue;
                Vector2 coordinate = world.grid.LongLatOf(new PlanetTile(tileId));
                float distance = AngularDistanceSquared(coordinate, Vector2.zero);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                anchor = coordinate;
            }
            return !float.IsPositiveInfinity(bestDistance);
        }

        private static bool TryFindPlacementTile(RimWorldWorld world, Vector2 target, HashSet<WorldObject> movable,
            HashSet<int> reserved, int minimumDistance, out int tileId)
        {
            tileId = -1;
            List<Tuple<float, int>> candidates = new List<Tuple<float, int>>();
            for (int candidate = 0; candidate < world.grid.TilesCount; candidate++)
            {
                Tile tile = world.grid[candidate];
                if (!IsBuildable(tile) || reserved.Contains(candidate)) continue;
                PlanetTile candidateTile = new PlanetTile(candidate);
                if (world.worldObjects.AnyWorldObjectAt(candidateTile)
                    && !world.worldObjects.ObjectsAt(candidateTile).All(movable.Contains)) continue;

                Vector2 coordinate = world.grid.LongLatOf(candidateTile);
                candidates.Add(Tuple.Create(AngularDistanceSquared(coordinate, target), candidate));
            }

            foreach (Tuple<float, int> candidate in candidates.OrderBy(item => item.Item1).ThenBy(item => item.Item2))
            {
                if (candidate.Item1 > MaximumPrototypePlacementErrorDegrees * MaximumPrototypePlacementErrorDegrees) break;
                PlanetTile tile = new PlanetTile(candidate.Item2);
                if (reserved.Any(other => world.grid.ApproxDistanceInTiles(tile, new PlanetTile(other)) < minimumDistance)) continue;
                tileId = candidate.Item2;
                return true;
            }
            return false;
        }

        private static bool IsBuildable(Tile tile) => tile != null && !tile.WaterCovered
            && tile.PrimaryBiome != null && !tile.PrimaryBiome.impassable && tile.PrimaryBiome.canBuildBase;

        private static float AngularDistanceSquared(Vector2 first, Vector2 second)
        {
            float longitude = Mathf.Abs(first.x - second.x);
            longitude = Mathf.Min(longitude, 360f - longitude) * Mathf.Cos(first.y * Mathf.Deg2Rad);
            float latitude = first.y - second.y;
            return longitude * longitude + latitude * latitude;
        }

        private static float NormalizeLongitude(float longitude)
        {
            while (longitude > 180f) longitude -= 360f;
            while (longitude < -180f) longitude += 360f;
            return longitude;
        }

        private static List<HistoricalSettlementPlacementDef> GetPlacements(string scenarioId) =>
            DefDatabase<HistoricalSettlementPlacementDef>.AllDefsListForReading
                .Where(item => item != null && item.scenario != null && item.scenario.defName == scenarioId)
                .OrderBy(item => item.defName, StringComparer.Ordinal)
                .ToList();
    }
}
