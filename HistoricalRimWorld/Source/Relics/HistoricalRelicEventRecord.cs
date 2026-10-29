using Verse;

namespace HistoricalRimWorld.Relics
{
    public sealed class HistoricalRelicEventRecord : IExposable
    {
        public string EventId;
        public string RelicId;
        public HistoricalRelicEventType EventType;
        public string FromPersonId;
        public string ToPersonId;
        public string RegionId;
        public int AstronomicalYear;
        public int GameTick;
        public string SourceNotes;
        public void ExposeData()
        {
            Scribe_Values.Look(ref EventId, "eventId"); Scribe_Values.Look(ref RelicId, "relicId");
            Scribe_Values.Look(ref EventType, "eventType", HistoricalRelicEventType.Created); Scribe_Values.Look(ref FromPersonId, "fromPersonId");
            Scribe_Values.Look(ref ToPersonId, "toPersonId"); Scribe_Values.Look(ref RegionId, "regionId");
            Scribe_Values.Look(ref AstronomicalYear, "astronomicalYear"); Scribe_Values.Look(ref GameTick, "gameTick"); Scribe_Values.Look(ref SourceNotes, "sourceNotes");
        }
    }
}
