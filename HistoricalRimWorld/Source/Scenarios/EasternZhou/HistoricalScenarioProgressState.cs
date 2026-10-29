using Verse;

namespace HistoricalRimWorld.Scenarios.EasternZhou
{
    public sealed class HistoricalScenarioProgressState : IExposable
    {
        public string ScenarioId;
        public bool Started;
        public bool Completed;
        public bool EndDateNoticeShown;
        public int? CompletionGameTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ScenarioId, "scenarioId");
            Scribe_Values.Look(ref Started, "started", false);
            Scribe_Values.Look(ref Completed, "completed", false);
            Scribe_Values.Look(ref EndDateNoticeShown, "endDateNoticeShown", false);
            Scribe_Values.Look(ref CompletionGameTick, "completionGameTick");
        }
    }
}
