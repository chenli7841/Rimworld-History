using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using Verse;

namespace HistoricalRimWorld.Scenarios
{
    public static class HistoricalDefValidator
    {
        public static IEnumerable<string> ValidateScenario(HistoricalScenarioDef scenario)
        {
            if (scenario == null)
            {
                yield return "Scenario definition is null.";
                yield break;
            }

            if (!HasHistoricalPrefix(scenario.defName))
            {
                yield return "Scenario defName must use the HR_ prefix.";
            }

            string dateError = null;
            try
            {
                scenario.GetStartDate();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                dateError = "Invalid scenario start date: " + exception.ParamName + ".";
            }

            if (dateError != null)
            {
                yield return dateError;
            }

            if (scenario.endAstronomicalYear.HasValue
                && scenario.endAstronomicalYear.Value < scenario.startAstronomicalYear)
            {
                yield return "endAstronomicalYear cannot be earlier than the scenario start year.";
            }

            foreach (string error in ValidateList(scenario.states, "states")) yield return error;
            foreach (string error in ValidateList(scenario.regions, "regions")) yield return error;
            foreach (string error in ValidateList(scenario.cultures, "cultures")) yield return error;
            foreach (string error in ValidateList(scenario.settlements, "settlements")) yield return error;

            if (scenario.defaultStartingRegion == null)
            {
                yield return "defaultStartingRegion is required.";
            }
            else if (!ContainsDef(scenario.regions, scenario.defaultStartingRegion))
            {
                yield return "defaultStartingRegion must be included in the scenario regions list.";
            }

            if (scenario.defaultStartingState == null)
            {
                yield return "defaultStartingState is required.";
            }
            else if (!ContainsDef(scenario.states, scenario.defaultStartingState))
            {
                yield return "defaultStartingState must be included in the scenario states list.";
            }

            if (scenario.states != null)
            {
                foreach (HistoricalStateDef state in scenario.states.Where(item => item != null))
                {
                    foreach (string error in ValidateState(state)) yield return state.defName + ": " + error;
                    if (state.primaryCulture != null && !ContainsDef(scenario.cultures, state.primaryCulture))
                    {
                        yield return state.defName + ": primaryCulture must be included in the scenario cultures list.";
                    }

                    if (state.nominalOverlord != null && !ContainsDef(scenario.states, state.nominalOverlord))
                    {
                        yield return state.defName + ": nominalOverlord must be included in the scenario states list.";
                    }
                }

            }

            if (scenario.regions != null)
            {
                foreach (HistoricalRegionDef region in scenario.regions.Where(item => item != null))
                {
                    foreach (string error in ValidateRegion(region)) yield return region.defName + ": " + error;
                    if (region.adjacentRegions != null)
                    {
                        foreach (HistoricalRegionDef adjacent in region.adjacentRegions.Where(item => item != null))
                        {
                            if (!ContainsDef(scenario.regions, adjacent))
                            {
                                yield return region.defName + ": adjacent region " + adjacent.defName + " is not included in the scenario regions list.";
                            }
                        }
                    }

                    if (region.associatedCultures != null)
                    {
                        foreach (HistoricalCultureDef culture in region.associatedCultures.Where(item => item != null))
                        {
                            if (!ContainsDef(scenario.cultures, culture))
                            {
                                yield return region.defName + ": associated culture " + culture.defName + " is not included in the scenario cultures list.";
                            }
                        }
                    }
                }

                foreach (string warning in GetAsymmetricAdjacencyWarnings(scenario.regions))
                {
                    Log.Warning("[Historical RimWorld] " + warning);
                }
            }

            if (scenario.cultures != null)
            {
                foreach (HistoricalCultureDef culture in scenario.cultures.Where(item => item != null))
                {
                    foreach (string error in ValidateCulture(culture)) yield return culture.defName + ": " + error;
                }
            }

            if (scenario.settlements != null)
            {
                foreach (HistoricalSettlementDef settlement in scenario.settlements.Where(item => item != null))
                {
                    foreach (string error in ValidateSettlement(settlement)) yield return settlement.defName + ": " + error;
                    if (settlement.region != null && !ContainsDef(scenario.regions, settlement.region))
                    {
                        yield return settlement.defName + ": region must be included in the scenario regions list.";
                    }

                    if (settlement.initialState != null && !ContainsDef(scenario.states, settlement.initialState))
                    {
                        yield return settlement.defName + ": initialState must be included in the scenario states list.";
                    }
                }
            }
        }

        public static IEnumerable<string> ValidateState(HistoricalStateDef state)
        {
            if (state == null) yield break;
            if (!HasHistoricalPrefix(state.defName)) yield return "defName must use the HR_ prefix.";
            if (state.primaryCulture == null) yield return "primaryCulture is required.";
            if (state.nominalOverlord != null && SameDef(state, state.nominalOverlord))
            {
                yield return "nominalOverlord cannot reference the state itself.";
            }

            if (state.foundedAstronomicalYear.HasValue && state.dissolvedAstronomicalYear.HasValue
                && state.foundedAstronomicalYear.Value > state.dissolvedAstronomicalYear.Value)
            {
                yield return "foundedAstronomicalYear cannot be later than dissolvedAstronomicalYear.";
            }

            foreach (string error in ValidateOverlordCycleFrom(state)) yield return error;
        }

