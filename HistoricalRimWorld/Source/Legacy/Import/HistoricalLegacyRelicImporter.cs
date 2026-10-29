using System;
using HistoricalRimWorld.Legacy;
using HistoricalRimWorld.Relics;
using Verse;

namespace HistoricalRimWorld.Legacy.Import
{
    /// <summary>Prepares one archive relic as a new Thing while preserving its stable RelicId.</summary>
    public static class HistoricalLegacyRelicImporter
    {
        public static bool TryCreateThing(LegacyRelicData data, string ownerPersonId, string familyId,
            out Thing thing, out HistoricalRelicRecord record, out string error)
        {
            thing = null;
            record = null;
            error = null;
            if (data == null || data.IsLost || data.IsDestroyed || string.IsNullOrWhiteSpace(data.RelicId))
            { error = "Only a recovered, intact relic can be selected for a new Pawn."; return false; }
            HistoricalRelicDef relicDef = DefDatabase<HistoricalRelicDef>.GetNamedSilentFail(data.RelicDefName);
            if (relicDef?.baseThingDef == null)
            { error = "Relic definition is missing or has no base ThingDef: " + data.RelicDefName; return false; }
            try
            {
                thing = ThingMaker.MakeThing(relicDef.baseThingDef);
                CompHistoricalRelic link = thing?.TryGetComp<CompHistoricalRelic>();
                if (thing == null || link == null)
                { if (thing != null) thing.Destroy(DestroyMode.Vanish); thing = null; error = "The relic Thing has no CompHistoricalRelic."; return false; }
                link.SetRelicId(data.RelicId);
                record = new HistoricalRelicRecord
                {
                    RelicId = data.RelicId, RelicDefName = data.RelicDefName, DisplayName = data.DisplayName,
                    OriginCultureId = data.OriginCultureId, OriginRegionId = data.OriginRegionId,
                    CreatorPersonId = data.CreatorPersonId, OriginalOwnerPersonId = data.OriginalOwnerPersonId,
                    CurrentOwnerPersonId = ownerPersonId, CurrentFamilyId = familyId,
                    ThingLoadId = thing.GetUniqueLoadID(), CreationAstronomicalYear = data.CreationAstronomicalYear,
                    IsLost = false, IsDestroyed = false
                };
                return true;
            }
            catch (Exception exception)
            {
                if (thing != null && !thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
                thing = null;
                error = "Relic preparation failed: " + exception.Message;
                return false;
            }
        }
    }
}
