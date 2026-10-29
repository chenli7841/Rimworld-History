using System.Linq;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Technology;
using HistoricalRimWorld.World;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Scenarios.EasternZhou
{
    public static class HistoricalEasternZhouScenarioValidator
    {
        public static bool TryValidate(HistoricalPlayableScenarioSetupDef setup, out string error)
        {
            error = null;
            if (setup == null || setup.scenario == null || setup.defaultStartingRegion == null || setup.defaultStartingPolity == null)
                return Fail("Playable scenario setup is missing its scenario, starting region, or starting polity.", out error);
            if (setup.startingPopulation < 1)
                return Fail("Starting population must be positive.", out error);
            if (setup.playableEndAstronomicalYear.HasValue
                && setup.playableEndAstronomicalYear.Value < setup.scenario.startAstronomicalYear)
                return Fail("Playable chapter end date precedes its start date.", out error);
            if (string.IsNullOrWhiteSpace(setup.startingCommunityName))
                return Fail("A starting community ID is required.", out error);
            Scenario selectedScenario = Current.Game?.Scenario;
            if (selectedScenario == null || selectedScenario.name != setup.vanillaScenarioName)
                return Fail("The active vanilla Scenario does not match this historical setup.", out error);
            ScenPart_ConfigPage_ConfigureStartingPawns[] pawnSetup = selectedScenario.AllParts
                .OfType<ScenPart_ConfigPage_ConfigureStartingPawns>().ToArray();
            if (pawnSetup.Length != 1 || pawnSetup[0].pawnCount != setup.startingPopulation)
                return Fail("Vanilla starting-pawn count and historical setup population must match.", out error);
            ScenPart_HistoricalStartingSupplies[] supplyParts = selectedScenario.AllParts
                .OfType<ScenPart_HistoricalStartingSupplies>().ToArray();
            if (supplyParts.Length != 1 || supplyParts[0].supplies != setup.startingSupplies)
                return Fail("The vanilla scenario starting-supplies part does not match the configured supplies Def.", out error);
            if (setup.defaultStartingRegion.defName != setup.scenario.defaultStartingRegion?.defName
                || (setup.scenario.regions != null && !setup.scenario.regions.Any(item => item?.defName == setup.defaultStartingRegion.defName)))
                return Fail("The configured starting region is not the historical scenario's default region.", out error);
            if (setup.startingSupplies?.items == null || setup.startingSupplies.items.Count == 0
                || setup.startingSupplies.items.Any(item => item == null || item.thingDef == null || item.count < 1))
                return Fail("Starting supplies contain a missing ThingDef or invalid count.", out error);
            foreach (string technologyId in setup.startingTechnologyIds ?? Enumerable.Empty<string>())
                if (DefDatabase<HistoricalTechnologyDef>.GetNamedSilentFail(technologyId) == null)
                    return Fail("A configured starting technology Def is missing: " + technologyId + ".", out error);

            var politicalSetups = DefDatabase<HistoricalPoliticalSetupDef>.AllDefsListForReading
                .Where(item => item.scenario?.defName == setup.scenario.defName).ToList();
            if (politicalSetups.Count != 1) return Fail("Expected exactly one political setup for the playable scenario.", out error);
            HistoricalPoliticalSetupDef politics = politicalSetups[0];
            if (politics.communities == null
                || !politics.communities.Any(item => item?.communityId == setup.startingCommunityName
                    && item.regionId == setup.defaultStartingRegion.defName
                    && item.allegiancePolityId == setup.defaultStartingPolity.defName))
                return Fail("Political setup does not contain the configured player community, starting region, and allegiance.", out error);

            if (DefDatabase<HistoricalWorldRevealSetupDef>.AllDefsListForReading
                .Count(item => item.scenario?.defName == setup.scenario.defName) != 1)
                return Fail("Expected exactly one world reveal setup for the playable scenario.", out error);
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            Log.Error("[Historical RimWorld] Eastern Zhou setup validation failed: " + message);
            return false;
        }
    }
}
