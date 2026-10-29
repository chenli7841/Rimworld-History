using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Events.Actions;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalEventScheduler
    {
        private readonly HistoricalEventGameComponent component;
        private readonly HistoricalScenarioDef scenario;
        private readonly Dictionary<string, HistoricalEventDef> eventById;

        public HistoricalEventScheduler(HistoricalEventGameComponent owner, string scenarioId)
        {
            component = owner;
            scenario = HistoricalScenarioRegistry.TryGetById(scenarioId, out HistoricalScenarioDef found) ? found : null;
            eventById = DefDatabase<HistoricalEventDef>.AllDefsListForReading
                .Where(item => item.scenario != null && item.scenario.defName == scenarioId)
                .ToDictionary(item => item.defName, StringComparer.Ordinal);
        }

        public void ProcessDueEvents(HistoricalDate currentDate)
        {
            if (scenario == null || !HistoricalCalendarManager.IsActive) return;
            List<HistoricalEventDef> due = new List<HistoricalEventDef>();
            foreach (string id in component.PendingEventIds.ToList())
            {
                HistoricalEventDef item;
                HistoricalEventRuntimeState state = component.FindState(id);
                if (!eventById.TryGetValue(id, out item))
                {
                    if (state != null) Fail(state, HistoricalEventFailureKind.MissingDefinition,
                        "Event Def is missing from the active scenario: " + id + ".", currentDate);
                    else Log.Error("[Historical RimWorld] Pending event ID has no runtime state: " + id + ".");
                    continue;
                }
                if (state == null)
                {
                    Log.Error("[Historical RimWorld] Event Def " + id + " has no runtime state; it was removed from the pending index.");
                    component.RemovePendingId(id);
                    continue;
                }

                HistoricalDate scheduled = HistoricalEventDateResolver.ResolveScheduledDate(item.historicalDate);
                if (scheduled <= currentDate) due.Add(item);
            }

            List<HistoricalEventDef> orderedDue = due
                .OrderBy(def => HistoricalEventDateResolver.ResolveScheduledDate(def.historicalDate))
                .ThenBy(def => def.priority)
                .ThenBy(def => def.defName, StringComparer.Ordinal).ToList();
            bool progress;
            do
            {
                progress = false;
                foreach (HistoricalEventDef item in orderedDue)
                {
                    HistoricalEventRuntimeState state = component.FindState(item.defName);
                    if (state == null || state.Status != HistoricalEventExecutionStatus.Pending) continue;
                    if (state.LastAttemptAbsoluteDay == currentDate.AbsoluteDay
                        && (state.FailureKind != HistoricalEventFailureKind.WaitingForPrerequisite
                            || !PrerequisitesSettled(item))) continue;
                    try
                    {
                        ProcessEvent(item, state, currentDate);
                    }
                    catch (Exception exception)
                    {
                        Fail(state, HistoricalEventFailureKind.ExecutionException, exception.ToString(), currentDate);
                    }
                    if (state.Status != HistoricalEventExecutionStatus.Pending) progress = true;
                }
            } while (progress);
        }

        private void ProcessEvent(HistoricalEventDef item, HistoricalEventRuntimeState state, HistoricalDate currentDate)
        {
            long day = currentDate.AbsoluteDay;
            state.RecordAttempt(day);
            if (item.activationMode == HistoricalEventActivationMode.DevelopmentOnly && !Prefs.DevMode)
            {
                component.RecordPendingAttempt(state, day, HistoricalEventFailureKind.WaitingForPrerequisite,
                    "Waiting for developer mode; development-only event was not executed.");
                return;
            }

            HistoricalEventContext context = new HistoricalEventContext(scenario, item, currentDate,
                HistoricalEventDateResolver.ResolveScheduledDate(item.historicalDate));
            foreach (string prerequisiteId in item.prerequisiteEventIds ?? Enumerable.Empty<string>())
            {
                HistoricalEventRuntimeState prerequisite = component.FindState(prerequisiteId);
                if (prerequisite == null)
                {
                    Fail(state, HistoricalEventFailureKind.MissingDefinition, "Missing prerequisite runtime state " + prerequisiteId + ".", currentDate);
                    return;
                }
                if (prerequisite.Status == HistoricalEventExecutionStatus.Failed || prerequisite.Status == HistoricalEventExecutionStatus.Skipped)
                {
                    if (prerequisite.Status == HistoricalEventExecutionStatus.Skipped
                        && DefDatabase<HistoricalEventDef>.GetNamedSilentFail(prerequisiteId)?.activationMode == HistoricalEventActivationMode.InitialBackground) continue;
                    Fail(state, HistoricalEventFailureKind.MissingDefinition,
                        "Prerequisite " + prerequisiteId + " did not execute successfully (" + prerequisite.Status + ").", currentDate);
                    return;
                }
                if (prerequisite.Status != HistoricalEventExecutionStatus.Executed)
                {
                    component.RecordPendingAttempt(state, day, HistoricalEventFailureKind.WaitingForPrerequisite,
                        "Waiting for prerequisite " + prerequisiteId + ".");
                    return;
                }
            }

            foreach (HistoricalEventCondition condition in item.conditions ?? new List<HistoricalEventCondition>())
            {
                if (condition == null)
                {
                    Fail(state, HistoricalEventFailureKind.InvalidAction, "Event contains a null condition.", currentDate);
                    return;
                }
                if (!condition.Validate(context, out string error))
                {
                    Fail(state, HistoricalEventFailureKind.MissingDefinition, error, currentDate);
                    return;
                }
                if (!condition.Evaluate(context))
                {
                    component.RecordPendingAttempt(state, day, HistoricalEventFailureKind.ConditionNotMet,
                        "A temporary condition is not satisfied; it will be checked again on the next historical day.");
                    return;
                }
            }

            List<HistoricalEventAction> actions = (item.actions ?? new List<HistoricalEventAction>()).ToList();
            foreach (HistoricalEventAction action in actions)
            {
                if (action == null)
                {
                    Fail(state, HistoricalEventFailureKind.InvalidAction, "Event contains a null action.", currentDate);
                    return;
                }
                if (!action.Validate(context, out string actionError))
                {
                    Fail(state, HistoricalEventFailureKind.InvalidAction, actionError, currentDate);
                    return;
                }
            }
            if (actions.Count(action => action.IsStateMutation) > 1)
            {
                Fail(state, HistoricalEventFailureKind.InvalidAction, "More than one state mutation is outside the supported atomicity boundary.", currentDate);
                return;
            }

            bool actionEffectCommitted = false;
            try
            {
                if (!HistoricalChronicleManager.IsActive)
                {
                    Fail(state, HistoricalEventFailureKind.MissingDefinition, "Historical Chronicle component is unavailable.", currentDate);
                    return;
                }
                HistoricalEventAction mutation = actions.FirstOrDefault(action => action.IsStateMutation);
                if (mutation != null && !mutation.Execute(context, out string mutationError))
                {
                    Fail(state, HistoricalEventFailureKind.InvalidAction, mutationError, currentDate);
                    return;
                }
                if (mutation != null) actionEffectCommitted = true;

                foreach (HistoricalEventAction action in actions.Where(action => !action.IsStateMutation))
                {
                    if (!action.Execute(context, out string followupError))
                    {
                        Fail(state, HistoricalEventFailureKind.InvalidAction, followupError, currentDate);
                        return;
                    }
                    actionEffectCommitted = true;
                }

                bool visible = HistoricalEventManager.IsVisible(item);
                if (item.recordInChronicle && !actions.Any(action => action is Action_AddChronicleEntry))
                {
                    if (!HistoricalChronicleManager.TryAddEventEntry(item, item.historicalDate, visible, out string chronicleError))
                    {
                        Fail(state, HistoricalEventFailureKind.ExecutionException, chronicleError, currentDate);
                        return;
                    }
                    actionEffectCommitted = true;
                }

                component.CompleteState(state, HistoricalEventExecutionStatus.Executed, HistoricalEventFailureKind.None, null, currentDate);
                PublishNotification(item, visible);
            }
            catch (Exception exception)
            {
                if (actionEffectCommitted)
                {
                    string reason = "A state or chronicle action committed, but follow-up processing failed: " + exception;
                    component.CompleteState(state, HistoricalEventExecutionStatus.Executed, HistoricalEventFailureKind.None, reason, currentDate);
                    Log.Error("[Historical RimWorld] Event " + item.defName + " committed with a follow-up failure: " + exception);
                }
                else Fail(state, HistoricalEventFailureKind.ExecutionException, exception.ToString(), currentDate);
            }
        }

        private void PublishNotification(HistoricalEventDef item, bool visible)
        {
            if (!visible || item.notificationPolicy == HistoricalEventNotificationPolicy.Silent
                || item.notificationPolicy == HistoricalEventNotificationPolicy.ChronicleOnly) return;
            string title = Translator.Translate(item.label);
            string description = Translator.Translate(item.description);
            if (item.notificationPolicy == HistoricalEventNotificationPolicy.Message)
                Messages.Message(title + " — " + description, MessageTypeDefOf.PositiveEvent, false);
            else if (item.notificationPolicy == HistoricalEventNotificationPolicy.Letter && Find.LetterStack != null)
                Find.LetterStack.ReceiveLetter(title, description, LetterDefOf.PositiveEvent);
        }

        private bool PrerequisitesReady(HistoricalEventDef item)
        {
            foreach (string id in item.prerequisiteEventIds ?? Enumerable.Empty<string>())
            {
                HistoricalEventRuntimeState state = component.FindState(id);
                if (state == null) return false;
                if (state.Status == HistoricalEventExecutionStatus.Executed) continue;
                if (state.Status == HistoricalEventExecutionStatus.Skipped
                    && DefDatabase<HistoricalEventDef>.GetNamedSilentFail(id)?.activationMode == HistoricalEventActivationMode.InitialBackground) continue;
                return false;
            }
            return true;
        }

        private bool PrerequisitesSettled(HistoricalEventDef item)
        {
            foreach (string id in item.prerequisiteEventIds ?? Enumerable.Empty<string>())
            {
                HistoricalEventRuntimeState state = component.FindState(id);
                if (state == null || state.Status == HistoricalEventExecutionStatus.Pending) return false;
            }
            return true;
        }

        private void Fail(HistoricalEventRuntimeState state, HistoricalEventFailureKind kind, string reason, HistoricalDate date)
        {
            component.CompleteState(state, HistoricalEventExecutionStatus.Failed, kind, reason, date);
            Log.Error("[Historical RimWorld] Historical event failed: " + state.EventId + ": " + reason);
        }
    }
}
