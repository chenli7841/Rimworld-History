using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using RimWorld;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    public sealed class HistoricalRiverWorldComponent : WorldComponent
    {
        private const int CurrentDataVersion = 1;
        private int dataVersion;
        private int gridTileCount;
        private string scenarioDefName;
        private List<HistoricalRiverGeometry> geometries = new List<HistoricalRiverGeometry>();
        private readonly Dictionary<int, List<string>> riverIdsByTile = new Dictionary<int, List<string>>();
        private bool indexReady;

        public IEnumerable<HistoricalRiverGeometry> Geometries => geometries ?? Enumerable.Empty<HistoricalRiverGeometry>();

        public HistoricalRiverWorldComponent(RimWorldWorld world) : base(world) { }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            RebuildFromSavedMapPaths();
        }

        public HistoricalRiverGeometry GetGeometry(string riverDefName)
        {
            EnsureIndex();
            return geometries?.FirstOrDefault(item => item != null && item.RiverDefName == riverDefName);
        }

        public IReadOnlyList<HistoricalRiverDef> GetRiversAtTile(int tileId)
        {
            EnsureIndex();
            if (!riverIdsByTile.TryGetValue(tileId, out List<string> ids)) return Array.Empty<HistoricalRiverDef>();
            return ids.Select(id => DefDatabase<HistoricalRiverDef>.GetNamedSilentFail(id))
                .Where(item => item != null).ToList().AsReadOnly();
        }

        public void RebuildFromSavedMapPaths()
        {
            if (world?.grid?.Surface == null) return;
            HistoricalMapEditWorldComponent edits = world.GetComponent<HistoricalMapEditWorldComponent>();
            List<HistoricalMapRiverPath> paths = edits?.RiverPaths
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.HistoricalRiverDefName))
                .ToList() ?? new List<HistoricalMapRiverPath>();
            if (paths.Count == 0)
            {
                gridTileCount = world.grid.TilesCount;
                scenarioDefName = world.GetComponent<HistoricalFactionWorldComponent>()?.ScenarioDefName;
                RebuildTileIndex();
                return;
            }

            HistoricalFactionWorldComponent factionState = world.GetComponent<HistoricalFactionWorldComponent>();
            HistoricalMapScaleDef scale = DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(
                factionState?.MapScaleState?.ScaleDefName);
            HistoricalMapScaleTransformer transformer = null;
            try { if (scale != null) transformer = new HistoricalMapScaleTransformer(scale); }
            catch (Exception exception)
            { Log.Warning("[HR-017] River registry could not load the historical projection: " + exception.Message); }

            Dictionary<string, HistoricalRiverGeometry> rebuilt = new Dictionary<string, HistoricalRiverGeometry>(StringComparer.Ordinal);
            foreach (HistoricalMapRiverPath path in paths)
            {
                HistoricalRiverDef historicalRiver = DefDatabase<HistoricalRiverDef>.GetNamedSilentFail(path.HistoricalRiverDefName);
                RiverDef vanillaRiver = DefDatabase<RiverDef>.GetNamedSilentFail(path.RiverDefName);
                if (historicalRiver == null || vanillaRiver == null || transformer == null || path.Points == null)
                {
                    Log.Warning("[HR-017] Skipped saved path " + path.Id + " because its historical river, vanilla river, or projection is missing.");
                    continue;
                }
                List<HistoricalGeoCoordinate> points = new List<HistoricalGeoCoordinate>();
                try
                {
                    foreach (HistoricalMapRiverPoint point in path.Points)
                        points.Add(new HistoricalGeoCoordinate(point.Latitude, point.Longitude));
                }
                catch (ArgumentOutOfRangeException exception)
                { Log.Warning("[HR-017] Skipped invalid saved river path " + path.Id + ": " + exception.Message); continue; }

                if (!HistoricalRiverPathManager.TryBuildPlan(world, transformer, 5f, vanillaRiver, points,
                    out HistoricalRiverPathPlan plan, out string error, historicalRiver))
                {
                    Log.Warning("[HR-017] Could not rebuild historical river path " + path.Id + ": " + error);
                    continue;
                }
                if (!rebuilt.TryGetValue(historicalRiver.defName, out HistoricalRiverGeometry geometry))
                {
                    geometry = new HistoricalRiverGeometry { RiverDefName = historicalRiver.defName };
                    rebuilt.Add(historicalRiver.defName, geometry);
                }
                HistoricalRiverSegment segment = new HistoricalRiverSegment
                { SegmentId = path.Id, PathTiles = plan.TileIds.Distinct().ToList() };
                geometry.Segments.Add(segment);
                if (segment.PathTiles.Count > 0)
                    geometry.LabelAnchors.Add(new RiverLabelAnchor
                    { TileId = segment.PathTiles[segment.PathTiles.Count / 2], PathIndex = segment.PathTiles.Count / 2 });
            }

            if (rebuilt.Count > 0 || geometries == null || geometries.Count == 0)
                geometries = rebuilt.Values.OrderBy(item => item.RiverDefName, StringComparer.Ordinal).ToList();
            gridTileCount = world.grid.TilesCount;
            scenarioDefName = factionState?.ScenarioDefName;
            dataVersion = CurrentDataVersion;
            RebuildTileIndex();
            Log.Message("[HR-017] Registered " + geometries.Count + " historical river(s) across "
                + riverIdsByTile.Count + " river tile(s).");
        }

        private void EnsureIndex()
        {
            if (!indexReady || world?.grid == null || gridTileCount != world.grid.TilesCount)
                RebuildTileIndex();
        }

        private void RebuildTileIndex()
        {
            riverIdsByTile.Clear();
            int tileCount = world?.grid?.TilesCount ?? 0;
            foreach (HistoricalRiverGeometry geometry in geometries ?? Enumerable.Empty<HistoricalRiverGeometry>())
            {
                if (geometry == null || DefDatabase<HistoricalRiverDef>.GetNamedSilentFail(geometry.RiverDefName) == null) continue;
                foreach (HistoricalRiverSegment segment in geometry.Segments ?? Enumerable.Empty<HistoricalRiverSegment>())
                {
                    foreach (int tileId in segment?.PathTiles ?? Enumerable.Empty<int>())
                    {
                        if (tileId < 0 || tileId >= tileCount) continue;
                        if (!riverIdsByTile.TryGetValue(tileId, out List<string> ids))
                        { ids = new List<string>(); riverIdsByTile.Add(tileId, ids); }
                        if (!ids.Contains(geometry.RiverDefName)) ids.Add(geometry.RiverDefName);
                    }
                }
            }
            gridTileCount = tileCount;
            indexReady = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dataVersion, "historicalRiverDataVersion", 0);
            Scribe_Values.Look(ref gridTileCount, "historicalRiverGridTileCount", 0);
            Scribe_Values.Look(ref scenarioDefName, "historicalRiverScenarioDefName");
            Scribe_Collections.Look(ref geometries, "historicalRiverGeometries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                geometries = geometries ?? new List<HistoricalRiverGeometry>();
                geometries.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.RiverDefName));
                if (dataVersion > CurrentDataVersion || (gridTileCount > 0 && world?.grid != null
                    && gridTileCount != world.grid.TilesCount))
                {
                    Log.Warning("[HR-017] Discarded stale derived river geometry because the save data version or grid size changed.");
                    geometries.Clear();
                }
                dataVersion = CurrentDataVersion;
                indexReady = false;
            }
        }
    }

    public static class HistoricalRiverRegistry
    {
        public static HistoricalRiverDef GetRiverAtTile(int tileId) => GetRiversAtTile(tileId).FirstOrDefault();

        public static IReadOnlyList<HistoricalRiverDef> GetRiversAtTile(int tileId) =>
            Find.World?.GetComponent<HistoricalRiverWorldComponent>()?.GetRiversAtTile(tileId)
            ?? Array.Empty<HistoricalRiverDef>();

        public static IReadOnlyList<int> GetRiverPath(string riverDefName)
        {
            HistoricalRiverGeometry geometry = Find.World?.GetComponent<HistoricalRiverWorldComponent>()?.GetGeometry(riverDefName);
            if (geometry == null || geometry.Segments == null) return Array.Empty<int>();
            return geometry.Segments.SelectMany(item => item.PathTiles ?? new List<int>())
                .Distinct().ToList().AsReadOnly();
        }

        public static HistoricalRiverGeometry GetGeometry(string riverDefName) =>
            Find.World?.GetComponent<HistoricalRiverWorldComponent>()?.GetGeometry(riverDefName);

        public static string GetDisplayName(string riverDefName, int historicalYear)
        {
            HistoricalRiverDef river = DefDatabase<HistoricalRiverDef>.GetNamedSilentFail(riverDefName);
            if (river == null) return null;
            HistoricalRiverNamePeriod period = (river.namePeriods ?? new List<HistoricalRiverNamePeriod>())
                .FirstOrDefault(item => item != null && historicalYear >= item.startYear && historicalYear <= item.endYear);
            return string.IsNullOrWhiteSpace(period?.nameKey) ? river.LabelCap : period.nameKey.Translate().ToString();
        }

        public static IReadOnlyList<string> GetAlternateNames(string riverDefName)
        {
            HistoricalRiverDef river = DefDatabase<HistoricalRiverDef>.GetNamedSilentFail(riverDefName);
            return (river?.alternateNames ?? new List<string>()).Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Translate().ToString()).ToList().AsReadOnly();
        }

        public static void Rebuild(RimWorldWorld world) => world?.GetComponent<HistoricalRiverWorldComponent>()?.RebuildFromSavedMapPaths();
    }
}
