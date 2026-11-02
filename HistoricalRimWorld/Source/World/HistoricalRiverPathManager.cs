using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.WorldGeneration;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    public sealed class HistoricalRiverPathPlan
    {
        public RiverDef River { get; }
        public IReadOnlyList<HistoricalGeoCoordinate> Waypoints { get; }
        public IReadOnlyList<int> TileIds { get; }
        public float MaximumSnapErrorDegrees { get; }

        internal HistoricalRiverPathPlan(RiverDef river, List<HistoricalGeoCoordinate> waypoints,
            List<int> tileIds, float maximumSnapErrorDegrees)
        {
            River = river;
            Waypoints = waypoints.AsReadOnly();
            TileIds = tileIds.AsReadOnly();
            MaximumSnapErrorDegrees = maximumSnapErrorDegrees;
        }
    }

    /// <summary>Projects user-entered historical coordinates and lays river links along adjacent WorldGrid tiles.</summary>
    public static class HistoricalRiverPathManager
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private const int MaximumWaypoints = 512;

        public static bool TryBuildPlanFromText(string text, RiverDef river,
            out HistoricalRiverPathPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (river == null) { error = "Select a RimWorld river type first."; return false; }
            if (!TryGetContext(out RimWorldWorld world, out HistoricalMapScaleTransformer transformer,
                out float maximumSnapError, out error)) return false;
            if (!TryParseCoordinates(text, out List<HistoricalGeoCoordinate> coordinates, out error)) return false;
            return TryBuildPlan(world, transformer, maximumSnapError, river, coordinates, out plan, out error);
        }

        internal static bool TryBuildPlan(RimWorldWorld world, HistoricalMapScaleTransformer transformer,
            float maximumSnapError, RiverDef river, IList<HistoricalGeoCoordinate> coordinates,
            out HistoricalRiverPathPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (world?.grid?.Surface == null || transformer == null || river == null)
            { error = "World grid, historical projection, or river definition is unavailable."; return false; }
            if (coordinates == null || coordinates.Count < 2 || coordinates.Count > MaximumWaypoints)
            { error = "Enter between 2 and " + MaximumWaypoints + " historical coordinate points."; return false; }

            Vector3[] tileCenters = new Vector3[world.grid.TilesCount];
            for (int tileId = 0; tileId < tileCenters.Length; tileId++)
            {
                Vector2 center = world.grid.Surface.LongLatOf(tileId);
                tileCenters[tileId] = HistoricalCoverageCalculator.ToUnitVector(center.x, center.y);
            }

            List<int> waypointTiles = new List<int>();
            float worstSnapError = 0f;
            foreach (HistoricalGeoCoordinate coordinate in coordinates)
            {
                HistoricalGameGeoCoordinate projected;
                try { projected = transformer.ToGameCoordinate(coordinate); }
                catch (Exception exception)
                { error = "Could not project historical coordinate " + Format(coordinate) + ": " + exception.Message; return false; }

                Vector3 target = HistoricalCoverageCalculator.ToUnitVector(projected.Longitude, projected.Latitude);
                int nearestId = -1;
                float nearestSquared = float.PositiveInfinity;
                for (int tileId = 0; tileId < tileCenters.Length; tileId++)
                {
                    Vector3 difference = tileCenters[tileId] - target;
                    float squared = difference.sqrMagnitude;
                    if (squared >= nearestSquared) continue;
                    nearestSquared = squared;
                    nearestId = tileId;
                }
                if (nearestId < 0)
                { error = "No surface tile is available for historical coordinate " + Format(coordinate) + "."; return false; }
                Vector2 resolved = world.grid.Surface.LongLatOf(nearestId);
                double snapError = HistoricalCoverageCalculator.AngularDistance(projected.Longitude,
                    projected.Latitude, resolved.x, resolved.y);
                worstSnapError = Math.Max(worstSnapError, (float)snapError);
                if (snapError > maximumSnapError)
                {
                    error = "Historical coordinate " + Format(coordinate) + " resolves "
                        + snapError.ToString("0.##", Invariant) + "° from the nearest tile center, exceeding the "
                        + maximumSnapError.ToString("0.##", Invariant) + "° map limit.";
                    return false;
                }
                if (waypointTiles.Count == 0 || waypointTiles[waypointTiles.Count - 1] != nearestId)
                    waypointTiles.Add(nearestId);
            }
            if (waypointTiles.Count < 2)
            { error = "The supplied points all resolve to the same WorldGrid tile. Add farther-apart river points."; return false; }

            List<int> route = new List<int> { waypointTiles[0] };
            HashSet<int> routeTiles = new HashSet<int> { waypointTiles[0] };
            for (int pointIndex = 1; pointIndex < waypointTiles.Count; pointIndex++)
            {
                if (!TryFindShortestTilePath(world.grid, waypointTiles[pointIndex - 1],
                    waypointTiles[pointIndex], out List<int> segment))
                { error = "No adjacent WorldGrid path connects waypoint tiles " + waypointTiles[pointIndex - 1]
                    + " and " + waypointTiles[pointIndex] + "."; return false; }
                foreach (int tileId in segment.Skip(1))
                {
                    if (!routeTiles.Add(tileId))
                    { error = "The supplied waypoint order creates a loop at tile " + tileId + ". Reorder or remove a point."; return false; }
                    route.Add(tileId);
                }
            }

            plan = new HistoricalRiverPathPlan(river, coordinates.ToList(), route, worstSnapError);
            return true;
        }

        public static bool TryCreateEditCommand(HistoricalRiverPathPlan plan,
            out IMapEditCommand command, out string error)
        {
            command = null;
            error = null;
            if (plan == null) { error = "Build a valid river route before applying it."; return false; }
            if (!TryGetContext(out RimWorldWorld world, out _, out _, out error)) return false;
            HistoricalMapEditWorldComponent edits = world.GetComponent<HistoricalMapEditWorldComponent>();
            if (edits == null) { error = "HistoricalMapEditWorldComponent is unavailable."; return false; }

            HistoricalMapRiverPath data = new HistoricalMapRiverPath
            {
                Id = Guid.NewGuid().ToString("N"),
                RiverDefName = plan.River.defName,
                Points = plan.Waypoints.Select(point => new HistoricalMapRiverPoint(point.Longitude, point.Latitude)).ToList()
            };
            command = new RiverPathEditCommand(world, edits, plan, data);
            return true;
        }

        internal static void ApplyPlan(RimWorldWorld world, HistoricalRiverPathPlan plan)
        {
            for (int i = 1; i < plan.TileIds.Count; i++)
                world.grid.OverlayRiver(new PlanetTile(plan.TileIds[i - 1], world.grid.Surface),
                    new PlanetTile(plan.TileIds[i], world.grid.Surface), plan.River);
            world.pathGrid?.RecalculateAllLayersPathCosts();
            world.renderer?.SetAllLayersDirty();
        }

        internal static bool TryParseCoordinates(string text, out List<HistoricalGeoCoordinate> coordinates,
            out string error)
        {
            coordinates = new List<HistoricalGeoCoordinate>();
            error = null;
            string[] lines = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                string[] parts = line.Split(',');
                if (parts.Length != 2
                    || !double.TryParse(parts[0].Trim(), NumberStyles.Float, Invariant, out double longitude)
                    || !double.TryParse(parts[1].Trim(), NumberStyles.Float, Invariant, out double latitude))
                {
                    error = "Line " + (i + 1) + " must use historical longitude,latitude format, for example 113.2,34.7.";
                    return false;
                }
                try { coordinates.Add(new HistoricalGeoCoordinate(latitude, longitude)); }
                catch (ArgumentOutOfRangeException)
                { error = "Line " + (i + 1) + " has longitude or latitude outside the valid historical range."; return false; }
                if (coordinates.Count > MaximumWaypoints)
                { error = "The river route exceeds the " + MaximumWaypoints + " waypoint limit."; return false; }
            }
            if (coordinates.Count < 2)
            { error = "Enter at least two historical coordinate lines."; return false; }
            return true;
        }

        private static bool TryGetContext(out RimWorldWorld world,
            out HistoricalMapScaleTransformer transformer, out float maximumSnapError, out string error)
        {
            world = Find.World;
            transformer = null;
            maximumSnapError = 5f;
            error = null;
            if (!HistoricalMapEditManager.CanEdit(world, out error)) return false;
            HistoricalFactionWorldComponent factionState = world.GetComponent<HistoricalFactionWorldComponent>();
            HistoricalMapScaleDef scale = DefDatabase<HistoricalMapScaleDef>
                .GetNamedSilentFail(factionState.MapScaleState.ScaleDefName);
            try { transformer = new HistoricalMapScaleTransformer(scale); }
            catch (Exception exception)
            { error = "Historical projection could not be loaded: " + exception.Message; return false; }

            HistoricalScenarioDef scenario = DefDatabase<HistoricalScenarioDef>.GetNamedSilentFail(factionState.ScenarioDefName);
            if (scenario != null && HistoricalWorldGenerationController.TryGetWorldGenerationDef(scenario,
                out HistoricalWorldGenerationDef generation))
                maximumSnapError = generation.maximumTileCenterErrorDegrees;
            return true;
        }

        private static bool TryFindShortestTilePath(WorldGrid grid, int start, int end, out List<int> path)
        {
            path = null;
            if (start == end) { path = new List<int> { start }; return true; }
            Queue<int> queue = new Queue<int>();
            Dictionary<int, int> previous = new Dictionary<int, int> { [start] = start };
            List<PlanetTile> neighbors = new List<PlanetTile>(8);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                neighbors.Clear();
                grid.GetTileNeighbors(new PlanetTile(current, grid.Surface), neighbors);
                foreach (PlanetTile neighbor in neighbors)
                {
                    int next = (int)neighbor;
                    if (previous.ContainsKey(next)) continue;
                    previous[next] = current;
                    if (next == end)
                    {
                        List<int> reversed = new List<int> { end };
                        int cursor = end;
                        while (cursor != start)
                        { cursor = previous[cursor]; reversed.Add(cursor); }
                        reversed.Reverse();
                        path = reversed;
                        return true;
                    }
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private static string Format(HistoricalGeoCoordinate coordinate) =>
            coordinate.Longitude.ToString("0.#####", Invariant) + "°, "
            + coordinate.Latitude.ToString("0.#####", Invariant) + "°";

        private sealed class RiverPathEditCommand : IMapEditCommand
        {
            private readonly RimWorldWorld world;
            private readonly HistoricalMapEditWorldComponent edits;
            private readonly HistoricalRiverPathPlan plan;
            private readonly HistoricalMapRiverPath data;
            private readonly Dictionary<int, TileRiverSnapshot> before = new Dictionary<int, TileRiverSnapshot>();
            public string Label { get; }

            public RiverPathEditCommand(RimWorldWorld world, HistoricalMapEditWorldComponent edits,
                HistoricalRiverPathPlan plan, HistoricalMapRiverPath data)
            {
                this.world = world;
                this.edits = edits;
                this.plan = plan;
                this.data = data;
                Label = "Draw " + plan.River.LabelCap + " across " + plan.TileIds.Count + " tiles";
                foreach (int tileId in plan.TileIds)
                {
                    SurfaceTile tile = world.grid.Surface[tileId];
                    before[tileId] = new TileRiverSnapshot(tileId, tile.potentialRivers, tile.riverDist);
                }
            }

            public void Execute()
            {
                try
                {
                    HistoricalRiverPathManager.ApplyPlan(world, plan);
                    edits.AddRiverPath(data);
                }
                catch
                {
                    RestoreBefore();
                    edits.RemoveRiverPath(data.Id);
                    world.pathGrid?.RecalculateAllLayersPathCosts();
                    world.renderer?.SetAllLayersDirty();
                    throw;
                }
            }

            public void Undo()
            {
                RestoreBefore();
                edits.RemoveRiverPath(data.Id);
                world.pathGrid?.RecalculateAllLayersPathCosts();
                world.renderer?.SetAllLayersDirty();
            }

            private void RestoreBefore()
            {
                foreach (TileRiverSnapshot snapshot in before.Values)
                {
                    SurfaceTile tile = world.grid.Surface[snapshot.TileId];
                    tile.potentialRivers = snapshot.Rivers == null
                        ? null : new List<SurfaceTile.RiverLink>(snapshot.Rivers);
                    tile.riverDist = snapshot.RiverDistance;
                }
            }
        }

        private sealed class TileRiverSnapshot
        {
            public readonly int TileId;
            public readonly List<SurfaceTile.RiverLink> Rivers;
            public readonly int RiverDistance;
            public TileRiverSnapshot(int tileId, List<SurfaceTile.RiverLink> rivers, int riverDistance)
            { TileId = tileId; Rivers = rivers == null ? null : new List<SurfaceTile.RiverLink>(rivers); RiverDistance = riverDistance; }
        }
    }
}
