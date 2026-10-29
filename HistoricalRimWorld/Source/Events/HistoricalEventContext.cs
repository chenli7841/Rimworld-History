using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventContext
    {
        public HistoricalScenarioDef Scenario { get; }
        public HistoricalEventDef Event { get; }
        public HistoricalDate CurrentDate { get; }
        public HistoricalDate ScheduledDate { get; }
        public HistoricalPoliticalChangeSource ChangeSource => HistoricalPoliticalChangeSource.HistoricalEvent;

        public HistoricalEventContext(HistoricalScenarioDef scenario, HistoricalEventDef historicalEvent,
            HistoricalDate currentDate, HistoricalDate scheduledDate)
        {
            Scenario = scenario;
            Event = historicalEvent;
            CurrentDate = currentDate;
            ScheduledDate = scheduledDate;
        }
    }
}
