using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Scenarios.EasternZhou
{
    public sealed class HistoricalScenarioProgressGameComponent : GameComponent
    {
        public HistoricalScenarioProgressState State = new HistoricalScenarioProgressState();

        public HistoricalScenarioProgressGameComponent(Game game) { }

        public bool Initialize(string scenarioId)
        {
            if (State == null) State = new HistoricalScenarioProgressState();
            if (State.Started) return State.ScenarioId == scenarioId;
            State.ScenarioId = scenarioId;
            State.Started = true;
            return true;
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0 || !HistoricalScenarioProgressManager.IsPlayableScenarioActive) return;
            if (HistoricalScenarioProgressManager.IsEndDateReached && !State.Completed && !State.EndDateNoticeShown)
            {
                State.EndDateNoticeShown = true;
                Messages.Message(Translator.Translate("HR_EZ_EndDateReached"), MessageTypeDefOf.PositiveEvent, false);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref State, "easternZhouScenarioProgress");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && State == null)
                State = new HistoricalScenarioProgressState();
        }
    }

    public static class HistoricalScenarioProgressManager
    {
        public static HistoricalPlayableScenarioSetupDef ActiveSetup => DefDatabase<HistoricalPlayableScenarioSetupDef>.AllDefsListForReading
            .FirstOrDefault(item => item.scenario?.defName == HistoricalScenarioManager.ActiveScenarioDefName);
        private static HistoricalPlayableScenarioSetupDef Setup => ActiveSetup;
        private static HistoricalScenarioProgressGameComponent Component => Current.Game?.GetComponent<HistoricalScenarioProgressGameComponent>();

        public static bool IsPlayableScenarioActive => Setup != null && HistoricalScenarioManager.IsScenarioActive
            && Component?.State?.Started == true && Component.State.ScenarioId == Setup.scenario.defName;
        public static bool IsEndDateReached => IsPlayableScenarioActive && Setup.playableEndAstronomicalYear.HasValue
            && HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)
            && date.AstronomicalYear >= Setup.playableEndAstronomicalYear.Value;
        public static bool IsCompleted => IsPlayableScenarioActive && Component.State.Completed;

        public static bool TryInitialize(HistoricalPlayableScenarioSetupDef setup, out string error)
        {
            error = null;
            if (setup?.scenario == null || Component == null) { error = "Scenario progress component or setup Def is unavailable."; return false; }
            if (!Component.Initialize(setup.scenario.defName)) { error = "Scenario progress is already bound to another scenario."; return false; }
            return true;
        }

        public static bool TryCompleteScenario(out string error)
        {
            error = null;
            if (!IsPlayableScenarioActive) { error = "Eastern Zhou scenario is not active."; return false; }
            if (!IsEndDateReached) { error = "The historical date has not yet reached the chapter end."; return false; }
            if (Component.State.Completed) return true;
            Component.State.Completed = true;
            Component.State.CompletionGameTick = Find.TickManager?.TicksGame;
            Messages.Message(Translator.Translate("HR_EZ_ChapterCompleted"), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }
    }
}
