using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.Geography.Terrain;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    /// <summary>Saveable, sparse manual terrain layer for one historical world.</summary>
    public sealed class HistoricalMapEditWorldComponent : WorldComponent
    {
        private string scenarioDefName;
        private string scaleDefName;
        private int gridTileCount;
        private int projectionVersion;
        private int scenarioMapVersion;
        private string terrainDatasetDefName;
        private int terrainDatasetVersion;
        private List<HistoricalMapTerrainOverride> terrainOverrides = new List<HistoricalMapTerrainOverride>();
        private List<HistoricalMapRiverPath> riverPaths = new List<HistoricalMapRiverPath>();

        public string ScenarioDefName => scenarioDefName;
        public string ScaleDefName => scaleDefName;
        public int GridTileCount => gridTileCount;
        public int ProjectionVersion => projectionVersion;
        public int ScenarioMapVersion => scenarioMapVersion;
        public string TerrainDatasetDefName => terrainDatasetDefName;
        public int TerrainDatasetVersion => terrainDatasetVersion;
        public IEnumerable<HistoricalMapTerrainOverride> TerrainOverrides =>
            terrainOverrides ?? Enumerable.Empty<HistoricalMapTerrainOverride>();
        public IEnumerable<HistoricalMapRiverPath> RiverPaths => riverPaths ?? Enumerable.Empty<HistoricalMapRiverPath>();

        public HistoricalMapEditWorldComponent(RimWorldWorld world) : base(world) { }

        public HistoricalMapTerrainOverride GetTerrainOverride(int tileId) =>
            terrainOverrides?.FirstOrDefault(item => item != null && item.TileId == tileId);

        internal void Initialize(string scenarioId, string scaleId)
        {
            if (!string.IsNullOrEmpty(scenarioDefName) && scenarioDefName != scenarioId)
            {
                Log.Warning("[Historical RimWorld] Map edit data belongs to a different scenario; it was preserved.");
                return;
            }
            scenarioDefName = scenarioId;
            scaleDefName = scaleId;
            gridTileCount = world?.grid?.TilesCount ?? 0;
            projectionVersion = HistoricalMapScaleTransformer.CurrentProjectionVersion;
            scenarioMapVersion = world?.GetComponent<HistoricalWorldRevealWorldComponent>()?.MapVersion ?? 0;
            HistoricalTerrainWorldComponent terrain = world?.GetComponent<HistoricalTerrainWorldComponent>();
            terrainDatasetDefName = terrain?.DatasetDefName;
            terrainDatasetVersion = terrain?.DatasetVersion ?? 0;
            terrainOverrides = terrainOverrides ?? new List<HistoricalMapTerrainOverride>();
        }

        internal void SetTerrain(int tileId, double longitude, double latitude, Hilliness baseValue, Hilliness value)
        {
            if (terrainOverrides == null) terrainOverrides = new List<HistoricalMapTerrainOverride>();
            HistoricalMapTerrainOverride state = GetTerrainOverride(tileId);
            if (value == baseValue)
            {
                if (state != null) terrainOverrides.Remove(state);
                return;
            }
            if (state == null)
            {
                state = new HistoricalMapTerrainOverride();
                state.Initialize(tileId, longitude, latitude, baseValue, value);
                terrainOverrides.Add(state);
            }
            else
            {
                state.SetValue(value);
            }
        }

        internal void AddRiverPath(HistoricalMapRiverPath path)
        {
            if (path == null || string.IsNullOrWhiteSpace(path.Id)) return;
            if (riverPaths == null) riverPaths = new List<HistoricalMapRiverPath>();
            riverPaths.RemoveAll(item => item != null && item.Id == path.Id);
            riverPaths.Add(path);
        }

        internal void RemoveRiverPath(string id)
        {
            riverPaths?.RemoveAll(item => item != null && item.Id == id);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalMapEditScenarioDefName");
            Scribe_Values.Look(ref scaleDefName, "historicalMapEditScaleDefName");
            Scribe_Values.Look(ref gridTileCount, "historicalMapEditGridTileCount", 0);
            Scribe_Values.Look(ref projectionVersion, "historicalMapEditProjectionVersion", 0);
            Scribe_Values.Look(ref scenarioMapVersion, "historicalMapEditScenarioMapVersion", 0);
            Scribe_Values.Look(ref terrainDatasetDefName, "historicalMapEditTerrainDatasetDefName");
            Scribe_Values.Look(ref terrainDatasetVersion, "historicalMapEditTerrainDatasetVersion", 0);
            Scribe_Collections.Look(ref terrainOverrides, "historicalMapTerrainOverrides", LookMode.Deep);
            Scribe_Collections.Look(ref riverPaths, "historicalMapRiverPaths", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                terrainOverrides = terrainOverrides ?? new List<HistoricalMapTerrainOverride>();
                terrainOverrides.RemoveAll(item => item == null || item.TileId < 0);
                riverPaths = riverPaths ?? new List<HistoricalMapRiverPath>();
                riverPaths.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.Id)
                    || string.IsNullOrWhiteSpace(item.RiverDefName) || item.Points == null || item.Points.Count < 2);
            }
        }
    }

    public sealed class HistoricalMapRiverPath : IExposable
    {
        public string Id;
        public string RiverDefName;
        public List<HistoricalMapRiverPoint> Points = new List<HistoricalMapRiverPoint>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref RiverDefName, "riverDefName");
            Scribe_Collections.Look(ref Points, "points", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Points = Points ?? new List<HistoricalMapRiverPoint>();
        }
    }

    public sealed class HistoricalMapRiverPoint : IExposable
    {
        public double Longitude;
        public double Latitude;

        public HistoricalMapRiverPoint() { }

        public HistoricalMapRiverPoint(double longitude, double latitude)
        { Longitude = longitude; Latitude = latitude; }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Longitude, "longitude");
            Scribe_Values.Look(ref Latitude, "latitude");
        }
    }

    public sealed class HistoricalMapTerrainOverride : IExposable
    {
        public int TileId = -1;
        public double HistoricalLongitude;
        public double HistoricalLatitude;
        public Hilliness BaseTerrain;
        public Hilliness Terrain;

        public void Initialize(int tileId, double longitude, double latitude, Hilliness baseTerrain, Hilliness terrain)
        {
            TileId = tileId;
            HistoricalLongitude = longitude;
            HistoricalLatitude = latitude;
            BaseTerrain = baseTerrain;
            Terrain = terrain;
        }

        public void SetValue(Hilliness value) => Terrain = value;

        public void ExposeData()
        {
            Scribe_Values.Look(ref TileId, "tileId", -1);
            Scribe_Values.Look(ref HistoricalLongitude, "historicalLongitude");
            Scribe_Values.Look(ref HistoricalLatitude, "historicalLatitude");
            Scribe_Values.Look(ref BaseTerrain, "baseTerrain", Hilliness.Flat);
            Scribe_Values.Look(ref Terrain, "terrain", Hilliness.Flat);
        }
    }
}
