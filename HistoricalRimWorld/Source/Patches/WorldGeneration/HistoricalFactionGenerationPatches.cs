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
using UnityEngine;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Patches.WorldGeneration
{
    [HarmonyPatch(typeof(Page_SelectScenario), nameof(Page_SelectScenario.BeginScenarioConfiguration))]
    internal static class HistoricalScenarioSelectionContextPatch
    {
        private static void Prefix(Scenario scen)
        {
            HistoricalFactionGenerationContext.CaptureSelectedScenario(scen);
        }
    }

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

    [HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.DoWindowContents))]
    internal static class HistoricalAutomaticCoverageUiPatch
    {
        private static void Prefix(ref float ___planetCoverage)
        {
            if (!HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)) return;
            if (!HistoricalWorldGenerationController.TryPrepareWorldGeneration(setup.scenario,
                out float requiredCoverage, out _, out string error))
            {
                Log.Error("[Historical RimWorld] Could not update the displayed historical coverage: " + error);
                return;
            }
            ___planetCoverage = requiredCoverage;
        }
    }

    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.GenerateWorld))]
    internal static class HistoricalWorldGenerationPatch
    {
        private static void Prefix(ref float planetCoverage, ref string seedString, ref List<FactionDef> factions)
        {
            if (!HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)) return;

            if (HistoricalWorldGenerationController.TryPrepareWorldGeneration(setup.scenario,
                out float requiredCoverage, out HistoricalSphericalCoverage coverage, out string coverageError))
            {
                Log.Message("[Historical RimWorld] Scenario-bounded world generation selected "
                    + requiredCoverage.ToString("P0", System.Globalization.CultureInfo.InvariantCulture)
                    + " planet coverage for center " + coverage.CenterLongitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + "°, " + coverage.CenterLatitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + "° and radius " + coverage.AngularRadiusDegrees.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°.");
                planetCoverage = requiredCoverage;
            }
            else
            {
                Log.Error("[Historical RimWorld] Could not prepare scenario-bounded world generation: " + coverageError);
            }

            string requestedSeed = seedString;
            seedString = setup.worldSeedOverride;
            Log.Message("[Historical RimWorld] Eastern Zhou world seed fixed to '" + seedString
                + "' (the seed entered on the world creation page '" + requestedSeed + "' is ignored for this scenario).");

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

            RimWorldWorld previousCreatingWorld = Current.CreatingWorld;
            Current.CreatingWorld = __result;
            try
            {
                HistoricalWorldGenerationController.InitializeGeneratedWorld(__result, setup.scenario, factions);
            }
            finally
            {
                Current.CreatingWorld = previousCreatingWorld;
            }
        }
    }

    [HarmonyPatch(typeof(WorldGrid), "CalculateViewCenterAndAngle")]
    internal static class HistoricalWorldCenterPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ref Vector3 ___surfaceViewCenter, ref float ___surfaceViewAngle)
        {
            if (!HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)) return;
            if (!HistoricalWorldGenerationController.TryPrepareWorldGeneration(setup.scenario,
                out _, out HistoricalSphericalCoverage coverage, out string error))
            {
                Log.Error("[Historical RimWorld] Could not set historical world center: " + error);
                return;
            }
            // CalculateViewCenterAndAngle derives a vanilla center from the game's
            // global planet coverage. Set both fields last so the surface layer is
            // built from this scenario's spherical cap even if another postfix or
            // a stale UI coverage value changed the vanilla calculation.
            ___surfaceViewCenter = coverage.CenterVector.normalized;
            ___surfaceViewAngle = (float)coverage.AngularRadiusDegrees;
            Log.Message("[Historical RimWorld] Set surface world-generation center to "
                + coverage.CenterLongitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "° longitude, "
                + coverage.CenterLatitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "° latitude and view angle "
                + ___surfaceViewAngle.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°.");
        }
    }

    // WorldGrid copies its view fields into PlanetLayer instances during
    // CreateRequiredLayers. Override the actual surface-layer inputs immediately
    // before IcosahedronGenerator builds the mesh. This makes the selected bounds
    // authoritative even if another patch changes WorldGrid's intermediate fields.
    [HarmonyPatch(typeof(PlanetLayer), "InitializeLayer")]
    internal static class HistoricalSurfaceLayerCoveragePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(PlanetLayer __instance, ref Vector3 ___viewCenter, ref float ___viewAngle)
        {
            if (__instance?.Def == null || !__instance.Def.SurfaceTiles) return;
            if (!HistoricalFactionGenerationContext.TryGetSelectedSetup(out HistoricalPlayableScenarioSetupDef setup)) return;
            if (!HistoricalWorldGenerationController.TryPrepareWorldGeneration(setup.scenario,
                out _, out HistoricalSphericalCoverage coverage, out string error))
            {
                Log.Error("[Historical RimWorld] Could not apply coverage to the surface PlanetLayer: " + error);
                return;
            }

            ___viewCenter = coverage.CenterVector.normalized;
            ___viewAngle = (float)coverage.AngularRadiusDegrees;
            Log.Message("[Historical RimWorld] Applied scenario coverage directly to surface PlanetLayer: center "
                + coverage.CenterLongitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "° longitude, "
                + coverage.CenterLatitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "° latitude; view angle "
                + ___viewAngle.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "°.");
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
