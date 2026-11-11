using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Cached map-local forest metrics. Cell indexes and plant references are rebuilt, not serialized.</summary>
    public sealed class ForestEcologyRegion : IExposable
    {
        private List<int> cellIndexes = new List<int>();
        private List<int> vegetationCellIndexes = new List<int>();
        private List<int> knownCellIndexes = new List<int>();
        private List<Plant> plants = new List<Plant>();
        private List<ForestEcologySnapshot> snapshots = new List<ForestEcologySnapshot>();

        public int RegionId { get; internal set; }
        public int CellCount { get; internal set; }
        public int ForestAreaCells => CellCount;
        public int AncientSoilAreaCells { get; internal set; }
        public int TreeCount { get; internal set; }
        public int TreeOccupiedCells => TreeCount;
        public int AncientTreeCount { get; internal set; }
        public int MatureAncientTreeCount { get; internal set; }
        public float TreeDensity { get; internal set; }
        public float MatureAncientTreeRatio { get; internal set; }
        public float HabitatCapacity { get; internal set; }
        public float HabitatCapacityScore { get; internal set; }
        public int HabitatCapacityTier { get; internal set; }
        public float EcologicalIntegrity { get; internal set; }
        public ForestEcologyStatus Status { get; internal set; }
        public ForestEcologyTrend Trend { get; internal set; }
        public IntVec3 Center { get; internal set; }
        public int AnchorCellIndex { get; internal set; }
        public int LastUpdatedTick { get; internal set; }
        public int LastStatusChangeTick { get; internal set; }
        public int KnownCellCount => knownCellIndexes?.Count ?? 0;
        public bool FullyExplored => CellCount > 0 && KnownCellCount >= CellCount;
        public IReadOnlyList<int> CellIndexes => cellIndexes;
        public IReadOnlyList<int> VegetationCellIndexes => vegetationCellIndexes;
        public IReadOnlyList<int> KnownCellIndexes => knownCellIndexes;
        public IReadOnlyList<ForestEcologySnapshot> Snapshots => snapshots;
        internal List<Plant> Plants => plants;

        public ForestEcologyRegion() { }

        internal void SetCells(List<int> cells, List<int> vegetation, List<Plant> regionPlants, int width)
        {
            cellIndexes = cells ?? new List<int>();
            vegetationCellIndexes = vegetation ?? new List<int>();
            plants = regionPlants ?? new List<Plant>();
            CellCount = cellIndexes.Count;
            AnchorCellIndex = vegetationCellIndexes.Count > 0 ? vegetationCellIndexes[0] : -1;
            if (vegetationCellIndexes.Count > 0)
            {
                long sumX = 0;
                long sumZ = 0;
                for (int i = 0; i < vegetationCellIndexes.Count; i++)
                {
                    sumX += vegetationCellIndexes[i] % width;
                    sumZ += vegetationCellIndexes[i] / width;
                }
                int x = (int)(sumX / vegetationCellIndexes.Count);
                int z = (int)(sumZ / vegetationCellIndexes.Count);
                // The mean may fall outside the patch; use the nearest real forest cell.
                int nearest = vegetationCellIndexes[0];
                long bestDistance = long.MaxValue;
                foreach (int index in vegetationCellIndexes)
                {
                    long dx = index % width - x;
                    long dz = index / width - z;
                    long distance = dx * dx + dz * dz;
                    if (distance < bestDistance || (distance == bestDistance && index < nearest))
                    {
                        nearest = index;
                        bestDistance = distance;
                    }
                }
                Center = new IntVec3(nearest % width, 0, nearest / width);
                AnchorCellIndex = nearest;
            }
            knownCellIndexes = new List<int>();
        }

        internal void CopyHistoryFrom(ForestEcologyRegion previous)
        {
            if (previous == null) return;
            snapshots = new List<ForestEcologySnapshot>(previous.snapshots ?? new List<ForestEcologySnapshot>());
            Status = previous.Status;
            Trend = previous.Trend;
            LastStatusChangeTick = previous.LastStatusChangeTick;
            AncientSoilAreaCells = previous.AncientSoilAreaCells;
            EcologicalIntegrity = previous.EcologicalIntegrity;
            TreeCount = previous.TreeCount;
        }

        internal void SetKnownCells(List<int> known) => knownCellIndexes = known ?? new List<int>();

        internal void RecordSnapshot(int tick)
        {
            if (snapshots == null) snapshots = new List<ForestEcologySnapshot>();
            if (snapshots.Count > 0 && snapshots[snapshots.Count - 1].Tick == tick) return;
            snapshots.Add(new ForestEcologySnapshot(tick, CellCount, TreeCount, EcologicalIntegrity));
            while (snapshots.Count > 30) snapshots.RemoveAt(0);
            UpdateTrend();
        }

        internal void UpdateTrend()
        {
            if (snapshots == null || snapshots.Count < 2)
            {
                Trend = ForestEcologyTrend.Unknown;
                return;
            }
            ForestEcologySnapshot old = snapshots[Math.Max(0, snapshots.Count - 5)];
            ForestEcologySnapshot current = snapshots[snapshots.Count - 1];
            int treeDelta = current.TreeCount - old.TreeCount;
            int areaDelta = current.Area - old.Area;
            float integrityDelta = current.Integrity - old.Integrity;
            if (treeDelta < -Math.Max(2, old.TreeCount / 10) || areaDelta < -Math.Max(10, old.Area / 10)
                || integrityDelta < -12f) Trend = ForestEcologyTrend.Declining;
            else if ((treeDelta > 0 || areaDelta > 0 || integrityDelta > 8f) && old.TreeCount < current.TreeCount + 1)
                Trend = ForestEcologyTrend.Recovering;
            else if (treeDelta > 2 || areaDelta > 5 || integrityDelta > 3f) Trend = ForestEcologyTrend.Growing;
            else Trend = ForestEcologyTrend.Stable;
        }

        internal ForestEcologyRegion PersistenceCopy()
        {
            return new ForestEcologyRegion
            {
                RegionId = RegionId,
                AnchorCellIndex = AnchorCellIndex,
                Status = Status,
                Trend = Trend,
                LastStatusChangeTick = LastStatusChangeTick,
                snapshots = new List<ForestEcologySnapshot>(snapshots ?? new List<ForestEcologySnapshot>())
            };
        }

        public void ExposeData()
        {
            int regionId = RegionId;
            int anchorCellIndex = AnchorCellIndex;
            int lastStatusChangeTick = LastStatusChangeTick;
            ForestEcologyStatus status = Status;
            ForestEcologyTrend trend = Trend;
            Scribe_Values.Look(ref regionId, "regionId", 0);
            Scribe_Values.Look(ref anchorCellIndex, "anchorCellIndex", -1);
            Scribe_Values.Look(ref status, "status", ForestEcologyStatus.Sparse);
            Scribe_Values.Look(ref trend, "trend", ForestEcologyTrend.Unknown);
            Scribe_Values.Look(ref lastStatusChangeTick, "lastStatusChangeTick", 0);
            Scribe_Collections.Look(ref snapshots, "snapshots", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RegionId = regionId;
                AnchorCellIndex = anchorCellIndex;
                Status = status;
                Trend = trend;
                LastStatusChangeTick = lastStatusChangeTick;
                snapshots = snapshots ?? new List<ForestEcologySnapshot>();
            }
        }
    }
}
