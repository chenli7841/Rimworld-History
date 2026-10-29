using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.Scenarios.EasternZhou;
using HistoricalRimWorld.WorldGeneration;
using RimWorld;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Patches.WorldGeneration
{
    [HarmonyPatch]
    internal static class HistoricalFactionSelectionPatch
    {
        private static MethodBase TargetMethod() => AccessTools.PropertyGetter(typeof(FactionGenerator), nameof(FactionGenerator.ConfigurableFactions));

        private static bool Prefix(ref IEnumerable<FactionDef> __result)
        {
            if (!HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)) return true;

            List<FactionDef> historicalFactions = HistoricalWorldGenerationController.GetHistoricalFactionDefs(setup.scenario.defName);
            if (historicalFactions.Count == 0)
            {
                Log.Error("[Historical RimWorld] Historical scenario has no configured faction bindings: " + setup.scenario.defName);
                __result = Enumerable.Empty<FactionDef>();
                return false;
            }

            __result = historicalFactions;
            return false;
        }
    }

    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.GenerateWorld))]
    internal static class HistoricalWorldGenerationPatch
    {
        private static void Prefix(ref List<FactionDef> factions)
        {
            if (!HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)) return;

            List<FactionDef> allowed = HistoricalWorldGenerationController.GetHistoricalFactionDefs(setup.scenario.defName);
            if (allowed.Count == 0)
            {
                Log.Error("[Historical RimWorld] Historical world generation was requested, but no historical factions are configured. No factions will be generated for this world.");
                factions = new List<FactionDef>();
                return;
            }

            HashSet<FactionDef> allowedSet = new HashSet<FactionDef>(allowed);
            List<FactionDef> selected = (factions ?? new List<FactionDef>())
                .Where(item => item != null && allowedSet.Contains(item))
                .ToList();

            foreach (HistoricalFactionBindingDef binding in HistoricalFactionManager.GetBindings(setup.scenario.defName))
            {
                int existing = selected.Count(item => item == binding.rimWorldFactionDef);
                int minimum = binding.generateAtWorldCreation ? Math.Max(1, binding.desiredFactionCount) : 0;
                while (existing < minimum)
                {
                    selected.Add(binding.rimWorldFactionDef);
                    existing++;
                }
            }
            FactionDef localBandits = DefDatabase<FactionDef>.GetNamedSilentFail("HR_Faction_LocalBandits");
            if (localBandits != null && !selected.Contains(localBandits)) selected.Add(localBandits);
            factions = selected;
        }

        private static void Postfix(RimWorldWorld __result, List<FactionDef> factions)
        {
            if (!HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)) return;
            HistoricalWorldGenerationController.InitializeGeneratedWorld(__result, setup.scenario, factions);
        }
    }

    [HarmonyPatch(typeof(FactionManager), nameof(FactionManager.GetFactions))]
    internal static class HistoricalFactionQueryPatch
    {
        private static void Postfix(ref IEnumerable<Faction> __result)
        {
            if (!HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)) return;
            __result = (__result ?? Enumerable.Empty<Faction>())
                .Where(faction => HistoricalFactionManager.IsAllowedFaction(faction, setup.scenario.defName))
                .ToList();
        }
    }

    [HarmonyPatch]
    internal static class HistoricalStartingRegionSelectionPatch
    {
        private static int lastRejectedTile = -1;

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Page_SelectStartingSite), "CanDoNext");

        private static void Postfix(ref bool __result)
        {
            if (!__result || !HistoricalFactionGenerationContext.IsHistoricalWorldGenerationActive) return;
            HistoricalFactionWorldComponent component = Find.World?.GetComponent<HistoricalFactionWorldComponent>();
            if (component == null || component.GenerationMode != HistoricalFactionGenerationMode.HistoricalScenario) return;

            if (!component.GenerationComplete)
            {
                __result = false;
                if (lastRejectedTile != -2)
                {
                    lastRejectedTile = -2;
                    Messages.Message("HR_012_WorldGenerationFailed".Translate(
                        component.GenerationFailureReason ?? "Unknown placement failure."), MessageTypeDefOf.RejectInput, false);
                }
                return;
            }

            int tileId = (int)Find.WorldInterface.SelectedTile;
            if (component.IsTileInRegion(tileId, "HR_Region_ZhengHeartland"))
            {
                lastRejectedTile = -1;
                return;
            }

            __result = false;
            if (tileId != lastRejectedTile)
            {
                lastRejectedTile = tileId;
                Messages.Message("HR_012_SelectZhengStartingRegion".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}