        public static IEnumerable<string> ValidateRegion(HistoricalRegionDef region)
        {
            if (region == null) yield break;
            if (!HasHistoricalPrefix(region.defName)) yield return "defName must use the HR_ prefix.";

            if (region.adjacentRegions != null)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (HistoricalRegionDef adjacent in region.adjacentRegions)
                {
                    if (adjacent == null)
                    {
                        yield return "adjacentRegions contains an empty reference.";
                        continue;
                    }

                    if (SameDef(region, adjacent)) yield return "A region cannot be adjacent to itself.";
                    if (!seen.Add(adjacent.defName)) yield return "adjacentRegions contains duplicate " + adjacent.defName + ".";
                }
            }

            if (region.associatedCultures != null)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (HistoricalCultureDef culture in region.associatedCultures)
                {
                    if (culture == null)
                    {
                        yield return "associatedCultures contains an empty reference.";
                        continue;
                    }

                    if (!seen.Add(culture.defName)) yield return "associatedCultures contains duplicate " + culture.defName + ".";
                }
            }
        }

        public static IEnumerable<string> ValidateCulture(HistoricalCultureDef culture)
        {
            if (culture == null) yield break;
            if (!HasHistoricalPrefix(culture.defName)) yield return "defName must use the HR_ prefix.";

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string traditionId in culture.traditionIds ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(traditionId))
                {
                    yield return "traditionIds cannot contain blank values.";
                    continue;
                }

                if (!seen.Add(traditionId)) yield return "traditionIds contains duplicate " + traditionId + ".";
            }
            foreach (string error in ValidateOptionalList(culture.relatedCultures, "relatedCultures")) yield return error;
            foreach (string error in ValidateOptionalList(culture.customs, "customs")) yield return error;
            foreach (string error in ValidateOptionalList(culture.rituals, "rituals")) yield return error;
        }

        public static IEnumerable<string> ValidateSettlement(HistoricalSettlementDef settlement)
        {
            if (settlement == null) yield break;
            if (!HasHistoricalPrefix(settlement.defName)) yield return "defName must use the HR_ prefix.";
            if (settlement.region == null) yield return "region is required.";
            if (settlement.initialState == null) yield return "initialState is required.";

            if (settlement.foundedAstronomicalYear.HasValue && settlement.abandonedAstronomicalYear.HasValue
                && settlement.foundedAstronomicalYear.Value > settlement.abandonedAstronomicalYear.Value)
            {
                yield return "foundedAstronomicalYear cannot be later than abandonedAstronomicalYear.";
            }

            if (settlement.latitude.HasValue && (settlement.latitude.Value < -90d || settlement.latitude.Value > 90d))
            {
                yield return "latitude must be between -90 and 90 degrees.";
            }

            if (settlement.longitude.HasValue && (settlement.longitude.Value < -180d || settlement.longitude.Value > 180d))
            {
                yield return "longitude must be between -180 and 180 degrees.";
            }

            if (settlement.latitude.HasValue != settlement.longitude.HasValue)
            {
                yield return "latitude and longitude must either both be supplied or both be omitted.";
            }
        }

        private static IEnumerable<string> ValidateList<TDef>(List<TDef> defs, string fieldName) where TDef : Def
        {
            if (defs == null)
            {
                yield return fieldName + " list is missing.";
                yield break;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TDef def in defs)
            {
                if (def == null)
                {
                    yield return fieldName + " contains an empty Def reference.";
                    continue;
                }

                if (!seen.Add(def.defName)) yield return fieldName + " contains duplicate " + def.defName + ".";
            }
        }

        private static IEnumerable<string> ValidateOptionalList<TDef>(List<TDef> defs, string fieldName) where TDef : Def
        {
            if (defs == null) yield break;
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TDef def in defs)
            {
                if (def == null) { yield return fieldName + " contains an empty Def reference."; continue; }
                if (!seen.Add(def.defName)) yield return fieldName + " contains duplicate " + def.defName + ".";
            }
        }

        private static IEnumerable<string> ValidateOverlordCycleFrom(HistoricalStateDef start)
        {
            HashSet<string> path = new HashSet<string>(StringComparer.Ordinal);
            HistoricalStateDef current = start;
            while (current != null)
            {
                if (!path.Add(current.defName))
                {
                    yield return "nominalOverlord relationship contains a cycle involving " + current.defName + ".";
                    yield break;
                }

                current = current.nominalOverlord;
            }
        }

        private static IEnumerable<string> GetAsymmetricAdjacencyWarnings(List<HistoricalRegionDef> regions)
        {
            foreach (HistoricalRegionDef region in regions.Where(item => item != null))
            {
                if (region.adjacentRegions == null) continue;
                foreach (HistoricalRegionDef adjacent in region.adjacentRegions.Where(item => item != null))
                {
                    bool reciprocal = adjacent.adjacentRegions != null
                        && adjacent.adjacentRegions.Any(item => item != null && SameDef(item, region));
                    if (!reciprocal)
                    {
                        yield return "Region adjacency is not reciprocal: " + region.defName + " -> " + adjacent.defName + ".";
                    }
                }
            }
        }

        private static bool ContainsDef<TDef>(IEnumerable<TDef> defs, TDef target) where TDef : Def
        {
            return defs != null && target != null && defs.Any(item => item != null && SameDef(item, target));
        }

        private static bool SameDef(Def left, Def right)
        {
            return left != null && right != null && string.Equals(left.defName, right.defName, StringComparison.Ordinal);
        }

        private static bool HasHistoricalPrefix(string defName)
        {
            return !string.IsNullOrEmpty(defName) && defName.StartsWith("HR_", StringComparison.Ordinal);
        }
    }
}
