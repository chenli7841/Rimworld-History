using System;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Politics
{
    /// <summary>One-way application of event outcomes into the existing save-backed politics model.</summary>
    public static class HistoricalPoliticalOutcomeService
    {
        private const string EventId = "HE_001_LishanCrisis";
        private const string RoyalPolityId = "HR_State_EasternZhou";

        public static bool HasApplied(string outcomeId) => HistoricalPoliticsManager.HasAppliedHistoricalOutcome(outcomeId);

        public static bool Apply(string outcomeId, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(outcomeId)) return Reject("Outcome ID is required.", out error);
            if (HasApplied(outcomeId)) return true;
            if (!HistoricalEventManager.HasOutcomeCommitted(EventId, outcomeId))
                return Reject("The authoritative HE-001 event state has not committed " + outcomeId + ".", out error);

            if (outcomeId == "HE001_YouWangDead" && !HistoricalFateExecutor.HasExecutedScheduledDeath("HR_Character_ZhouYouWang"))
                return Reject("King You's scheduled fate has not yet been executed.", out error);
            if (outcomeId == "HE001_WesternZhouCollapse" && !HasApplied("HE001_YouWangDead"))
                return Reject("The royal death outcome must be recorded first.", out error);
            if (outcomeId == "HE001_RoyalSuccessionCrisis" && !HasApplied("HE001_WesternZhouCollapse"))
                return Reject("The Western Zhou collapse outcome must be recorded first.", out error);
            if (outcomeId == "HE001_EastwardTransitionUnlocked"
                && (!HasApplied("HE001_WesternZhouCollapse") || !HasApplied("HE001_RoyalSuccessionCrisis")))
                return Reject("The collapse and succession crisis must be applied before eastward transition is unlocked.", out error);

            if (outcomeId == "HE001_WesternZhouCollapse"
                && !HistoricalPoliticsManager.TrySetPolityStatus(RoyalPolityId, HistoricalPolityStatus.Fragmented,
                    HistoricalPoliticalChangeSource.HistoricalEvent, out error)) return false;

            if (!HistoricalPoliticsManager.TryApplyHistoricalOutcome(outcomeId, out error)) return false;
            Log.Message("[Historical RimWorld] Applied political outcome " + outcomeId
                + " for HE-001. The Zhou royal polity and faction remain present; the transition state is "
                + HistoricalPoliticsManager.PoliticalPeriodId + "/" + HistoricalPoliticsManager.SuccessionStatus + ".");
            return true;
        }

        public static bool IsEastwardTransitionUnlocked => HistoricalPoliticsManager.EastwardTransitionUnlocked;
        public static bool IsEastwardMigrationCompleted => HistoricalPoliticsManager.EastwardMigrationCompleted;

        private static bool Reject(string message, out string error) { error = message; return false; }
    }

    /// <summary>HE-002 handoff contract. The event remains owned and scheduled by HR-006.</summary>
    public static class HistoricalEventPrerequisiteService
    {
        public static bool IsEventUnlocked(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return false;
            if (eventId == "HE_002_KingPingEastwardRelocation" || eventId == "HR_Event_KingPingEastwardRelocation")
                return HistoricalPoliticalOutcomeService.IsEastwardTransitionUnlocked
                    && HistoricalPoliticalOutcomeService.HasApplied("HE001_WesternZhouCollapse")
                    && HistoricalPoliticalOutcomeService.HasApplied("HE001_RoyalSuccessionCrisis");
            return ArePrerequisitesSatisfied(eventId);
        }

        public static bool ArePrerequisitesSatisfied(string eventId)
        {
            HistoricalEventDef target = HistoricalEventValidator.FindEventDef(eventId);
            if (target == null) return false;
            foreach (string prerequisiteId in target.prerequisiteEventIds ?? new System.Collections.Generic.List<string>())
                if (!HistoricalEventManager.HasEventCompleted(prerequisiteId)) return false;
            return true;
        }
    }

    public sealed class HistoricalPoliticalOutcomeGameComponent : GameComponent
    {
        private const string EventId = "HE_001_LishanCrisis";
        private int lastTick = -1;

        public HistoricalPoliticalOutcomeGameComponent(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (!HistoricalScenarioManager.IsScenarioActive || HistoricalScenarioManager.ActiveScenarioDefName != "HR_Scenario_EasternZhou_770BCE"
                || Find.TickManager == null || Find.TickManager.TicksGame % 250 != 0 || lastTick == Find.TickManager.TicksGame) return;
            lastTick = Find.TickManager.TicksGame;

            ApplyWhenCommitted("HE001_YouWangDead");
            ApplyWhenCommitted("HE001_WesternZhouCollapse");
            ApplyWhenCommitted("HE001_RoyalSuccessionCrisis");
            ApplyWhenCommitted("HE001_EastwardTransitionUnlocked");

        }

        private static void ApplyWhenCommitted(string outcomeId)
        {
            if (!HistoricalEventManager.HasOutcomeCommitted(EventId, outcomeId)
                || HistoricalPoliticalOutcomeService.HasApplied(outcomeId)) return;
            if (outcomeId == "HE001_YouWangDead"
                && !HistoricalFateExecutor.HasExecutedScheduledDeath("HR_Character_ZhouYouWang")) return;
            if (!HistoricalPoliticalOutcomeService.Apply(outcomeId, out string error))
                Log.Warning("[Historical RimWorld] HE-001 political outcome " + outcomeId + " is pending: " + error);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastTick, "he001PoliticalLastTick", -1);
        }
    }
}
