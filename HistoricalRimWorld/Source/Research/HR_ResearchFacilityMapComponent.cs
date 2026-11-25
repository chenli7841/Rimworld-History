using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Research
{
    /// <summary>Low-frequency cache for nearby manuscript racks; rebuilt after relevant buildings change.</summary>
    public sealed class HR_ResearchFacilityMapComponent : MapComponent
    {
        private const int RefreshIntervalTicks = 2500;
        private const string RackDefName = "HR_ScrollStorageRack";
        private readonly Dictionary<int, int> racksByBenchThingId = new Dictionary<int, int>();
        private int lastRefreshTick = -1;
        private bool dirty = true;

        public HR_ResearchFacilityMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive || Find.TickManager == null) return;
            if (!dirty && lastRefreshTick >= 0 && Find.TickManager.TicksGame - lastRefreshTick < RefreshIntervalTicks) return;
            RebuildCache();
        }

        public int NearbyRackCount(Building_ResearchBench bench)
        {
            if (bench == null) return 0;
            EnsureFresh();
            return racksByBenchThingId.TryGetValue(bench.thingIDNumber, out int count) ? count : 0;
        }

        public void Invalidate() => dirty = true;

        private void EnsureFresh()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (dirty || lastRefreshTick < 0 || now - lastRefreshTick >= RefreshIntervalTicks)
                RebuildCache();
        }

        private void RebuildCache()
        {
            racksByBenchThingId.Clear();
            ThingDef rackDef = DefDatabase<ThingDef>.GetNamedSilentFail(RackDefName);
            List<Thing> racks = rackDef == null ? null : map.listerThings?.ThingsOfDef(rackDef);
            List<Thing> benches = map.listerThings?.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            if (racks != null && benches != null)
            {
                foreach (Building_ResearchBench bench in benches.OfType<Building_ResearchBench>())
                {
                    HR_ResearchFacilityExtension facility = bench.def?.GetModExtension<HR_ResearchFacilityExtension>();
                    if (facility?.facilityType != HR_ResearchFacilityType.WritingDesk) continue;
                    Room room = bench.GetRoom();
                    if (room == null || room.PsychologicallyOutdoors) continue;

                    int count = 0;
                    foreach (Thing rack in racks)
                    {
                        if (rack == null || !rack.Spawned || rack.Destroyed || rack.GetRoom() != room) continue;
                        if (rack.Position.DistanceToSquared(bench.Position) > 36) continue;
                        count++;
                        if (count >= 2) break;
                    }
                    if (count > 0) racksByBenchThingId[bench.thingIDNumber] = count;
                }
            }

            lastRefreshTick = Find.TickManager?.TicksGame ?? 0;
            dirty = false;
        }
    }
}
