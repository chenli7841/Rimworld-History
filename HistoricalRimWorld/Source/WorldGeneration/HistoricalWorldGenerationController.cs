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
        private const int CurrentMappingVersion = 2;
        private const int PrototypeRegionRadiusInTiles = 4;
        private const float MaximumPrototypePlacementErrorDegrees = 18f;

        public static bool ValidateConfiguration(HistoricalScenarioDef scenario, out string error)
        {
            error = string.Join(Environment.NewLine, HistoricalFactionManager.ValidateBindings(scenario));
            if (!string.IsNullOrEmpty(error)) return false;

            HistoricalWorldRevealSetupDef geographySetup = DefDatabase<HistoricalWorldRevealSetupDef>.AllDefsListForReading
                .FirstOrDefault(item => item?.scenario == scenario);
            if (geographySetup == null)
            {
                error = "No world geography setup exists for " + scenario.defName + ".";
                return false;
            }
            if (!TryGetWorldGenerationDef(scenario, out HistoricalWorldGenerationDef generationDef))
            {
                error = "No HistoricalWorldGenerationDef exists for " + scenario.defName + ".";
                return false;
            }
            if (!HistoricalWorldGenerationValidator.Validate(generationDef, out error)) return false;
            if (geographySetup.useUniformPlainsTemplate && geographySetup.mapMode != HistoricalWorldMapMode.PrototypeWorld)
            {
                error = "The uniform terrain template requires PrototypeWorld coordinate mapping.";
                return false;
            }
            if (!geographySetup.useUniformPlainsTemplate && geographySetup.mapMode != HistoricalWorldMapMode.HistoricalEarth)
            {
                error = "The real China geography dataset is not installed. The prototype world layout cannot be used for this historical scenario.";
                return false;
            }
            if (!geographySetup.useUniformPlainsTemplate && !new HistoricalEarthGeoMapper().IsAvailable)
            {
                error = "The real China geography dataset is missing or unavailable. Install a validated HR-013 dataset before creating this historical world.";
                return false;
            }

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

        public static bool TryGetWorldGenerationDef(HistoricalScenarioDef scenario, out HistoricalWorldGenerationDef def)
        {
            def = DefDatabase<HistoricalWorldGenerationDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.scenario == scenario);
            return def != null;
        }

        public static bool TryPrepareWorldGeneration(HistoricalScenarioDef scenario, out float planetCoverage,
            out HistoricalSphericalCoverage coverage, out string error)
        {
            planetCoverage = 0f;
            coverage = default(HistoricalSphericalCoverage);
            if (!TryGetWorldGenerationDef(scenario, out HistoricalWorldGenerationDef def))
            {
                error = "No HistoricalWorldGenerationDef exists for " + scenario?.defName + ".";
                return false;
            }
            return HistoricalCoverageCalculator.TryCalculate(def, out _, out coverage, out planetCoverage, out error);
        }

        private static bool ValidateGeneratedWorldCoverage(RimWorldWorld world, HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (!TryGetWorldGenerationDef(scenario, out HistoricalWorldGenerationDef def))
            { error = "HistoricalWorldGenerationDef is missing."; return false; }
            if (!def.requireCompleteCoverage) return true;
            if (!HistoricalCoverageCalculator.TryCalculate(def, out HistoricalGeoBounds bounds,
                out _, out _, out error)) return false;
            if (world?.grid?.Surface == null || world.grid.TilesCount == 0)
            { error = "Generated surface WorldGrid has no tiles."; return false; }

            List<Vector3> tileCenters = new List<Vector3>(world.grid.TilesCount);
            for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
            {
                Vector2 coordinate = world.grid.Surface.LongLatOf(tileId);
                tileCenters.Add(HistoricalCoverageCalculator.ToUnitVector(coordinate.x, coordinate.y));
            }

            double unwrappedMinLongitude = def.minLongitude - def.longitudeMargin;
            double unwrappedMaxLongitude = def.maxLongitude + def.longitudeMargin;
            if (unwrappedMaxLongitude < unwrappedMinLongitude) unwrappedMaxLongitude += 360.0;
            List<Tuple<double, double>> samples = HistoricalCoverageCalculator.SampleBounds(
                bounds.MinLatitude, bounds.MaxLatitude, unwrappedMinLongitude, unwrappedMaxLongitude, 1.0);
            double worstError = 0.0;
            Tuple<double, double> worstPoint = null;
            foreach (Tuple<double, double> sample in samples)
            {
                Vector3 target = HistoricalCoverageCalculator.ToUnitVector(sample.Item1, sample.Item2);
                double nearestDot = -1.0;
                foreach (Vector3 center in tileCenters)
                {
                    double dot = target.x * center.x + target.y * center.y + target.z * center.z;
                    if (dot > nearestDot) nearestDot = dot;
                }
                double closest = Math.Acos(Math.Max(-1.0, Math.Min(1.0, nearestDot))) * 180.0 / Math.PI;
                if (closest <= worstError) continue;
                worstError = closest;
                worstPoint = sample;
            }
            if (worstError > def.maximumTileCenterErrorDegrees)
            {
                error = "Generated WorldGrid does not adequately cover the scenario bounds. Worst sampled coordinate "
                    + worstPoint.Item1.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°, "
                    + worstPoint.Item2.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + "° is " + worstError.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + "° from the nearest generated tile center (limit " + def.maximumTileCenterErrorDegrees + "°).";
                return false;
            }
            Log.Message("[Historical RimWorld] Verified full scenario geographic bounds: " + samples.Count
                + " samples; worst tile-center offset " + worstError.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°.");
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

            if (!ValidateGeneratedWorldCoverage(world, scenario, out error))
            {
                component.MarkGenerationResult(false, error);
                Log.Error("[Historical RimWorld] Historical world coverage validation failed: " + error);
                return;
            }

            HistoricalWorldRevealSetupDef geographySetup = DefDatabase<HistoricalWorldRevealSetupDef>.AllDefsListForReading
                .First(item => item?.scenario == scenario);
            TryGetWorldGenerationDef(scenario, out HistoricalWorldGenerationDef generationDef);
            if (geographySetup.useUniformPlainsTemplate
                && !HistoricalBlankWorldTemplate.TryApply(world, geographySetup.worldMap,
                    generationDef.maximumTileCenterErrorDegrees, out error))
            {
                component.MarkGenerationResult(false, error);
                Log.Error("[Historical RimWorld] Blank world template could not be applied: " + error);
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
                Log.Message("[Historical RimWorld] Generated historical scenario template and settlement mappings for " + scenario.defName + ".");
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
                string historicalSettlementName = placement.settlement?.LabelCap.ToString();
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

                    PlanetTile destination = new PlanetTile(tileId, world.grid.Surface);
                    world.worldObjects.Remove(settlement);
                    settlement.Tile = destination;
                    if (!string.IsNullOrWhiteSpace(historicalSettlementName))
                    {
                        settlement.Name = placedCount == 0
                            ? historicalSettlementName
                            : historicalSettlementName + " (" + (placedCount + 1) + ")";
                    }
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
                        world.grid.ApproxDistanceInTiles(new PlanetTile(candidate, world.grid.Surface),
                            new PlanetTile(anchor.Item3, world.grid.Surface))))
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
                Vector2 coordinate = world.grid.LongLatOf(new PlanetTile(tileId, world.grid.Surface));
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
                PlanetTile candidateTile = new PlanetTile(candidate, world.grid.Surface);
                if (world.worldObjects.AnyWorldObjectAt(candidateTile)
                    && !world.worldObjects.ObjectsAt(candidateTile).All(movable.Contains)) continue;

                Vector2 coordinate = world.grid.LongLatOf(candidateTile);
                candidates.Add(Tuple.Create(AngularDistanceSquared(coordinate, target), candidate));
            }

            foreach (Tuple<float, int> candidate in candidates.OrderBy(item => item.Item1).ThenBy(item => item.Item2))
            {
                if (candidate.Item1 > MaximumPrototypePlacementErrorDegrees * MaximumPrototypePlacementErrorDegrees) break;
                PlanetTile tile = new PlanetTile(candidate.Item2, world.grid.Surface);
                if (reserved.Any(other => world.grid.ApproxDistanceInTiles(tile,
                    new PlanetTile(other, world.grid.Surface)) < minimumDistance)) continue;
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
