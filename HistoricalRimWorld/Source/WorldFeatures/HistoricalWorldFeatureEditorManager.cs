using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.World;
using RimWorld;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldFeatures
{
    /// <summary>World-editor operations that mutate only RimWorld's native WorldFeature objects.</summary>
    public static class HistoricalWorldFeatureEditorManager
    {
        public static bool TryCreateFeatureCommand(RimWorldWorld world, string name, FeatureDef featureDef,
            IEnumerable<int> requestedTileIds, out IMapEditCommand command, out string error)
        {
            command = null;
            error = null;
            List<int> tileIds = (requestedTileIds ?? Enumerable.Empty<int>()).Distinct().OrderBy(id => id).ToList();
            if (world?.grid?.Surface == null || world.features?.features == null)
            { error = "World or native WorldFeatures collection is unavailable."; return false; }
            if (string.IsNullOrWhiteSpace(name)) { error = "Feature name cannot be empty."; return false; }
            if (featureDef == null || DefDatabase<FeatureDef>.GetNamedSilentFail(featureDef.defName) != featureDef)
            { error = "Select a valid native RimWorld FeatureDef."; return false; }
            if (tileIds.Count == 0) { error = "Add at least one selected WorldGrid tile before creating a feature."; return false; }
            if (tileIds.Any(id => id < 0 || id >= world.grid.Surface.TilesCount))
            { error = "The staged tile list contains a tile outside the active WorldGrid."; return false; }
            if (tileIds.Any(id => world.grid.Surface.Tiles[id] == null))
            { error = "One of the selected tiles is unavailable."; return false; }

            WorldFeature feature;
            try { feature = new WorldFeature(featureDef, world.grid.Surface); }
            catch (Exception exception) { error = "RimWorld could not allocate a native WorldFeature ID: " + exception.Message; return false; }
            if (feature.uniqueID < 0 || feature.uniqueID >= ushort.MaxValue)
            { error = "RimWorld assigned an ID that cannot fit the native tile-feature save field."; return false; }
            feature.name = name.Trim();

            Snapshot before = Snapshot.Capture(world, tileIds, new[] { feature });
            world.features.features.Add(feature);
            foreach (int tileId in tileIds)
            {
                if (!WorldFeatureAdapter.TrySetTileFeature(world, tileId, feature))
                { before.Apply(world); error = "Could not update the native feature mapping for tile " + tileId + "."; return false; }
            }
            WorldFeatureAdapter.RecalculateFeatureGeometry(world, feature);
            Snapshot after = Snapshot.Capture(world, tileIds, new[] { feature });
            before.Apply(world);
            command = new SnapshotEditCommand(world, "Create World Feature: " + feature.name, before, after);
            return true;
        }

        public static bool TryCreateMembershipCommand(RimWorldWorld world, WorldFeature target,
            int selectedTileId, bool addTile, out IMapEditCommand command, out string error)
        {
            command = null;
            error = null;
            if (world?.grid?.Surface == null || world.features?.features == null || target == null)
            { error = "Select an existing native WorldFeature first."; return false; }
            if (!world.features.features.Contains(target))
            { error = "The selected feature is no longer in the active world."; return false; }
            if (selectedTileId < 0 || selectedTileId >= world.grid.Surface.TilesCount
                || world.grid.Surface.Tiles[selectedTileId] == null)
            { error = "Select a valid WorldGrid tile first."; return false; }

            WorldFeature previous = world.grid.Surface.Tiles[selectedTileId].feature;
            if (addTile && ReferenceEquals(previous, target))
            { error = "The selected tile already belongs to this feature."; return false; }
            if (!addTile && !ReferenceEquals(previous, target))
            { error = "The selected tile is not a member of this feature."; return false; }

            WorldFeature[] changedFeatures = new[] { target, previous }.Where(item => item != null).Distinct().ToArray();
            Snapshot before = Snapshot.Capture(world, new[] { selectedTileId }, changedFeatures);
            if (!WorldFeatureAdapter.TrySetTileFeature(world, selectedTileId, addTile ? target : null))
            { error = "Could not update the native feature mapping for the selected tile."; return false; }

            foreach (WorldFeature changed in changedFeatures)
            {
                List<int> members = WorldFeatureAdapter.GetFeatureTiles(world, changed).ToList();
                if (members.Count == 0) world.features.features.Remove(changed);
                else WorldFeatureAdapter.RecalculateFeatureGeometry(world, changed);
            }
            Snapshot after = Snapshot.Capture(world, new[] { selectedTileId }, changedFeatures);
            before.Apply(world);
            string label = (addTile ? "Add tile to " : "Remove tile from ") + target.name;
            command = new SnapshotEditCommand(world, label, before, after);
            return true;
        }

        public static bool TryCreateMembershipBatchCommand(RimWorldWorld world, WorldFeature target,
            IEnumerable<int> requestedTileIds, bool addTiles, out IMapEditCommand command,
            out int changedCount, out string error)
        {
            command = null;
            changedCount = 0;
            error = null;
            if (world?.grid?.Surface == null || world.features?.features == null || target == null
                || !world.features.features.Contains(target))
            { error = "Select an existing native WorldFeature first."; return false; }

            List<int> tileIds = (requestedTileIds ?? Enumerable.Empty<int>()).Distinct().OrderBy(id => id).ToList();
            if (tileIds.Count == 0) { error = "The selected area contains no tiles."; return false; }
            if (tileIds.Any(id => id < 0 || id >= world.grid.Surface.TilesCount
                || world.grid.Surface.Tiles[id] == null))
            { error = "The selected area contains a tile outside the active WorldGrid."; return false; }

            List<int> changedTileIds = tileIds.Where(id => addTiles
                ? !ReferenceEquals(world.grid.Surface.Tiles[id].feature, target)
                : ReferenceEquals(world.grid.Surface.Tiles[id].feature, target)).ToList();
            if (changedTileIds.Count == 0)
            { error = "All tiles in the selected area already belong to this feature."; return false; }

            WorldFeature[] changedFeatures = changedTileIds
                .Select(id => world.grid.Surface.Tiles[id].feature)
                .Where(feature => feature != null).Append(target).Distinct().ToArray();
            Snapshot before = Snapshot.Capture(world, changedTileIds, changedFeatures);
            foreach (int tileId in changedTileIds)
            {
                if (addTiles)
                {
                    if (!WorldFeatureAdapter.TrySetTileFeature(world, tileId, target))
                    { before.Apply(world); error = "Could not assign tile " + tileId + " to the feature; changes were rolled back."; return false; }
                }
                else if (ReferenceEquals(world.grid.Surface.Tiles[tileId].feature, target)
                    && !WorldFeatureAdapter.TrySetTileFeature(world, tileId, null))
                { before.Apply(world); error = "Could not remove tile " + tileId + " from the feature; changes were rolled back."; return false; }
            }

            foreach (WorldFeature changed in changedFeatures)
            {
                int memberCount = WorldFeatureAdapter.GetFeatureTiles(world, changed).Count;
                if (memberCount == 0) world.features.features.Remove(changed);
                else WorldFeatureAdapter.RecalculateFeatureGeometry(world, changed);
            }
            Snapshot after = Snapshot.Capture(world, changedTileIds, changedFeatures);
            before.Apply(world);
            changedCount = changedTileIds.Count;
            command = new SnapshotEditCommand(world,
                (addTiles ? "Add " : "Remove ") + changedCount + " tiles " + (addTiles ? "to " : "from ") + target.name,
                before, after);
            return true;
        }

        private sealed class Snapshot
        {
            private readonly List<WorldFeature> features;
            private readonly Dictionary<int, WorldFeature> tileAssignments;
            private readonly Dictionary<WorldFeature, FeatureGeometry> geometries;

            private Snapshot(List<WorldFeature> features, Dictionary<int, WorldFeature> tileAssignments,
                Dictionary<WorldFeature, FeatureGeometry> geometries)
            { this.features = features; this.tileAssignments = tileAssignments; this.geometries = geometries; }

            public static Snapshot Capture(RimWorldWorld world, IEnumerable<int> tileIds,
                IEnumerable<WorldFeature> relatedFeatures)
            {
                Dictionary<int, WorldFeature> assignments = tileIds.Distinct().ToDictionary(id => id,
                    id => world.grid.Surface.Tiles[id]?.feature);
                HashSet<WorldFeature> involved = new HashSet<WorldFeature>(relatedFeatures.Where(item => item != null));
                foreach (WorldFeature assigned in assignments.Values)
                    if (assigned != null) involved.Add(assigned);
                Dictionary<WorldFeature, FeatureGeometry> geometry = involved.ToDictionary(item => item,
                    item => new FeatureGeometry(item.drawCenter, item.drawAngle, item.maxDrawSizeInTiles));
                return new Snapshot(world.features.features.ToList(), assignments, geometry);
            }

            public void Apply(RimWorldWorld world)
            {
                world.features.features.Clear();
                world.features.features.AddRange(features);
                foreach (KeyValuePair<int, WorldFeature> pair in tileAssignments)
                    WorldFeatureAdapter.TrySetTileFeature(world, pair.Key, pair.Value);
                foreach (KeyValuePair<WorldFeature, FeatureGeometry> pair in geometries)
                {
                    pair.Key.drawCenter = pair.Value.drawCenter;
                    pair.Key.drawAngle = pair.Value.drawAngle;
                    pair.Key.maxDrawSizeInTiles = pair.Value.maxDrawSizeInTiles;
                }
                WorldFeatureAdapter.InvalidateTileCache();
                try { WorldFeatureAdapter.RefreshFeatureLabels(world); }
                catch (Exception exception)
                { Log.Warning("[Historical RimWorld] Could not refresh native feature labels after editor operation: " + exception.Message); }
                HistoricalWorldTerrainColorManager.Invalidate(world, true);
            }
        }

        private struct FeatureGeometry
        {
            public readonly UnityEngine.Vector3 drawCenter;
            public readonly float drawAngle;
            public readonly float maxDrawSizeInTiles;
            public FeatureGeometry(UnityEngine.Vector3 center, float angle, float size)
            { drawCenter = center; drawAngle = angle; maxDrawSizeInTiles = size; }
        }

        private sealed class SnapshotEditCommand : IMapEditCommand
        {
            private readonly RimWorldWorld world;
            private readonly Snapshot before, after;
            public string Label { get; }
            public SnapshotEditCommand(RimWorldWorld world, string label, Snapshot before, Snapshot after)
            { this.world = world; Label = label; this.before = before; this.after = after; }
            public void Execute() => after.Apply(world);
            public void Undo() => before.Apply(world);
        }
    }
}
