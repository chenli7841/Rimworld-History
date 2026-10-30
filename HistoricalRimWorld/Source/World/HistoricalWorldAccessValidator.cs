using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.World
{
    public static class HistoricalWorldAccessValidator
    {
        public static IEnumerable<string> ValidateSetup(HistoricalWorldRevealSetupDef setup)
        {
            if (setup == null) yield break;
            if (string.IsNullOrWhiteSpace(setup.defName) || !setup.defName.StartsWith("HR_", StringComparison.Ordinal))
                yield return "defName must use the HR_ prefix.";
            if (setup.scenario == null) yield return "scenario is required.";
            if (!Enum.IsDefined(typeof(HistoricalWorldMapMode), setup.mapMode)) yield return "mapMode is invalid.";
            if (setup.mapVersion < 1) yield return "mapVersion must be positive.";
            if (setup.useUniformPlainsTemplate && setup.worldMap == null)
                yield return "worldMap is required when useUniformPlainsTemplate is enabled.";
            if (setup.regions == null)
            {
                yield return "regions list is required.";
                yield break;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (HistoricalInitialRevealRecord record in setup.regions)
            {
                if (record == null || record.region == null)
                {
                    yield return "regions contains a record without a region Def.";
                    continue;
                }
                if (!Enum.IsDefined(typeof(HistoricalRegionAccess), record.access))
                    yield return record.region.defName + " has an invalid access state.";
                if (!ids.Add(record.region.defName)) yield return "Duplicate region access record: " + record.region.defName + ".";
                if (DefDatabase<HistoricalRegionGeographyDef>.AllDefsListForReading
                    .All(item => item.region == null || item.region.defName != record.region.defName))
                    yield return "No HistoricalRegionGeographyDef exists for " + record.region.defName + ".";
            }

            if (setup.scenario != null)
            {
                foreach (HistoricalRegionDef scenarioRegion in HistoricalDataQuery.GetRegions(setup.scenario).Where(item => item != null))
                {
                    if (!ids.Contains(scenarioRegion.defName))
                        yield return "Missing initial access record for scenario region " + scenarioRegion.defName + ".";
                }
            }

            if (setup.unlockRules == null) yield break;
            foreach (HistoricalRegionUnlockRule rule in setup.unlockRules.Where(item => item != null))
            {
                if (rule.region == null) yield return "An unlock rule has no region Def.";
                if (!Enum.IsDefined(typeof(HistoricalRegionAccess), rule.targetAccess)
                    || rule.targetAccess == HistoricalRegionAccess.Locked)
                    yield return "Unlock rules must target Unexplored or Revealed.";
            }
        }

        public static IEnumerable<string> ValidatePrototypeTileMapping(int tileId, string regionId)
        {
            if (tileId < 0) yield return "Tile ID cannot be negative.";
            if (string.IsNullOrWhiteSpace(regionId)) yield return "Region ID is required.";
            else if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
                yield return "Unknown historical region " + regionId + ".";
            if (Find.WorldGrid != null && tileId >= Find.WorldGrid.TilesCount)
                yield return "Tile ID is outside the current world grid.";
        }
    }
}
