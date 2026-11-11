using Verse;

namespace HistoricalRimWorld.Forest
{
    public sealed class ForestEcologySnapshot : IExposable
    {
        public int Tick;
        public int Area;
        public int TreeCount;
        public float Integrity;

        public ForestEcologySnapshot() { }

        public ForestEcologySnapshot(int tick, int area, int treeCount, float integrity)
        {
            Tick = tick;
            Area = area;
            TreeCount = treeCount;
            Integrity = integrity;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick", 0);
            Scribe_Values.Look(ref Area, "area", 0);
            Scribe_Values.Look(ref TreeCount, "treeCount", 0);
            Scribe_Values.Look(ref Integrity, "integrity", 0f);
        }
    }
}
