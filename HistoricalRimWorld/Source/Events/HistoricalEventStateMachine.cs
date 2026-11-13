using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Events
{
    /// <summary>Advances timeline data owned by HR-006's existing event runtime state.</summary>
    public static class HistoricalEventStateMachine
    {
        public static bool AdvanceDueStages(HistoricalEventDef def, HistoricalEventRuntimeState state,
            HistoricalDate currentDate, Action<HistoricalEventRuntimeState, HistoricalDate> markCompleted)
        {
            if (def == null || state == null || def.stages == null || def.stages.Count == 0
                || state.TimelineStatus == HistoricalEventRuntimeStatus.Completed
                || state.TimelineStatus == HistoricalEventRuntimeStatus.Disabled
                || state.TimelineStatus == HistoricalEventRuntimeStatus.Error) return false;

            bool changed = false;
            bool pendingWorkSucceeded = true;
            foreach (HistoricalEventStageEntry stage in def.stages)
            {
                if (stage == null) continue;
                if (state.HasReachedStage(stage.stageId))
                {
                    HistoricalEventStageRecord previous = state.FindStageRecord(stage.stageId);
                    if (previous != null && !ProcessStage(def, stage, previous)) pendingWorkSucceeded = false;
                    continue;
                }
                if (stage.requiresScene && !stage.canProgressOffMap) break;
                HistoricalDate scheduled = HistoricalEventDateResolver.ResolveScheduledDate(stage.scheduledDate);
                if (scheduled > currentDate) break;
                HistoricalEventStageRecord record = state.EnterStage(stage, scheduled, currentDate);
                if (record == null) continue;
                changed = true;
                HistoricalEventObserverRegistry.NotifyStageEntered(def.eventId ?? def.defName, stage.stageId);
                bool stageProcessed = ProcessStage(def, stage, record);
                HistoricalEventIntegrationCoordinator.OnHistoricalStageChanged(def.eventId ?? def.defName, stage.stageId);
                if (!stageProcessed) pendingWorkSucceeded = false;
            }

            if (pendingWorkSucceeded && def.stages.All(stage => stage != null && state.HasReachedStage(stage.stageId)))
            {
                bool allMandatoryCommitted = (def.outcomes ?? new List<HistoricalEventOutcomeEntry>())
                    .Where(outcome => outcome != null && outcome.mandatory)
                    .All(outcome => state.CommittedOutcomeIds.Contains(outcome.outcomeId, StringComparer.Ordinal));
                if (!allMandatoryCommitted) return changed;
                state.SetHistoricalOutcomeCommitted(true);
                state.CompleteTimeline();
                markCompleted?.Invoke(state, currentDate);
                HistoricalEventObserverRegistry.NotifyEventCompleted(def.eventId ?? def.defName);
                changed = true;
            }
            return changed;
        }

        private static bool ProcessStage(HistoricalEventDef def, HistoricalEventStageEntry stage, HistoricalEventStageRecord record)
        {
            bool success = true;
            if (stage.recordInChronicle && !record.ChronicleRecorded)
            {
                HistoricalDateReference date = stage.scheduledDate;
                string descriptionKey = stage.notificationDescriptionKey ?? def.description;
                bool firstChronicleStage = def.stages.FirstOrDefault(candidate => candidate != null && candidate.recordInChronicle)?.stageId == stage.stageId;
                string entryId = firstChronicleStage ? "Event:" + def.defName
                    : "HistoricalStage:" + (def.eventId ?? def.defName) + ":" + stage.stageId;
                if (HistoricalEventChronicleBridge.Record(entryId, def, date, stage.displayLabelKey, descriptionKey))
                    record.MarkChronicleRecorded();
                else success = false;
            }

            if (!record.NotificationHandled)
            {
                // Persist the handled bit before publishing so a load/catch-up cannot spam the player.
                record.MarkNotificationHandled();
                if (stage.showNotification && record.TransitionDate == record.ScheduledDate)
                    HistoricalEventNotificationBridge.Publish(stage.displayLabelKey,
                        stage.notificationDescriptionKey ?? def.description);
            }

            if (!record.OutcomesProcessed)
            {
                foreach (HistoricalEventOutcomeEntry outcome in (def.outcomes ?? new List<HistoricalEventOutcomeEntry>())
                    .Where(item => item != null && item.stageId == stage.stageId))
                {
                    bool firstCommit = HistoricalEventManager.CommitOnce(def.eventId ?? def.defName, outcome.outcomeId);
                    if (outcome.recordInChronicle)
                    {
                        HistoricalDateReference date = stage.scheduledDate;
                        if (!HistoricalEventChronicleBridge.Record("HistoricalOutcome:" + (def.eventId ?? def.defName) + ":" + outcome.outcomeId,
                            def, date, outcome.titleKey, outcome.descriptionKey)) { success = false; continue; }
                    }
                    if (firstCommit) Log.Message("[Historical RimWorld] Committed historical outcome " + outcome.outcomeId + " for " + def.defName + ".");
                }
                if (success) record.MarkOutcomesProcessed();
            }
            return success && (!stage.recordInChronicle || record.ChronicleRecorded) && record.OutcomesProcessed;
        }
    }

    public interface IHistoricalEventObserver
    {
        void OnHistoricalStageEntered(string eventId, string stageId);
        void OnHistoricalEventCompleted(string eventId);
    }

    public static class HistoricalEventObserverRegistry
    {
        private static readonly List<IHistoricalEventObserver> observers = new List<IHistoricalEventObserver>();
        public static void Register(IHistoricalEventObserver observer)
        {
            if (observer != null && !observers.Contains(observer)) observers.Add(observer);
        }
        public static void Unregister(IHistoricalEventObserver observer) => observers.Remove(observer);
        internal static void NotifyStageEntered(string eventId, string stageId) => Notify(observer => observer.OnHistoricalStageEntered(eventId, stageId));
        internal static void NotifyEventCompleted(string eventId) => Notify(observer => observer.OnHistoricalEventCompleted(eventId));
        private static void Notify(Action<IHistoricalEventObserver> callback)
        {
            foreach (IHistoricalEventObserver observer in observers.ToArray())
                try { callback(observer); }
                catch (Exception exception) { Log.Error("[Historical RimWorld] Historical event observer failed: " + exception); }
        }
    }

    public static class HistoricalEventChronicleBridge
    {
        public static bool Record(string entryId, HistoricalEventDef def, HistoricalDateReference date, string titleKey, string descriptionKey)
        {
            if (HistoricalChronicleManager.Entries.Any(entry => entry != null && entry.EntryId == entryId)) return true;
            if (HistoricalChronicleManager.TryAddCustomEventEntry(entryId, def.defName, date, titleKey, descriptionKey,
                def.regionId, def.polityId, def.evidenceLevel, def.sourceNotes, def.category, def.scope,
                HistoricalEventManager.IsVisible(def), out string error)) return true;
            Log.Error("[Historical RimWorld] Could not record event chronicle entry " + entryId + ": " + error);
            return false;
        }
    }

    public static class HistoricalEventNotificationBridge
    {
        public static void Publish(string titleKey, string descriptionKey)
        {
            string title = Translator.Translate(titleKey);
            string description = Translator.Translate(descriptionKey);
            Messages.Message(title + " — " + description, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
