using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.World;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldGeneration
{
    /// <summary>Creates the named, icon-bearing XML locations after faction settlements are placed.</summary>
    public static class HistoricalWorldMapMarkerPlacer
    {
        public static bool TryPlace(RimWorldWorld world, HistoricalWorldMapDef map, HistoricalMapScaleDef scale,
            float maximumCoordinateErrorDegrees, out string error)
        {
            error = null;
            if (map?.locations == null || map.locations.Count == 0) return true;
            if (world?.grid?.Surface == null || world.worldObjects == null)
            {
                error = "World grid or world object holder is unavailable for map locations.";
                return false;
            }

            HistoricalMapScaleTransformer transformer;
            try { transformer = new HistoricalMapScaleTransformer(scale); }
            catch (Exception exception)
            {
                error = "The historical map scale could not be initialized: " + exception.Message;
                return false;
            }
            List<Tuple<HistoricalWorldMapLocation, int>> placedLocations = new List<Tuple<HistoricalWorldMapLocation, int>>();

            foreach (HistoricalWorldMapLocation location in map.locations)
            {
                if (location == null || location.worldObjectDef == null)
                {
                    error = "A map location is missing its data or WorldObjectDef.";
                    return false;
                }

                HistoricalGameGeoCoordinate gameCoordinate;
                try
                {
                    gameCoordinate = transformer.ToGameCoordinate(
                        new HistoricalGeoCoordinate(location.latitude, location.longitude));
                }
                catch (Exception exception)
                {
                    error = "Map location '" + location.locationId + "' cannot be projected: " + exception.Message;
                    return false;
                }

                int bestTile = -1;
                double bestDistance = double.PositiveInfinity;
                for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
                {
                    Tile tile = world.grid.Surface.Tiles[tileId];
                    if (tile == null || tile.WaterCovered || tile.PrimaryBiome == null
                        || tile.PrimaryBiome.impassable || !tile.PrimaryBiome.canBuildBase)
                        continue;
                    PlanetTile planetTile = new PlanetTile(tileId, world.grid.Surface);
                    if (world.worldObjects.AnyWorldObjectAt(planetTile)) continue;

                    Vector2 center = world.grid.Surface.LongLatOf(tileId);
                    double distance = HistoricalCoverageCalculator.AngularDistance(
                        gameCoordinate.Longitude, gameCoordinate.Latitude, center.x, center.y);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestTile = tileId;
                }

                if (bestTile < 0 || bestDistance > maximumCoordinateErrorDegrees)
                {
                    error = "Map location '" + location.locationId + "' at "
                        + location.longitude.ToString("0.##", CultureInfo.InvariantCulture) + "°, "
                        + location.latitude.ToString("0.##", CultureInfo.InvariantCulture)
                        + " (projected to " + gameCoordinate.Longitude.ToString("0.##", CultureInfo.InvariantCulture) + "°, "
                        + gameCoordinate.Latitude.ToString("0.##", CultureInfo.InvariantCulture) + ")"
                        + "° has no free buildable tile within " + maximumCoordinateErrorDegrees + "°.";
                    return false;
                }

                WorldObject worldObject = WorldObjectMaker.MakeWorldObject(location.worldObjectDef);
                HistoricalMapLocationWorldObject marker = worldObject as HistoricalMapLocationWorldObject;
                if (marker == null)
                {
                    error = "WorldObjectDef '" + location.worldObjectDef.defName
                        + "' must create a HistoricalMapLocationWorldObject.";
                    return false;
                }

                marker.SetHistoricalLabel(location.label);
                marker.SetHistoricalDescription(location.description);
                marker.SetHistoricalGeoCoordinates(location.longitude, location.latitude,
                    gameCoordinate.Longitude, gameCoordinate.Latitude, transformer.ScaleFactor);
                if (location.factionDef != null)
                {
                    Faction owner = world.factionManager?.AllFactionsListForReading
                        .FirstOrDefault(item => item != null && item.def == location.factionDef);
                    if (owner == null)
                    {
                        error = "Map location '" + location.locationId + "' refers to faction Def '"
                            + location.factionDef.defName + "', but that faction was not generated in this world.";
                        return false;
                    }
                    marker.SetHistoricalFactionColor(owner.Color);
                }
                marker.Tile = new PlanetTile(bestTile, world.grid.Surface);
                world.worldObjects.Add(marker);
                placedLocations.Add(Tuple.Create(location, bestTile));
                Vector2 placedCenter = world.grid.Surface.LongLatOf(bestTile);
                Log.Message("[Historical RimWorld] Added map location '" + location.locationId + "' ("
                    + location.label + ") at WorldGrid tile " + bestTile + " ("
                    + placedCenter.x.ToString("0.##", CultureInfo.InvariantCulture) + "°, "
                    + placedCenter.y.ToString("0.##", CultureInfo.InvariantCulture) + "°), icon Def "
                    + location.worldObjectDef.defName
                    + (location.factionDef == null ? "." : ", faction " + location.factionDef.defName + "."));
            }

            LogPairwiseTileDistances(world, placedLocations, transformer);
            return true;
        }

        private static void LogPairwiseTileDistances(RimWorldWorld world,
            IEnumerable<Tuple<HistoricalWorldMapLocation, int>> placements, HistoricalMapScaleTransformer transformer)
        {
            List<Tuple<HistoricalWorldMapLocation, int>> locations = placements
                .Where(item => item.Item1.factionDef != null).ToList();
            if (locations.Count < 2) return;
            for (int secondIndex = 1; secondIndex < locations.Count; secondIndex++)
            {
                HistoricalWorldMapLocation first = locations[0].Item1;
                HistoricalWorldMapLocation second = locations[secondIndex].Item1;
                HistoricalGeoCoordinate firstHistorical = new HistoricalGeoCoordinate(first.latitude, first.longitude);
                HistoricalGeoCoordinate secondHistorical = new HistoricalGeoCoordinate(second.latitude, second.longitude);
                HistoricalGameGeoCoordinate firstGame = transformer.ToGameCoordinate(firstHistorical);
                HistoricalGameGeoCoordinate secondGame = transformer.ToGameCoordinate(secondHistorical);
                float tileDistance = world.grid.ApproxDistanceInTiles(
                    new PlanetTile(locations[0].Item2, world.grid.Surface),
                    new PlanetTile(locations[secondIndex].Item2, world.grid.Surface));
                Log.Message("[Historical RimWorld] Map scale diagnostic: " + first.locationId + " to " + second.locationId
                    + "; historical great-circle distance "
                    + HistoricalGeographicDistanceUtility.GetGreatCircleDistanceKm(firstHistorical, secondHistorical)
                        .ToString("0", CultureInfo.InvariantCulture) + " km; map scale "
                    + transformer.ScaleFactor.ToString("0.##", CultureInfo.InvariantCulture) + "x; game-sphere separation "
                    + HistoricalGeographicDistanceUtility.GetGameWorldDistanceDegrees(firstGame, secondGame)
                        .ToString("0.##", CultureInfo.InvariantCulture) + "°; tile-grid distance "
                    + tileDistance + " tiles (geometric estimate, not a pathfinder route).");
            }
        }
    }
}
