using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Relics
{
    public static class HistoricalRelicManager
    {
        private static HistoricalRelicGameComponent Component => Current.Game?.GetComponent<HistoricalRelicGameComponent>();
        public static bool IsActive => Component?.Initialized == true && HistoricalScenarioManager.IsScenarioActive;
        public static IEnumerable<HistoricalRelicRecord> Records => Component?.Relics ?? Enumerable.Empty<HistoricalRelicRecord>();
        public static IEnumerable<HistoricalRelicEventRecord> Events => Component?.Events ?? Enumerable.Empty<HistoricalRelicEventRecord>();
        public static HistoricalRelicRecord Find(string id) => Component?.Find(id);

        public static bool TryInitializeForScenario(HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (scenario == null || HistoricalScenarioManager.CurrentScenario?.defName != scenario.defName) return Fail("Scenario must be active before relics initialize.", out error);
            if (Component == null) return Fail("HistoricalRelicGameComponent is unavailable.", out error);
            return Component.Initialize(scenario.defName) || Fail("Relic state belongs to another scenario.", out error);
        }

        public static bool TryDesignateAsRelic(Thing thing, string relicDefName, string creatorPersonId, string ownerPersonId, out string relicId, out string error)
        {
            relicId = null; error = null;
            if (!IsActive) return Fail("Historical relics are inactive.", out error);
            HistoricalRelicDef def = DefDatabase<HistoricalRelicDef>.GetNamedSilentFail(relicDefName);
            CompHistoricalRelic comp = thing?.TryGetComp<CompHistoricalRelic>();
            if (def == null || thing == null || thing.Destroyed || thing.def != def.baseThingDef || comp == null || comp.IsLinked)
                return Fail("Select an unregistered item whose ThingDef matches this relic definition.", out error);
            HistoricalPersonCultureState culture = HistoricalCultureManager.FindPerson(creatorPersonId ?? ownerPersonId);
            HistoricalPersonRecord creator = HistoricalGenealogyManager.FindPerson(creatorPersonId);
            HistoricalPersonRecord owner = HistoricalGenealogyManager.FindPerson(ownerPersonId);
            if (ownerPersonId != null && owner == null) return Fail("Owner PersonId was not found.", out error);
            if (creatorPersonId != null && creator == null) return Fail("Creator PersonId was not found.", out error);
            string id = "HR_Relic_" + Guid.NewGuid().ToString("N");
            string region = owner?.CurrentRegionId ?? creator?.CurrentRegionId ?? HistoricalScenarioManager.CurrentScenario?.defaultStartingRegion?.defName;
            HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate);
            int year = HistoricalCalendarManager.IsActive ? currentDate.AstronomicalYear : HistoricalScenarioManager.CurrentScenario?.startAstronomicalYear ?? 0;
            HistoricalRelicRecord record = new HistoricalRelicRecord
            {
                RelicId = id, RelicDefName = def.defName, DisplayName = thing.LabelCap.ToString(),
                OriginCultureId = culture?.PrimaryCultureId ?? def.associatedCultures?.FirstOrDefault()?.defName,
                OriginRegionId = region, CreatorPersonId = creatorPersonId, OriginalOwnerPersonId = ownerPersonId,
                CurrentOwnerPersonId = ownerPersonId, CurrentFamilyId = owner?.FamilyId, ThingLoadId = thing.GetUniqueLoadID(),
                CreationAstronomicalYear = year
            };
            Component.Add(record); comp.SetRelicId(id); RecordEvent(record, HistoricalRelicEventType.Created, null, ownerPersonId, region, def.sourceNotes);
            relicId = id; return true;
        }

        public static bool TryTransfer(string relicId, string toPersonId, HistoricalRelicEventType eventType, out string error)
        {
            error = null;
            HistoricalRelicRecord record = IsActive ? Component.Find(relicId) : null;
            if (record == null || record.IsDestroyed || record.IsLost) return Fail("Relic record is unavailable or lost; recover it first.", out error);
            if (eventType != HistoricalRelicEventType.Acquired && eventType != HistoricalRelicEventType.Gifted && eventType != HistoricalRelicEventType.Inherited
                && eventType != HistoricalRelicEventType.Traded && eventType != HistoricalRelicEventType.Stolen)
                return Fail("This event type cannot transfer ownership.", out error);
            HistoricalPersonRecord to = HistoricalGenealogyManager.FindPerson(toPersonId);
            if (to == null) return Fail("Recipient PersonId was not found.", out error);
            string from = record.CurrentOwnerPersonId;
            record.CurrentOwnerPersonId = toPersonId; record.CurrentFamilyId = to.FamilyId; record.IsLost = false;
            RecordEvent(record, eventType, from, toPersonId, to.CurrentRegionId, null);
            if (eventType == HistoricalRelicEventType.Inherited && !string.IsNullOrWhiteSpace(to.FamilyId))
            {
                HistoricalFamilyCultureState family = HistoricalCultureManager.FindFamily(to.FamilyId);
                if (family?.PrimaryCultureId != null)
                    HistoricalCultureManager.TryIncreaseCommunityPresence(HistoricalCultureManager.Communities.FirstOrDefault()?.CommunityId, record.OriginCultureId, 0.02f, out _);
            }
            return true;
        }

        public static bool TryMarkLost(string relicId, out string error) => SetLost(relicId, false, out error);
        public static bool TryRecover(string relicId, string newOwnerPersonId, out string error)
        {
            if (!TryMarkRecovered(relicId, newOwnerPersonId, out error)) return false;
            return true;
        }
        private static bool TryMarkRecovered(string relicId, string newOwner, out string error)
        {
            HistoricalRelicRecord record = IsActive ? Component.Find(relicId) : null;
            if (record == null || !record.IsLost || record.IsDestroyed) return Fail("Only a lost, undestroyed relic can be recovered.", out error);
            if (newOwner != null && HistoricalGenealogyManager.FindPerson(newOwner) == null) return Fail("New owner was not found.", out error);
            string old = record.CurrentOwnerPersonId; record.CurrentOwnerPersonId = newOwner ?? old;
            record.CurrentFamilyId = HistoricalGenealogyManager.FindPerson(record.CurrentOwnerPersonId)?.FamilyId;
            record.IsLost = false; RecordEvent(record, HistoricalRelicEventType.Recovered, old, record.CurrentOwnerPersonId, null, null); error = null; return true;
        }
        private static bool SetLost(string relicId, bool destroyed, out string error)
        {
            error = null; HistoricalRelicRecord record = IsActive ? Component.Find(relicId) : null;
            if (record == null || record.IsDestroyed || record.IsLost) return Fail("Relic record is unavailable or already lost.", out error);
            record.IsLost = true; record.IsDestroyed |= destroyed;
            RecordEvent(record, destroyed ? HistoricalRelicEventType.Destroyed : HistoricalRelicEventType.Lost, record.CurrentOwnerPersonId, null, null, null);
            return true;
        }
        internal static void NotifyThingDestroyed(string relicId, string loadId, Map previousMap)
        {
            HistoricalRelicRecord record = IsActive ? Component.Find(relicId) : null;
            if (record == null || record.IsDestroyed) return;
            record.ThingLoadId = loadId; SetLost(relicId, true, out _);
        }
        private static void RecordEvent(HistoricalRelicRecord record, HistoricalRelicEventType type, string from, string to, string region, string notes)
        {
            int tick = Verse.Find.TickManager?.TicksGame ?? -1;
            HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate);
            int year = HistoricalCalendarManager.IsActive ? currentDate.AstronomicalYear : record.CreationAstronomicalYear;
            string id = "HR_RelicEvent_" + Guid.NewGuid().ToString("N");
            Component.Add(new HistoricalRelicEventRecord { EventId = id, RelicId = record.RelicId, EventType = type,
                FromPersonId = from, ToPersonId = to, RegionId = region ?? record.OriginRegionId, AstronomicalYear = year, GameTick = tick, SourceNotes = notes });
            record.EventIds.Add(id);
            HistoricalChronicleManager.TryAddLocalExperience("Relic:" + id, "HR_Relic_Chronicle_Title", "HR_Relic_Chronicle_Description",
                region ?? record.OriginRegionId, null, HistoricalEvidenceLevel.Probable, notes ?? "Gameplay record; historical provenance is not independently verified.", out _);
        }
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
