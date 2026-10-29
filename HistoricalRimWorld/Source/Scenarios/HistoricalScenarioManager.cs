using System;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.World;
using HistoricalRimWorld.Technology;
using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public static class HistoricalScenarioManager
    {
        public static bool IsScenarioActive
        {
            get
            {
                HistoricalScenarioGameComponent component = GetCurrentComponent();
                return component != null && component.Initialized;
            }
        }

        public static HistoricalScenarioDef CurrentScenario
        {
            get
            {
                HistoricalScenarioGameComponent component = GetCurrentComponent();
                HistoricalScenarioDef scenario;
                return component != null && component.Initialized
                    && HistoricalScenarioRegistry.TryGetById(component.ActiveScenarioDefName, out scenario)
                    ? scenario
                    : null;
            }
        }

        public static string ActiveScenarioDefName => GetCurrentComponent()?.ActiveScenarioDefName;

        public static bool TryInitializeScenario(HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (scenario == null)
            {
                error = "No historical scenario was selected.";
                return Fail(error);
            }

            if (Current.Game == null)
            {
                error = "A game must be loaded before a historical scenario can be initialized.";
                return Fail(error);
            }

            HistoricalScenarioDef registeredScenario;
            if (!HistoricalScenarioRegistry.TryGetById(scenario.defName, out registeredScenario)
                || !ReferenceEquals(registeredScenario, scenario))
            {
                error = "The selected scenario is not a loaded Def in the current database.";
                return Fail(error);
            }

            HistoricalScenarioGameComponent scenarioComponent = Current.Game.GetComponent<HistoricalScenarioGameComponent>();
            HistoricalCalendarGameComponent calendarComponent = Current.Game.GetComponent<HistoricalCalendarGameComponent>();
            if (scenarioComponent == null || calendarComponent == null)
            {
                error = "Required historical game components are unavailable.";
                return Fail(error);
            }

            if (scenarioComponent.Initialized)
            {
                if (string.Equals(scenarioComponent.ActiveScenarioDefName, scenario.defName, StringComparison.Ordinal))
                {
                    HistoricalDate existingStartDate;
                    HistoricalDate requestedStartDate;
                    try
                    {
                        requestedStartDate = scenario.GetStartDate();
                    }
                    catch (ArgumentOutOfRangeException exception)
                    {
                        error = "Scenario start date is invalid: " + exception.ParamName + ".";
                        return Fail(error);
                    }

                    if (calendarComponent.Initialized
                        && calendarComponent.TryGetStartDate(out existingStartDate)
                        && existingStartDate == requestedStartDate)
                    {
                        if (!HistoricalPoliticsManager.TryInitializeForScenario(scenario, out error))
                        {
                            return Fail(error);
                        }
                        if (!HistoricalWorldRevealManager.TryInitializeForScenario(scenario, out error))
                        {
                            return Fail(error);
                        }
                        if (!HistoricalEventManager.TryInitializeForScenario(scenario, out error)) return Fail(error);
                        if (!HistoricalGenealogyManager.TryInitializeForScenario(scenario.defName, out error)) return Fail(error);
                        if (!HistoricalTechnologyManager.TryInitializeForScenario(scenario, out error)) return Fail(error);
                        return true;
                    }

                    error = "The scenario is already bound, but its historical clock is unavailable or inconsistent. The save was left unchanged.";
                    return Fail(error);
                }

                error = "This save is already bound to scenario " + scenarioComponent.ActiveScenarioDefName + ". Scenario switching is not supported.";
                return Fail(error);
            }

            string[] validationErrors = HistoricalDefValidator.ValidateScenario(scenario).ToArray();
            if (validationErrors.Length > 0)
            {
                error = string.Join(Environment.NewLine, validationErrors);
                return Fail(error);
            }

            HistoricalDate startDate;
            try
            {
                startDate = scenario.GetStartDate();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                error = "Scenario start date is invalid: " + exception.ParamName + ".";
                return Fail(error);
            }

            if (!calendarComponent.CanInitializeCalendar(startDate))
            {
                error = "The historical clock is already initialized with a different start date, or the game tick manager is unavailable.";
                return Fail(error);
            }

            HistoricalPoliticsInitializationPlan politicsPlan;
            if (!HistoricalPoliticsManager.TryPrepareScenarioInitialization(scenario, out politicsPlan, out error))
            {
                return Fail(error);
            }

            HistoricalWorldRevealInitializationPlan revealPlan;
            if (!HistoricalWorldRevealManager.TryPrepareScenarioInitialization(scenario, out revealPlan, out error))
            {
                return Fail(error);
            }

            if (!HistoricalEventManager.ValidateScenarioDefinitions(scenario, out error)) return Fail(error);

            if (!calendarComponent.TryInitializeCalendar(startDate))
            {
                error = "The historical clock could not be initialized.";
                return Fail(error);
            }

            if (!scenarioComponent.TryBindScenario(scenario.defName))
            {
                error = "The save could not be bound to the selected scenario.";
                return Fail(error);
            }

            if (!HistoricalPoliticsManager.TryCommitScenarioInitialization(politicsPlan, out error))
            {
                return Fail("Scenario and clock were initialized, but political setup could not be committed: " + error);
            }

            if (!HistoricalWorldRevealManager.TryCommitScenarioInitialization(revealPlan, out error))
            {
                return Fail("Scenario and clock were initialized, but world reveal setup could not be committed: " + error);
            }

            if (!HistoricalEventManager.TryInitializeForScenario(scenario, out error))
            {
                return Fail("Scenario, clock, politics, and reveal state were initialized, but the event engine could not be initialized: " + error);
            }

            if (!HistoricalGenealogyManager.TryInitializeForScenario(scenario.defName, out error))
                return Fail("Scenario, clock, politics, reveal, and events were initialized, but genealogy could not be initialized: " + error);

            if (!HistoricalTechnologyManager.TryInitializeForScenario(scenario, out error))
                return Fail("Scenario systems were initialized, but historical technology could not be initialized: " + error);

            Log.Message("[Historical RimWorld] Initialized historical scenario " + scenario.defName + ".");
            return true;
        }

        private static HistoricalScenarioGameComponent GetCurrentComponent()
        {
            return Current.Game == null
                ? null
                : Current.Game.GetComponent<HistoricalScenarioGameComponent>();
        }

        private static bool Fail(string error)
        {
            Log.Error("[Historical RimWorld] Scenario initialization failed: " + error);
            return false;
        }
    }
}
