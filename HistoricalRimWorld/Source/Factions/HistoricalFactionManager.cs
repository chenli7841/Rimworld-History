using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Factions
{
    public static class HistoricalFactionManager
    {
        public static bool TryGetFaction(string historicalStateId, out Faction faction)
        {
            faction = null;
            RimWorldWorld world = Find.World;
            HistoricalFactionWorldComponent component = world?.GetComponent<HistoricalFactionWorldComponent>();
            if (component == null || world.factionManager == null || string.IsNullOrEmpty(historicalStateId)) return false;

            foreach (HistoricalFactionBindingState binding in component.Bindings)
            {
                if (binding == null || binding.historicalStateId != historicalStateId) continue;
                faction = world.factionManager.AllFactionsListForReading
                    .FirstOrDefault(item => item != null && item.GetUniqueLoadID() == binding.factionLoadId);
                if (faction != null) return true;
            }
            return false;
        }

        public static bool TryGetHistoricalState(Faction faction, out HistoricalStateDef state)
        {
            state = null;
            if (faction == null) return false;
            HistoricalFactionWorldComponent component = Find.World?.GetComponent<HistoricalFactionWorldComponent>();
            HistoricalFactionBindingState binding = component?.Bindings.FirstOrDefault(item =>
                item != null && item.factionLoadId == faction.GetUniqueLoadID());
            if (binding == null) return false;
            state = DefDatabase<HistoricalStateDef>.GetNamedSilentFail(binding.historicalStateId);
            return state != null;
        }

        public static bool IsAllowedFaction(Faction faction, string scenarioId)
        {
            if (faction?.def == null) return false;
            if (faction == Faction.OfPlayer) return true;
            if (faction.def.defName == "HR_Faction_LocalBandits") return true;
            return GetBindings(scenarioId).Any(binding => binding.rimWorldFactionDef == faction.def);
        }

        internal static List<FactionBindingPair> GeneratedPairs(IEnumerable<Faction> factions, string scenarioId)
        {
            List<FactionBindingPair> result = new List<FactionBindingPair>();
            if (factions == null) return result;
            foreach (HistoricalFactionBindingDef binding in GetBindings(scenarioId))
            {
                Faction faction = factions.FirstOrDefault(item => item != null && item.def == binding.rimWorldFactionDef);
                if (faction != null) result.Add(new FactionBindingPair(faction, binding.historicalState.defName));
            }
            return result;
        }

        internal static List<HistoricalFactionBindingDef> GetBindings(string scenarioId) =>
            DefDatabase<HistoricalFactionBindingDef>.AllDefsListForReading
                .Where(item => item != null && item.generateAtWorldCreation && item.scenario != null
                    && item.scenario.defName == scenarioId && item.historicalState != null && item.rimWorldFactionDef != null)
                .OrderBy(item => item.defName, StringComparer.Ordinal)
                .ToList();

        public static IEnumerable<string> ValidateBindings(HistoricalScenarioDef scenario)
        {
            if (scenario == null)
            {
                yield return "Historical scenario is missing.";
                yield break;
            }

            List<HistoricalFactionBindingDef> bindings = GetBindings(scenario.defName);
            if (bindings.Count == 0)
            {
                yield return "No historical faction bindings are defined for " + scenario.defName + ".";
                yield break;
            }

            HashSet<string> stateIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> factionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (HistoricalFactionBindingDef binding in bindings)
            {
                if (!stateIds.Add(binding.historicalState.defName)) yield return "Multiple Faction bindings map to historical state " + binding.historicalState.defName + ".";
                if (!factionIds.Add(binding.rimWorldFactionDef.defName)) yield return "FactionDef is bound more than once: " + binding.rimWorldFactionDef.defName + ".";
            }
        }
    }
}
