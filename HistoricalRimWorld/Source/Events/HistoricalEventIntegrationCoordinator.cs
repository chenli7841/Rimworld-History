using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Events
{
    /// <summary>
    /// Reconciles HE-001 through its existing owners. This class owns no timeline, Pawn, Quest, or
    /// political state; it only asks the existing systems to re-apply already committed work safely.
    /// </summary>
    public static class HistoricalEventIntegrationCoordinator
    {
        private const string EventId = "HE_001_LishanCrisis";
        private const string ScenarioId = "HR_Scenario_EasternZhou_770BCE";
        private static readonly HashSet<string> reportedIssues = new HashSet<string>(StringComparer.Ordinal);

        public static void ReconcileAfterLoad()
        {
            if (!HistoricalScenarioManager.IsScenarioActive
                || HistoricalScenarioManager.ActiveScenarioDefName != ScenarioId) return;
            ReconcileEvent(EventId);
            Log.Message("[Historical RimWorld] HE-001 post-load reconciliation completed using saved event, fate, battle, Quest, and politics state.");
        }

        public static void ReconcileEvent(string eventId)
        {
            if (!string.Equals(eventId, EventId, StringComparison.Ordinal)
                || !HistoricalScenarioManager.IsScenarioActive
                || HistoricalScenarioManager.ActiveScenarioDefName != ScenarioId) return;

            HistoricalEventDef def = HistoricalEventValidator.FindEventDef(EventId);
            HistoricalEventRuntimeState state = HistoricalEventManager.GetEvent(EventId);
            if (def == null || state == null)
            {
                ReportOnce("missing-runtime", "HE-001 reconciliation found no registered event Def or saved runtime state; no entities were generated.");
                return;
            }

            if (HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate today))
            {
                foreach (HistoricalEventStageEntry stage in def.stages ?? new List<HistoricalEventStageEntry>())
                {
                    if (stage == null || state.HasReachedStage(stage.stageId)) continue;
                    HistoricalDate scheduled = HistoricalEventDateResolver.ResolveScheduledDate(stage.scheduledDate);
                    if (scheduled <= today)
                    {
                        ReportOnce("due-stage:" + stage.stageId, "HE-001 has a due stage " + stage.stageId
                            + " not yet recorded; the existing HistoricalEventScheduler will catch it up.");
                        break;
                    }
                }
            }

            // Political outcomes are derived only from the event component's committed outcome IDs.
            // The service itself checks fate ordering and its own save-backed applied IDs.
            HistoricalPoliticalOutcomeGameComponent.ReconcileCommittedOutcomes();
            HistoricalEventLongTermQuestBridge.Reconcile(def, state);
        }

        public static void OnHistoricalStageChanged(string eventId, string stageId)
        {
            if (!string.Equals(eventId, EventId, StringComparison.Ordinal)) return;
            ReconcileEvent(eventId);
        }

        public static void OnHistoricalMapActivated(string landmarkId, Map map)
        {
            if (map == null || !string.Equals(landmarkId, "HR_Landmark_Lishan", StringComparison.Ordinal)) return;
            HistoricalArmyPresenceController.TryActivateForMap(map);
        }

        public static void OnHistoricalMapDeactivated(string landmarkId)
        {
            // Pawn, Map, Quest, and Thing references remain owned by RimWorld and their existing
            // save-backed game components. Unloading never resets a battle or synthesizes replacements.
        }

        private static void ReportOnce(string key, string message)
        {
            if (reportedIssues.Add(key)) Log.Warning("[Historical RimWorld] " + message);
        }
    }
}
