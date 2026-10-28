using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.UI
{
    public sealed class MainTabWindow_HistoricalCalendar : MainTabWindow
    {
        private Vector2 scrollPosition;
        private bool scenarioInitializationFailed;

        public override void DoWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, CalculateContentHeight(inRect));
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            listing.Label(Translator.Translate("HR_Calendar_Title"));
            listing.Gap();

            DrawCalendarSection(listing);
            listing.GapLine();
            DrawScenarioSection(listing);
            if (scenarioInitializationFailed && !HistoricalScenarioManager.IsScenarioActive)
            {
                listing.Label(Translator.Translate("HR_Scenario_InitializationFailed"));
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawCalendarSection(Listing_Standard listing)
        {
            HistoricalCalendarGameComponent component = HistoricalCalendarManager.GetCurrentComponent();
            if (component == null || !component.Initialized)
            {
                listing.Label(Translator.Translate("HR_Calendar_Inactive"));
                listing.Gap();
                listing.Label(Translator.Translate("HR_Calendar_InactiveHelp"));
                if (listing.ButtonText(Translator.Translate("HR_Calendar_EnableButton"))
                    && component != null && component.TryInitializeDefaultCalendar())
                {
                    Log.Message("[Historical RimWorld] Historical calendar enabled for this save.");
                }

                return;
            }

            HistoricalDate currentDate;
            HistoricalDate startDate;
            if (!component.TryGetCurrentDate(out currentDate) || !component.TryGetStartDate(out startDate))
            {
                listing.Label(Translator.Translate("HR_Calendar_Inactive"));
                return;
            }

            listing.Label(Translator.Translate("HR_Calendar_CurrentDate"));
            listing.Label(HistoricalDateFormatter.Format(currentDate));
            listing.Gap();
            listing.Label(Translator.Translate("HR_Calendar_Rules"));
            listing.Label(string.Format(Translator.Translate("HR_Calendar_ElapsedDays"), startDate.DaysUntil(currentDate)));

            if (HistoricalCalendarDebugVisibility.ShouldShow())
            {
                listing.Gap();
                listing.Label(Translator.Translate("HR_Calendar_DebugTitle"));
                listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugCurrentTick"), Find.TickManager.TicksGame));
                listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugStartTick"), component.StartGameTick));
                listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugElapsedDays"), startDate.DaysUntil(currentDate)));
                listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugAstronomicalYear"), currentDate.AstronomicalYear));
                listing.Label(string.Format(Translator.Translate("HR_Calendar_DebugAbsoluteDay"), currentDate.AbsoluteDay));
            }
        }

        private void DrawScenarioSection(Listing_Standard listing)
        {
            if (!HistoricalScenarioManager.IsScenarioActive)
            {
                listing.Label(Translator.Translate("HR_Scenario_NoActive"));
                if (Prefs.DevMode)
                {
                    listing.Gap();
                    listing.Label(Translator.Translate("HR_Scenario_SelectHeading"));
                    foreach (HistoricalScenarioDef scenario in HistoricalScenarioRegistry.AllScenarios)
                    {
                        if (listing.ButtonText(scenario.LabelCap))
                        {
                            string error;
                            if (!HistoricalScenarioManager.TryInitializeScenario(scenario, out error))
                            {
                                scenarioInitializationFailed = true;
                            }
                            else
                            {
                                scenarioInitializationFailed = false;
                            }
                        }
                    }
                }

                return;
            }

            HistoricalScenarioDef currentScenario = HistoricalScenarioManager.CurrentScenario;
            if (currentScenario == null)
            {
                listing.Label(string.Format(
                    Translator.Translate("HR_Scenario_StoredMissing"),
                    HistoricalScenarioManager.ActiveScenarioDefName ?? string.Empty));
                return;
            }

            listing.Label(Translator.Translate("HR_Scenario_Current"));
            listing.Label(currentScenario.LabelCap);
            listing.Gap();
            listing.Label(Translator.Translate("HR_Scenario_StartDate"));
            listing.Label(HistoricalDateFormatter.Format(currentScenario.GetStartDate()));
            listing.Label(Translator.Translate("HR_Scenario_Period"));
            listing.Label(string.IsNullOrWhiteSpace(currentScenario.historicalPeriod)
                ? Translator.Translate("HR_Scenario_UnknownPeriod")
                : Translator.Translate(currentScenario.historicalPeriod));
            DrawDefList(listing, "HR_Scenario_States", HistoricalDataQuery.GetStates(currentScenario));
            DrawDefList(listing, "HR_Scenario_Regions", HistoricalDataQuery.GetRegions(currentScenario));
            DrawDefList(listing, "HR_Scenario_Settlements", HistoricalDataQuery.GetSettlements(currentScenario));
        }

        private static void DrawDefList(Listing_Standard listing, string headerKey, IEnumerable<Def> defs)
        {
            listing.Gap();
            listing.Label(Translator.Translate(headerKey));
            string separator = Translator.Translate("HR_Scenario_ListSeparator");
            listing.Label(string.Join(separator, defs.Where(def => def != null).Select(def => def.LabelCap)));
        }

        private static float CalculateContentHeight(Rect inRect)
        {
            float lines = 16f;
            HistoricalCalendarGameComponent calendar = HistoricalCalendarManager.GetCurrentComponent();
            if (calendar != null && calendar.Initialized && HistoricalCalendarDebugVisibility.ShouldShow())
            {
                lines += 6f;
            }

            if (HistoricalScenarioManager.IsScenarioActive)
            {
                HistoricalScenarioDef scenario = HistoricalScenarioManager.CurrentScenario;
                if (scenario != null)
                {
                    lines += 14f + HistoricalDataQuery.GetStates(scenario).Count()
                        + HistoricalDataQuery.GetRegions(scenario).Count()
                        + HistoricalDataQuery.GetSettlements(scenario).Count();
                }
            }
            else if (Prefs.DevMode)
            {
                lines += 3f + HistoricalScenarioRegistry.AllScenarios.Count();
            }

            return Math.Max(inRect.height, lines * 28f);
        }
    }
}
