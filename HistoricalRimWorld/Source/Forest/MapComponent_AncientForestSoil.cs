using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>
    /// Permanent map-local record of living forest soil. Terrain layering keeps the visible
    /// natural terrain below ordinary floors; this bitset preserves its identity independently.
    /// </summary>
    public sealed class MapComponent_AncientForestSoil : MapComponent
    {
        private const int CurrentDataVersion = 1;
        private bool[] activeCells;
        private int savedWidth;
        private int savedHeight;
        private int dataVersion;
        private bool generationApplied;
        private List<int> debugTreeIds = new List<int>();

        public MapComponent_AncientForestSoil(Map map) : base(map) => Allocate();

        public int AncientForestSoilCellCount
        {
            get
            {
                int count = 0;
                if (activeCells != null) for (int i = 0; i < activeCells.Length; i++) if (activeCells[i]) count++;
                return count;
            }
        }

        public bool GenerationApplied => generationApplied;

        public bool IsAncientForestSoil(IntVec3 cell)
        {
            int index = IndexOf(cell);
            return index >= 0 && activeCells[index];
        }

        public IEnumerable<IntVec3> GetAncientForestSoilCells()
        {
            if (activeCells == null) yield break;
            for (int i = 0; i < activeCells.Length; i++)
                if (activeCells[i]) yield return new IntVec3(i % savedWidth, 0, i / savedWidth);
        }

        internal void AddAncientForestSoil(IntVec3 cell)
        {
            int index = IndexOf(cell);
            if (index >= 0) activeCells[index] = true;
        }

        internal void MarkGenerationApplied() => generationApplied = true;

        public bool CanGrowAncientTree(IntVec3 cell)
        {
            // Historical active soil is a high-quality habitat, not a hard boundary.
            return AncientForestReproductionUtility.CanSpawnAncientTree(cell, map, null);
        }

        internal void RegisterDebugTree(Thing tree)
        {
            if (tree != null && tree.thingIDNumber > 0 && !debugTreeIds.Contains(tree.thingIDNumber))
                debugTreeIds.Add(tree.thingIDNumber);
        }

        public int ClearDebugTrees()
        {
            int removed = 0;
            if (debugTreeIds == null) debugTreeIds = new List<int>();
            foreach (int id in debugTreeIds.ToList())
            {
                Thing tree = map.listerThings?.AllThings.FirstOrDefault(item => item?.thingIDNumber == id);
                if (tree == null || tree.Destroyed) continue;
                tree.Destroy(DestroyMode.Vanish);
                removed++;
            }
            debugTreeIds.Clear();
            return removed;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                savedWidth = map.Size.x;
                savedHeight = map.Size.z;
                dataVersion = CurrentDataVersion;
            }

            Scribe_Values.Look(ref dataVersion, "ancientForestSoilDataVersion", 0);
            Scribe_Values.Look(ref savedWidth, "ancientForestSoilWidth", 0);
            Scribe_Values.Look(ref savedHeight, "ancientForestSoilHeight", 0);
            Scribe_Values.Look(ref generationApplied, "ancientForestGenerationApplied", false);
            List<int> activeIndexes = null;
            if (Scribe.mode == LoadSaveMode.Saving && activeCells != null)
            {
                activeIndexes = new List<int>();
                for (int i = 0; i < activeCells.Length; i++) if (activeCells[i]) activeIndexes.Add(i);
            }
            Scribe_Collections.Look(ref activeIndexes, "ancientForestSoilCells", LookMode.Value);
            Scribe_Collections.Look(ref debugTreeIds, "ancientForestDebugTreeIds", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Allocate();
                if (dataVersion == CurrentDataVersion && savedWidth == map.Size.x && savedHeight == map.Size.z
                    && activeIndexes != null)
                {
                    foreach (int index in activeIndexes)
                        if ((uint)index < (uint)activeCells.Length) activeCells[index] = true;
                }
                else
                {
                    // Old saves intentionally remain unchanged; they are never mass-converted.
                    generationApplied = true;
                }
                debugTreeIds = debugTreeIds ?? new List<int>();
            }
        }

        private void Allocate()
        {
            savedWidth = map?.Size.x ?? 0;
            savedHeight = map?.Size.z ?? 0;
            int count = Math.Max(0, savedWidth * savedHeight);
            if (activeCells == null || activeCells.Length != count) activeCells = new bool[count];
        }

        private int IndexOf(IntVec3 cell)
        {
            if (activeCells == null || !cell.InBounds(map)) return -1;
            return cell.z * savedWidth + cell.x;
        }
    }
}
