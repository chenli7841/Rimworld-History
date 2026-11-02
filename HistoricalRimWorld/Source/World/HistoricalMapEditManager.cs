using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    public interface IMapEditCommand
    {
        void Execute();
        void Undo();
        string Label { get; }
    }

    public static class HistoricalMapEditManager
    {
        public static bool TryGetSelectedTileId(out int tileId)
        {
            tileId = -1;
            if (Find.WorldInterface != null && Find.WorldInterface.SelectedTile.Valid)
            {
                tileId = (int)Find.WorldInterface.SelectedTile;
                return true;
            }
            if (Find.WorldSelector != null && Find.WorldSelector.SelectedTile.Valid)
            {
                tileId = (int)Find.WorldSelector.SelectedTile;
                return true;
            }
            return false;
        }

        public static bool CanEdit(RimWorldWorld world, out string reason)
        {
            reason = null;
            if (!Prefs.DevMode) { reason = "World Map Editor is available only in Dev Mode."; return false; }
            if (world?.grid?.Surface == null) { reason = "The current world surface is unavailable."; return false; }
            HistoricalFactionWorldComponent factions = world.GetComponent<HistoricalFactionWorldComponent>();
            if (factions == null || factions.GenerationMode != HistoricalFactionGenerationMode.HistoricalScenario
                || string.IsNullOrWhiteSpace(factions.ScenarioDefName))
            { reason = "The current world is not a Historical RimWorld scenario."; return false; }
            HistoricalMapScaleState scale = factions.MapScaleState;
            if (scale == null || DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(scale.ScaleDefName) == null)
            { reason = "The active historical map projection is unavailable."; return false; }
            return true;
        }

        public static bool TryCreateTerrainPaint(Hilliness terrain, int radius,
            out IMapEditCommand command, out string error)
        {
            command = null;
            error = null;
            if (!TryGetContext(out RimWorldWorld world, out HistoricalMapEditWorldComponent edits,
                out HistoricalMapScaleTransformer transformer, out string scenarioId, out error)) return false;
            if (!TryGetSelectedTileId(out int selectedId))
            { error = "Select a WorldGrid tile first."; return false; }
            if (radius < 0 || radius > 4) { error = "Brush radius must be between 0 and 4."; return false; }
            List<int> tileIds = TilesWithinRadius(world.grid, selectedId, radius);
            List<TerrainEditDelta> deltas = new List<TerrainEditDelta>();
            foreach (int tileId in tileIds)
            {
                Tile tile = world.grid.Surface.Tiles[tileId];
                if (tile == null || tile.hilliness == terrain) continue;
                HistoricalMapTerrainOverride previous = edits.GetTerrainOverride(tileId);
                Hilliness baseTerrain = previous == null ? tile.hilliness : previous.BaseTerrain;
                Vector2 coordinate = world.grid.Surface.LongLatOf(tileId);
                HistoricalGeoCoordinate historical;
                try
                {
                    historical = transformer.ToHistoricalCoordinate(
                        new HistoricalGameGeoCoordinate(coordinate.y, coordinate.x));
                }
                catch (Exception exception)
                { error = "Could not inverse-project tile " + tileId + ": " + exception.Message; return false; }
                deltas.Add(new TerrainEditDelta(tileId, historical.Longitude, historical.Latitude,
                    baseTerrain, tile.hilliness, terrain));
            }
            if (deltas.Count == 0) { error = "The selected brush does not change any tiles."; return false; }
            command = new TerrainPaintCommand(world, edits, deltas, "Set " + terrain + " on " + deltas.Count + " tile(s)");
            return true;
        }

        public static bool TryCreateRestore(int radius, out IMapEditCommand command, out string error)
        {
            command = null;
            error = null;
            if (!TryGetContext(out RimWorldWorld world, out HistoricalMapEditWorldComponent edits,
                out _, out string scenarioId, out error)) return false;
            if (!TryGetSelectedTileId(out int selectedId))
            { error = "Select a WorldGrid tile first."; return false; }
            if (radius < 0 || radius > 4) { error = "Brush radius must be between 0 and 4."; return false; }
            List<TerrainEditDelta> deltas = new List<TerrainEditDelta>();
            foreach (int tileId in TilesWithinRadius(world.grid, selectedId, radius))
            {
                HistoricalMapTerrainOverride state = edits.GetTerrainOverride(tileId);
                Tile tile = world.grid.Surface.Tiles[tileId];
                if (state == null || tile == null || tile.hilliness == state.BaseTerrain) continue;
                deltas.Add(new TerrainEditDelta(tileId, state.HistoricalLongitude, state.HistoricalLatitude,
                    state.BaseTerrain, tile.hilliness, state.BaseTerrain));
            }
            if (deltas.Count == 0) { error = "The selected brush contains no manual terrain overrides."; return false; }
            edits.Initialize(scenarioId, edits.ScaleDefName);
            command = new TerrainPaintCommand(world, edits, deltas, "Restore base terrain on " + deltas.Count + " tile(s)");
            return true;
        }

        private static bool TryGetContext(out RimWorldWorld world, out HistoricalMapEditWorldComponent edits,
            out HistoricalMapScaleTransformer transformer, out string scenarioId, out string error)
        {
            world = Find.World;
            edits = null;
            transformer = null;
            scenarioId = null;
            error = null;
            if (!CanEdit(world, out error)) return false;
            HistoricalFactionWorldComponent factionState = world.GetComponent<HistoricalFactionWorldComponent>();
            scenarioId = factionState.ScenarioDefName;
            HistoricalMapScaleDef scaleDef = DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(factionState.MapScaleState.ScaleDefName);
            try { transformer = new HistoricalMapScaleTransformer(scaleDef); }
            catch (Exception exception) { error = "Historical projection could not be loaded: " + exception.Message; return false; }
            edits = world.GetComponent<HistoricalMapEditWorldComponent>();
            if (edits == null) { error = "HistoricalMapEditWorldComponent is unavailable."; return false; }
            edits.Initialize(scenarioId, scaleDef.defName);
            return true;
        }

        private static List<int> TilesWithinRadius(WorldGrid grid, int startTile, int radius)
        {
            HashSet<int> visited = new HashSet<int> { startTile };
            Queue<Tuple<int, int>> frontier = new Queue<Tuple<int, int>>();
            frontier.Enqueue(Tuple.Create(startTile, 0));
            List<PlanetTile> neighbors = new List<PlanetTile>(8);
            while (frontier.Count > 0)
            {
                Tuple<int, int> current = frontier.Dequeue();
                if (current.Item2 >= radius) continue;
                neighbors.Clear();
                grid.GetTileNeighbors(new PlanetTile(current.Item1, grid.Surface), neighbors);
                foreach (PlanetTile neighbor in neighbors)
                {
                    int id = (int)neighbor;
                    if (!visited.Add(id)) continue;
                    frontier.Enqueue(Tuple.Create(id, current.Item2 + 1));
                }
            }
            return visited.OrderBy(id => id).ToList();
        }

        private sealed class TerrainEditDelta
        {
            public readonly int TileId;
            public readonly double Longitude;
            public readonly double Latitude;
            public readonly Hilliness BaseTerrain;
            public readonly Hilliness Before;
            public readonly Hilliness After;

            public TerrainEditDelta(int tileId, double longitude, double latitude, Hilliness baseTerrain,
                Hilliness before, Hilliness after)
            {
                TileId = tileId;
                Longitude = longitude;
                Latitude = latitude;
                BaseTerrain = baseTerrain;
                Before = before;
                After = after;
            }
        }

        private sealed class TerrainPaintCommand : IMapEditCommand
        {
            private readonly RimWorldWorld world;
            private readonly HistoricalMapEditWorldComponent edits;
            private readonly List<TerrainEditDelta> deltas;
            public string Label { get; }

            public TerrainPaintCommand(RimWorldWorld world, HistoricalMapEditWorldComponent edits,
                List<TerrainEditDelta> deltas, string label)
            { this.world = world; this.edits = edits; this.deltas = deltas; Label = label; }

            public void Execute() => Apply(true);
            public void Undo() => Apply(false);

            private void Apply(bool forward)
            {
                foreach (TerrainEditDelta delta in deltas)
                {
                    Hilliness value = forward ? delta.After : delta.Before;
                    world.grid.Surface.Tiles[delta.TileId].hilliness = value;
                    edits.SetTerrain(delta.TileId, delta.Longitude, delta.Latitude, delta.BaseTerrain, value);
                }
                world.pathGrid?.RecalculateAllLayersPathCosts();
                // Tile data is updated immediately, but the world-map mesh is cached. Mark
                // renderer layers dirty so the edited area is redrawn on the next world UI frame.
                world.renderer?.SetAllLayersDirty();
            }
        }
    }
}
