using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Events
{
    public static class HistoricalEventManager
    {
        public static IEnumerable<HistoricalEventRuntimeState> States => GetComponent()?.EventStates ?? Enumerable.Empty<HistoricalEventRuntimeState>();
        public static IEnumerable<HistoricalEventStatusChange> StatusChanges => GetComponent()?.StatusChanges ?? Enumerable.Empty<HistoricalEventStatusChange>();

        public static bool WasExecuted(string eventId)
        {
            HistoricalEventRuntimeState state = GetComponent()?.FindState(eventId);
            HistoricalEventDef item = DefDatabase<HistoricalEventDef>.GetNamedSilentFail(eventId);
            return state != null && (state.Status == HistoricalEventExecutionStatus.Executed
                || (state.Status == HistoricalEventExecutionStatus.Skipped && item?.activationMode == HistoricalEventActivationMode.InitialBackground));
        }

        public static bool IsEventKnownToCurrentScenario(string eventId) =>
            GetComponent()?.FindState(eventId) != null;

        public static bool IsVisible(HistoricalEventDef item)
        {
            if (item == null) return false;
            switch (item.visibility)
            {
                case HistoricalEventVisibility.Public: return true;
                case HistoricalEventVisibility.Hidden: return false;
                case HistoricalEventVisibility.RegionKnowledgeRequired:
                    return HistoricalChronicleManager.IsKnownRegion(item.regionId);
                default: return false;
            }
        }

        internal static bool TryInitializeForScenario(HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (scenario == null || HistoricalScenarioManager.CurrentScenario?.defName != scenario.defName)
                return Reject("Scenario must be active before the event engine initializes.", out error);
            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate currentDate))
                return Reject("Historical calendar must be active before the event engine initializes.", out error);
            if (Current.Game == null) return Reject("A game must be loaded.", out error);
            HistoricalEventGameComponent component = GetComponent();
            if (component == null) return Reject("HistoricalEventGameComponent is unavailable.", out error);
            if (component.Initialized) return string.Equals(component.ScenarioDefName, scenario.defName, StringComparison.Ordinal)
                || Reject("Event engine is already initialized for another scenario.", out error);

            if (!ValidateScenarioDefinitions(scenario, out error)) return false;

            List<HistoricalEventRuntimeState> states = new List<HistoricalEventRuntimeState>();
            List<string> pending = new List<string>();
            List<HistoricalEventDef> background = new List<HistoricalEventDef>();
            foreach (HistoricalEventDef item in DefDatabase<HistoricalEventDef>.AllDefsListForReading
                .Where(def => def.scenario != null && def.scenario.defName == scenario.defName)
                .OrderBy(def => HistoricalEventDateResolver.ResolveScheduledDate(def.historicalDate))
                .ThenBy(def => def.priority).ThenBy(def => def.defName, StringComparer.Ordinal))
            {
                HistoricalDate scheduled = HistoricalEventDateResolver.ResolveScheduledDate(item.historicalDate);
                HistoricalEventRuntimeState state = new HistoricalEventRuntimeState();
                if (item.activationMode == HistoricalEventActivationMode.InitialBackground)
                {
                    state.Initialize(item.defName, HistoricalEventExecutionStatus.Skipped, "Initial background; added to the chronicle without replaying actions.");
                    background.Add(item);
                }
                else if (item.scope == HistoricalEventScope.LocalCommunity)
                {
                    state.Initialize(item.defName, HistoricalEventExecutionStatus.Skipped, "Local random incident scheduling is not part of HR-006.");
                }
                else if (scheduled < currentDate)
                {
                    state.Initialize(item.defName, HistoricalEventExecutionStatus.Skipped, "Scheduled date passed before the event engine initialized; event was not replayed.");
                    if (item.recordInChronicle) background.Add(item);
                }
                else
                {
                    state.Initialize(item.defName, HistoricalEventExecutionStatus.Pending);
                    pending.Add(item.defName);
                }
                states.Add(state);
            }

            HistoricalEventInitializationPlan plan = new HistoricalEventInitializationPlan(
                scenario.defName, currentDate, states, pending, background);
            return component.ApplyInitializationPlan(plan, out error)
                || Reject("Event engine initialization failed.", out error);
        }

        public static bool TryGetState(string eventId, out HistoricalEventRuntimeState state)
        {
            state = GetComponent()?.FindState(eventId);
            return state != null;
        }

        internal static bool ValidateScenarioDefinitions(HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (scenario == null) return Reject("No scenario was supplied.", out error);
            List<string> errors = HistoricalEventValidator.ValidateScenarioEvents(scenario).ToList();
            foreach (HistoricalEventDef item in DefDatabase<HistoricalEventDef>.AllDefsListForReading
                .Where(def => def.scenario != null && def.scenario.defName == scenario.defName))
                errors.AddRange(HistoricalEventValidator.ValidateDef(item));
            if (errors.Count == 0) return true;
            return Reject(string.Join(Environment.NewLine, errors), out error);
        }

        private static HistoricalEventGameComponent GetComponent() =>
            Current.Game == null ? null : Current.Game.GetComponent<HistoricalEventGameComponent>();

        private static bool Reject(string message, out string error) { error = message; return false; }
    }
}
