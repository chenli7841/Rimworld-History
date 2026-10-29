using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Relics
{
    public sealed class HistoricalRelicRecord : IExposable
    {
        public string RelicId;
        public string RelicDefName;
        public string DisplayName;
        public string OriginCultureId;
        public string OriginRegionId;
        public string CreatorPersonId;
        public string OriginalOwnerPersonId;
        public string CurrentOwnerPersonId;
        public string CurrentFamilyId;
        public string ThingLoadId;
        public int CreationAstronomicalYear;
        public bool IsLost;
        public bool IsDestroyed;
        public List<string> EventIds = new List<string>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref RelicId, "relicId"); Scribe_Values.Look(ref RelicDefName, "relicDefName");
            Scribe_Values.Look(ref DisplayName, "displayName"); Scribe_Values.Look(ref OriginCultureId, "originCultureId");
            Scribe_Values.Look(ref OriginRegionId, "originRegionId"); Scribe_Values.Look(ref CreatorPersonId, "creatorPersonId");
            Scribe_Values.Look(ref OriginalOwnerPersonId, "originalOwnerPersonId"); Scribe_Values.Look(ref CurrentOwnerPersonId, "currentOwnerPersonId");
            Scribe_Values.Look(ref CurrentFamilyId, "currentFamilyId"); Scribe_Values.Look(ref ThingLoadId, "thingLoadId");
            Scribe_Values.Look(ref CreationAstronomicalYear, "creationAstronomicalYear"); Scribe_Values.Look(ref IsLost, "isLost");
            Scribe_Values.Look(ref IsDestroyed, "isDestroyed"); Scribe_Collections.Look(ref EventIds, "eventIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) EventIds = EventIds ?? new List<string>();
        }
    }
}
