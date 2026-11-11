using LudeonTK;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    public static class ForestEcologyDebugActions
    {
        [DebugAction("Historical RimWorld", "HR Forest: Rebuild all regions", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RebuildAll()
        {
            Find.CurrentMap?.GetComponent<MapComponent_ForestEcology>()?.RequestFullRebuild();
        }

        [DebugAction("Historical RimWorld", "HR Forest: Run road connectivity checks", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunConnectivityChecks()
        {
            bool passed = ForestConnectivityUtility.RunCompatibilitySelfTest(out string report);
            if (passed) Log.Message("[Historical RimWorld][HR-046] Connectivity checks passed: " + report);
            else Log.Error("[Historical RimWorld][HR-046] Connectivity checks failed: " + report);
        }

        [DebugAction("Historical RimWorld", "HR Forest: Print map statistics", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PrintStatistics()
        {
            MapComponent_ForestEcology ecology = Find.CurrentMap?.GetComponent<MapComponent_ForestEcology>();
            if (ecology == null)
            {
                Log.Warning("[Historical RimWorld][HR-046] No forest ecology component on the current map.");
                return;
            }
            Log.Message("[Historical RimWorld][HR-046] regions=" + ecology.RegionCount
                + "; known=" + ecology.GetKnownRegions().Count
                + "; forest area=" + ecology.GetTotalForestArea()
                + "; trees=" + ecology.GetTotalTreeCount()
                + "; dirty=" + ecology.DirtyRegionCount
                + "; last rebuild=" + ecology.LastRebuildMilliseconds + " ms"
                + "; processed vegetation=" + ecology.LastProcessedVegetationCount);
            foreach (ForestEcologyRegion region in ecology.GetAllRegions())
                Log.Message("[Historical RimWorld][HR-046] Forest #" + region.RegionId
                    + ": area=" + region.CellCount + ", trees=" + region.TreeCount
                    + ", integrity=" + region.EcologicalIntegrity.ToString("F1")
                    + ", capacity=" + region.HabitatCapacity.ToString("F2")
                    + ", tier=" + region.HabitatCapacityTier);
        }

        [DebugAction("Historical RimWorld", "HR Forest: Toggle explored-region overlay", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleOverlay()
        {
            Find.CurrentMap?.GetComponent<MapComponent_ForestEcology>()?.ToggleAllKnownRegionHighlight();
        }

        [DebugAction("Historical RimWorld", "HR Wildlife: Rebuild populations", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RebuildWildlife()
        {
            MapComponent_ForestWildlife wildlife = Find.CurrentMap?.GetComponent<MapComponent_ForestWildlife>();
            if (wildlife == null)
            {
                Log.Warning("[Historical RimWorld][HR-047] No wildlife component on the current map.");
                return;
            }
            wildlife.RequestRefresh();
            Log.Message("[Historical RimWorld][HR-047] Requested a wildlife population refresh.");
        }

        [DebugAction("Historical RimWorld", "HR Wildlife: Print population stats", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PrintWildlifeStats()
        {
            MapComponent_ForestWildlife wildlife = Find.CurrentMap?.GetComponent<MapComponent_ForestWildlife>();
            if (wildlife == null)
            {
                Log.Warning("[Historical RimWorld][HR-047] No wildlife component on the current map.");
                return;
            }
            Log.Message("[Historical RimWorld][HR-047] configured wild animals=" + wildlife.WildConfiguredAnimalCount
                + "; tracked populations=" + wildlife.Populations.Count
                + "; under migration pressure=" + wildlife.MigratingPopulationCount);
            foreach (ForestWildlifePopulation population in wildlife.Populations)
            {
                ThingDef species = DefDatabase<ThingDef>.GetNamedSilentFail(population.SpeciesDefName);
                Log.Message("[Historical RimWorld][HR-047] " + (species?.LabelCap.ToString() ?? population.SpeciesDefName)
                    + "; region=" + population.ForestRegionId + "; current=" + population.CurrentCount
                    + "; target=" + population.TargetCount + "; food availability=" + population.FoodAvailability.ToString("F2")
                    + "; habitat suitability=" + population.HabitatSuitability.ToString("F2")
                    + "; disturbance=" + population.DisturbanceLevel.ToString("F2")
                    + "; migration pressure=" + population.MigrationPressure);
            }
        }
    }
}
