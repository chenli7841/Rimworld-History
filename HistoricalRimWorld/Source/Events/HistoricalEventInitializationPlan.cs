using System.Collections.Generic;
using HistoricalRimWorld.Calendar;

namespace HistoricalRimWorld.Events
{
    internal sealed class HistoricalEventInitializationPlan
    {
        public string ScenarioDefName { get; }
        public HistoricalDate StartDate { get; }
        public List<HistoricalEventRuntimeState> States { get; }
        public List<string> PendingIds { get; }
        public List<HistoricalEventDef> BackgroundEvents { get; }

        public HistoricalEventInitializationPlan(string scenarioId, HistoricalDate startDate,
            List<HistoricalEventRuntimeState> states, List<string> pendingIds, List<HistoricalEventDef> background)
        {
            ScenarioDefName = scenarioId; StartDate = startDate; States = states; PendingIds = pendingIds; BackgroundEvents = background;
        }
    }
}
