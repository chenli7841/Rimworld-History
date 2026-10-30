using System;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.World;
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
        public static bool TryPlace(RimWorldWorld world, HistoricalWorldMapDef map,
            float maximumCoordinateErrorDegrees, out string error)
        {
            error = null;
            if (map?.locations == null || map.locations.Count == 0) return true;
            if (world?.grid?.Surface == null || world.worldObjects == null)
            {
                error = "World grid or world object holder is unavailable for map locations.";
                return false;
            }

            foreach (HistoricalWorldMapLocation location in map.locations)
            {
                if (location == null || location.worldObjectDef == null)
                {
                    error = "A map location is missing its data or WorldObjectDef.";
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
                        location.longitude, location.latitude, center.x, center.y);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestTile = tileId;
                }

                if (bestTile < 0 || bestDistance > maximumCoordinateErrorDegrees)
                {
                    error = "Map location '" + location.locationId + "' at "
                        + location.longitude.ToString("0.##", CultureInfo.InvariantCulture) + "°, "
                        + location.latitude.ToString("0.##", CultureInfo.InvariantCulture)
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
                Vector2 placedCenter = world.grid.Surface.LongLatOf(bestTile);
                Log.Message("[Historical RimWorld] Added map location '" + location.locationId + "' ("
                    + location.label + ") at WorldGrid tile " + bestTile + " ("
                    + placedCenter.x.ToString("0.##", CultureInfo.InvariantCulture) + "°, "
                    + placedCenter.y.ToString("0.##", CultureInfo.InvariantCulture) + "°), icon Def "
                    + location.worldObjectDef.defName
                    + (location.factionDef == null ? "." : ", faction " + location.factionDef.defName + "."));
            }

            return true;
        }
    }
}
