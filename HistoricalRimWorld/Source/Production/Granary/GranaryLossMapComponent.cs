using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Production.Granary
{
    /// <summary>
    /// Applies a lightweight, map-local annual loss only to the explicit grain whitelist.
    /// Grain stays as ordinary map Things, so vanilla cooking, hauling, storage and caravans can use it.
    /// </summary>
    public sealed class GranaryLossMapComponent : MapComponent
    {
        private const string EasternZhouScenarioId = "HR_Scenario_EasternZhou_770BCE";
        private const int GameYearDays = 60;
        private const int TicksPerGameDay = 60000;
        private int lastProcessedTick = -1;
        private List<GranaryLossRecord> records = new List<GranaryLossRecord>();

        public GranaryLossMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager == null || !IsEasternZhouScenarioActive) return;

            int ticks = Find.TickManager.TicksGame;
            int ticksPerDay = TicksPerGameDay;
            if (ticks % ticksPerDay != 0 || ticks == lastProcessedTick) return;

            int elapsedTicks = lastProcessedTick < 0 ? 0 : Math.Min(ticksPerDay, Math.Max(0, ticks - lastProcessedTick));
            lastProcessedTick = ticks;
            if (elapsedTicks > 0) ApplyLoss(elapsedTicks, ticks);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastProcessedTick, "granaryLossLastProcessedTick", -1);
            Scribe_Collections.Look(ref records, "granaryLossRecords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && records == null)
                records = new List<GranaryLossRecord>();
        }

        private static bool IsEasternZhouScenarioActive => HistoricalScenarioManager.IsScenarioActive
            && string.Equals(HistoricalScenarioManager.ActiveScenarioDefName, EasternZhouScenarioId, StringComparison.Ordinal);

        private void ApplyLoss(int elapsedTicks, int currentTick)
        {
            if (records == null) records = new List<GranaryLossRecord>();
            GranarySettingsDef settings = GranarySettingsDef.Settings;
            if (settings?.eligibleGrains == null || settings.eligibleGrains.Count == 0) return;

            Dictionary<int, GranaryLossRecord> byThingId = records
                .Where(record => record != null && record.thingId != 0)
                .GroupBy(record => record.thingId)
                .ToDictionary(group => group.Key, group => group.Last());
            List<Thing> grainThings = new List<Thing>();
            List<int> staleIds = new List<int>();
            List<Thing> allThings = map.listerThings.AllThings;

            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (!GranaryStorageUtility.IsEligibleGrain(thing) || thing.Destroyed || !thing.Spawned) continue;

                grainThings.Add(thing);
                int thingId = thing.thingIDNumber;
                if (!byThingId.TryGetValue(thingId, out GranaryLossRecord record))
                {
                    record = new GranaryLossRecord { thingId = thingId, lastSeenTick = currentTick };
                    records.Add(record);
                    byThingId.Add(thingId, record);
                }
                record.lastSeenTick = currentTick;
                double expectedLoss = thing.stackCount * GranaryStorageUtility.AnnualLossRateFor(thing)
                    * elapsedTicks / (TicksPerGameDay * (double)GameYearDays);
                record.fractionalLoss += expectedLoss;
            }

            // Do not mutate the map's listerThings collection while it is being enumerated.
            for (int i = 0; i < grainThings.Count; i++)
            {
                Thing thing = grainThings[i];
                if (thing.Destroyed || !byThingId.TryGetValue(thing.thingIDNumber, out GranaryLossRecord record)) continue;
                int wholeUnits = (int)Math.Floor(record.fractionalLoss + 0.000001d);
                if (wholeUnits <= 0) continue;

                int loss = Math.Min(wholeUnits, thing.stackCount);
                record.fractionalLoss = loss == thing.stackCount ? 0d : Math.Max(0d, record.fractionalLoss - loss);
                if (loss >= thing.stackCount)
                {
                    staleIds.Add(thing.thingIDNumber);
                    thing.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    Thing lostGrain = thing.SplitOff(loss);
                    lostGrain?.Destroy(DestroyMode.Vanish);
                }
            }

            if (staleIds.Count > 0)
            {
                HashSet<int> destroyedIds = new HashSet<int>(staleIds);
                records.RemoveAll(record => record == null || destroyedIds.Contains(record.thingId));
            }

            // Bound the saved ledger without retroactively simulating grain that left a loaded map.
            int retentionTicks = TicksPerGameDay * GameYearDays * 2;
            records.RemoveAll(record => record == null || currentTick - record.lastSeenTick > retentionTicks);
        }
    }

    public sealed class GranaryLossRecord : IExposable
    {
        public int thingId;
        public int lastSeenTick;
        public double fractionalLoss;

        public void ExposeData()
        {
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref lastSeenTick, "lastSeenTick");
            Scribe_Values.Look(ref fractionalLoss, "fractionalLoss");
        }
    }
}
