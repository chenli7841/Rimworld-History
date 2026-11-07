using System.Collections.Generic;
using RimWorld;
using Verse;
using HistoricalRimWorld.Patches.Technology;

namespace HistoricalRimWorld.Technology
{
    /// <summary>A native RimWorld letter that opens the historical research tab at its technology node.</summary>
    public sealed class ChoiceLetter_HistoricalTechnology : ChoiceLetter
    {
        public string ResearchDefName;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(ResearchDefName);
                if (project != null)
                {
                    DiaOption view = new DiaOption("HR_Tech_Letter_View".Translate());
                    view.action = () =>
                    {
                        MainButtonDef researchButton = MainButtonDefOf.Research;
                        Find.MainTabsRoot?.SetCurrentTab(researchButton);
                        MainTabWindow_Research researchWindow = researchButton?.TabWindow as MainTabWindow_Research;
                        ResearchTabDef historicalTab = DefDatabase<ResearchTabDef>.GetNamedSilentFail(
                            HistoricalResearchTimelineUIState.TabDefName);
                        if (researchWindow != null && historicalTab != null)
                        {
                            researchWindow.CurTab = historicalTab;
                            researchWindow.Select(project);
                            HistoricalResearchTimelineUIState.FocusProject(project.defName);
                        }
                    };
                    yield return view;
                }
                yield return Option_Close;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ResearchDefName, "historicalTechnologyResearchDefName");
        }
    }
}
