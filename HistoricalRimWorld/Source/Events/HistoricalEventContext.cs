using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventContext
    {
        public HistoricalScenarioDef Scenario { get; }
        public HistoricalEventDef Event { get; }
        public HistoricalDate CurrentDate { get; }
        public HistoricalDate ScheduledDate { get; }
        public Map Map { get; }
        public string CommunityId { get; }
        public HistoricalPoliticalChangeSource ChangeSource => HistoricalPoliticalChangeSource.HistoricalEvent;

        public HistoricalEventContext(HistoricalScenarioDef scenario, HistoricalEventDef historicalEvent,
            HistoricalDate currentDate, HistoricalDate scheduledDate, Map map = null, string communityId = null)
        {
            Scenario = scenario;
            Event = historicalEvent;
            CurrentDate = currentDate;
            ScheduledDate = scheduledDate;
            Map = map;
            CommunityId = communityId;
        }
    }
}
