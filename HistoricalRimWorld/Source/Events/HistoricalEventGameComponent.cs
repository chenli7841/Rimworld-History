using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventGameComponent : GameComponent
    {
        private const int CurrentDataVersion = 3;
        private string scenarioDefName;
        private int dataVersion;
        private bool initialized;
        private List<HistoricalEventRuntimeState> eventStates = new List<HistoricalEventRuntimeState>();
        private List<string> pendingEventIds = new List<string>();
        private List<HistoricalEventStatusChange> statusChanges = new List<HistoricalEventStatusChange>();
        private long lastProcessedAbsoluteDay = long.MinValue;
        private HistoricalEventScheduler scheduler;
        private int lastCheckGameTick = -1;
        private string initializationAttemptedForScenario;
        private string pendingLocalEventId;
        private string pendingStorylineId;
        private long nextLocalEventAbsoluteDay;
        private List<HistoricalLocalEventRuntimeRecord> localEventRecords = new List<HistoricalLocalEventRuntimeRecord>();
        private List<HistoricalStorylineRuntimeState> storylineStates = new List<HistoricalStorylineRuntimeState>();

        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public IEnumerable<HistoricalEventRuntimeState> EventStates => eventStates ?? Enumerable.Empty<HistoricalEventRuntimeState>();
        public IEnumerable<string> PendingEventIds => pendingEventIds ?? Enumerable.Empty<string>();
        public IEnumerable<HistoricalEventStatusChange> StatusChanges => statusChanges ?? Enumerable.Empty<HistoricalEventStatusChange>();
        public string PendingLocalEventId => pendingLocalEventId;
        public string PendingStorylineId => pendingStorylineId;
        public long NextLocalEventAbsoluteDay => nextLocalEventAbsoluteDay;
        public IEnumerable<HistoricalLocalEventRuntimeRecord> LocalEventRecords => localEventRecords ?? Enumerable.Empty<HistoricalLocalEventRuntimeRecord>();
        public IEnumerable<HistoricalStorylineRuntimeState> StorylineStates => storylineStates ?? Enumerable.Empty<HistoricalStorylineRuntimeState>();

        public HistoricalEventRuntimeState FindState(string id) => eventStates?.FirstOrDefault(item => item != null && item.EventId == id);

        internal bool ApplyInitializationPlan(HistoricalEventInitializationPlan plan, out string error)
        {
            error = null;
            if (plan == null) return false;
            if (initialized) return string.Equals(scenarioDefName, plan.ScenarioDefName, StringComparison.Ordinal);
            if (!HistoricalChronicleManager.TryInitializeForScenario(plan.ScenarioDefName, out error)) return false;
            scenarioDefName = plan.ScenarioDefName;
            dataVersion = CurrentDataVersion;
            eventStates = plan.States;
            pendingEventIds = plan.PendingIds;
            statusChanges = new List<HistoricalEventStatusChange>();
            lastProcessedAbsoluteDay = plan.StartDate.AbsoluteDay - 1;
            nextLocalEventAbsoluteDay = plan.StartDate.AbsoluteDay + 60;
            pendingLocalEventId = null;
            pendingStorylineId = null;
            localEventRecords = new List<HistoricalLocalEventRuntimeRecord>();
            storylineStates = new List<HistoricalStorylineRuntimeState>();
            initialized = true;

            foreach (HistoricalEventDef item in plan.BackgroundEvents)
            {
                bool visible = HistoricalEventManager.IsVisible(item);
                if (!HistoricalChronicleManager.TryAddEventEntry(item, item.historicalDate, visible, out error))
                {
                    Log.Error("[Historical RimWorld] Background chronicle entry could not be added for " + item.defName + ": " + error);
                    error = null;
                }
            }
            EnsureScheduler();
            Log.Message("[Historical RimWorld] Initialized historical event engine for scenario " + scenarioDefName + ".");
            return true;
        }

        internal void CompleteState(HistoricalEventRuntimeState state, HistoricalEventExecutionStatus status,
            HistoricalEventFailureKind failureKind, string reason, HistoricalDate currentDate)
        {
            if (state == null || state.Status == status) return;
            int tick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            HistoricalEventExecutionStatus old = state.SetTerminalStatus(status, failureKind, reason, tick, currentDate);
            HistoricalEventStatusChange record = new HistoricalEventStatusChange();
            record.Initialize(state.EventId, old, status, tick, currentDate.AbsoluteDay, reason);
            if (statusChanges == null) statusChanges = new List<HistoricalEventStatusChange>();
            statusChanges.Add(record);
            if (status != HistoricalEventExecutionStatus.Pending) pendingEventIds.Remove(state.EventId);
            Log.Message("[Historical RimWorld] Event " + state.EventId + " changed from " + old + " to " + status
                + (string.IsNullOrEmpty(reason) ? "." : ": " + reason));
        }

        internal void RecordPendingAttempt(HistoricalEventRuntimeState state, long absoluteDay,
            HistoricalEventFailureKind failureKind, string reason)
        {
            if (state == null) return;
            state.RecordAttempt(absoluteDay);
            state.SetPendingDiagnostic(failureKind, reason);
        }

        internal void MarkDayProcessed(long absoluteDay) => lastProcessedAbsoluteDay = absoluteDay;

        internal void SetPendingLocalEvent(string id, string storylineId = null) { pendingLocalEventId = id; pendingStorylineId = storylineId; }
        internal void SetNextLocalEventDay(long day) => nextLocalEventAbsoluteDay = day;
        internal bool TryResolveLocalEventChoice(string id, int choiceIndex, out string error)
        {
            EnsureScheduler();
            if (scheduler == null) { error = "The historical event scheduler is unavailable."; return false; }
            return scheduler.TryResolveLocalEventChoice(id, choiceIndex, out error);
        }
        internal bool CanResolveLocalEventChoice(string id, int choiceIndex, out string error)
        {
            EnsureScheduler();
            if (scheduler == null) { error = "The historical event scheduler is unavailable."; return false; }
            return scheduler.CanResolveLocalEventChoice(id, choiceIndex, out error);
        }
        internal HistoricalLocalEventRuntimeRecord FindLocalRecord(string id) =>
            localEventRecords?.FirstOrDefault(item => item != null && item.eventId == id);
        internal HistoricalLocalEventRuntimeRecord GetOrCreateLocalRecord(string id)
        {
            if (localEventRecords == null) localEventRecords = new List<HistoricalLocalEventRuntimeRecord>();
            HistoricalLocalEventRuntimeRecord record = FindLocalRecord(id);
            if (record != null) return record;
            record = new HistoricalLocalEventRuntimeRecord { eventId = id };
            localEventRecords.Add(record);
            return record;
        }
        internal HistoricalStorylineRuntimeState FindStoryline(string id) =>
            storylineStates?.FirstOrDefault(item => item != null && item.storylineId == id);
        internal HistoricalStorylineRuntimeState GetOrCreateStoryline(string id)
        {
            if (storylineStates == null) storylineStates = new List<HistoricalStorylineRuntimeState>();
            HistoricalStorylineRuntimeState state = FindStoryline(id);
            if (state != null) return state;
            state = new HistoricalStorylineRuntimeState { storylineId = id };
            storylineStates.Add(state);
            return state;
        }

        internal void RemovePendingId(string id) => pendingEventIds?.Remove(id);

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.TickManager.TicksGame % 250 != 0
                || lastCheckGameTick == Find.TickManager.TicksGame) return;
            lastCheckGameTick = Find.TickManager.TicksGame;

            if (!initialized)
            {
                HistoricalScenarioDef scenario = HistoricalScenarioManager.CurrentScenario;
                if (scenario != null && initializationAttemptedForScenario != scenario.defName)
                {
                    initializationAttemptedForScenario = scenario.defName;
                    if (!HistoricalEventManager.TryInitializeForScenario(scenario, out string error))
                        Log.Error("[Historical RimWorld] Event engine initialization failed: " + error);
                }
                return;
            }

            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate)) return;
            if (currentDate.AbsoluteDay > lastProcessedAbsoluteDay)
            {
                if (nextLocalEventAbsoluteDay == 0L) nextLocalEventAbsoluteDay = currentDate.AbsoluteDay + 60;
                EnsureScheduler();
                scheduler?.ProcessDueEvents(currentDate);
                lastProcessedAbsoluteDay = currentDate.AbsoluteDay;
                HistoricalChronicleManager.RefreshRegionVisibility();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalEventScenarioDefName");
            Scribe_Values.Look(ref dataVersion, "historicalEventDataVersion", 0);
            Scribe_Values.Look(ref initialized, "historicalEventEngineInitialized", false);
            Scribe_Collections.Look(ref eventStates, "historicalEventRuntimeStates", LookMode.Deep);
            Scribe_Collections.Look(ref pendingEventIds, "historicalPendingEventIds", LookMode.Value);
            Scribe_Collections.Look(ref statusChanges, "historicalEventStatusChanges", LookMode.Deep);
            Scribe_Values.Look(ref lastProcessedAbsoluteDay, "historicalEventLastProcessedAbsoluteDay", long.MinValue);
            Scribe_Values.Look(ref pendingLocalEventId, "historicalPendingLocalEventId");
            Scribe_Values.Look(ref pendingStorylineId, "historicalPendingStorylineId");
            Scribe_Values.Look(ref nextLocalEventAbsoluteDay, "historicalNextLocalEventAbsoluteDay", 0L);
            Scribe_Collections.Look(ref localEventRecords, "historicalLocalEventRecords", LookMode.Deep);
            Scribe_Collections.Look(ref storylineStates, "historicalStorylineStates", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                eventStates = eventStates ?? new List<HistoricalEventRuntimeState>();
                pendingEventIds = pendingEventIds ?? new List<string>();
                statusChanges = statusChanges ?? new List<HistoricalEventStatusChange>();
                localEventRecords = localEventRecords ?? new List<HistoricalLocalEventRuntimeRecord>();
                storylineStates = storylineStates ?? new List<HistoricalStorylineRuntimeState>();
                foreach (HistoricalLocalEventRuntimeRecord record in localEventRecords.Where(item => item != null))
                    record.triggerCount = Math.Max(record.triggerCount, record.appearances);
                scheduler = null;
                lastCheckGameTick = -1;
                initializationAttemptedForScenario = null;
                if (initialized && dataVersion != CurrentDataVersion)
                {
                    Log.Warning("[Historical RimWorld] Historical event save data version " + dataVersion
                        + " differs from current " + CurrentDataVersion + "; stored event IDs and outcomes were preserved.");
                    dataVersion = CurrentDataVersion;
                }
                if (initialized)
                {
                    foreach (HistoricalEventRuntimeState state in eventStates.Where(item => item != null))
                    {
                        if (DefDatabase<HistoricalEventDef>.GetNamedSilentFail(state.EventId) == null)
                            Log.Warning("[Historical RimWorld] Saved event state references missing Def " + state.EventId + "; the saved outcome was preserved.");
                    }
                }
            }
        }

        private void EnsureScheduler()
        {
            if (scheduler == null && initialized) scheduler = new HistoricalEventScheduler(this, scenarioDefName);
        }
    }
}
