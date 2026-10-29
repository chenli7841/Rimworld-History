using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Events
{
    public sealed class HistoricalLocalEventRuntimeRecord : IExposable
    {
        public string eventId;
        public int appearances;
        public long nextEligibleAbsoluteDay;
        public int triggerCount;
        public int lastTriggerGameTick = -1;
        public bool isActive;
        public List<string> executedChoiceIds = new List<string>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref eventId, "eventId");
            Scribe_Values.Look(ref appearances, "appearances", 0);
            Scribe_Values.Look(ref nextEligibleAbsoluteDay, "nextEligibleAbsoluteDay", 0L);
            Scribe_Values.Look(ref triggerCount, "triggerCount", 0);
            Scribe_Values.Look(ref lastTriggerGameTick, "lastTriggerGameTick", -1);
            Scribe_Values.Look(ref isActive, "isActive", false);
            Scribe_Collections.Look(ref executedChoiceIds, "executedChoiceIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) executedChoiceIds = executedChoiceIds ?? new List<string>();
        }
    }

    public sealed class HistoricalStorylineRuntimeState : IExposable
    {
        public string storylineId;
        public int currentStage;
        public bool ended;
        public string outcome;
        public string communityId;
        public long startedAbsoluteDay = long.MinValue;
        public List<string> completedStageIds = new List<string>();
        public List<string> failedStageIds = new List<string>();
        public List<string> selectedChoiceIds = new List<string>();
        public bool cancelled;
        public string familyId;
        public bool archiveExported;
        public string archiveFileName;
        public void ExposeData()
        {
            Scribe_Values.Look(ref storylineId, "storylineId");
            Scribe_Values.Look(ref currentStage, "currentStage", 0);
            Scribe_Values.Look(ref ended, "ended", false);
            Scribe_Values.Look(ref outcome, "outcome");
            Scribe_Values.Look(ref communityId, "communityId");
            Scribe_Values.Look(ref startedAbsoluteDay, "startedAbsoluteDay", long.MinValue);
            Scribe_Collections.Look(ref completedStageIds, "completedStageIds", LookMode.Value);
            Scribe_Collections.Look(ref failedStageIds, "failedStageIds", LookMode.Value);
            Scribe_Collections.Look(ref selectedChoiceIds, "selectedChoiceIds", LookMode.Value);
            Scribe_Values.Look(ref cancelled, "cancelled", false);
            Scribe_Values.Look(ref familyId, "familyId");
            Scribe_Values.Look(ref archiveExported, "archiveExported", false);
            Scribe_Values.Look(ref archiveFileName, "archiveFileName");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                completedStageIds = completedStageIds ?? new List<string>();
                failedStageIds = failedStageIds ?? new List<string>();
                selectedChoiceIds = selectedChoiceIds ?? new List<string>();
            }
        }
    }
}
