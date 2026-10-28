using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.World
{
    public static class HistoricalWorldRevealManager
    {
        public static bool IsActive
        {
            get
            {
                HistoricalWorldRevealWorldComponent component = GetCurrentComponent();
                HistoricalScenarioDef scenario = HistoricalScenarioManager.CurrentScenario;
                return component != null && component.Initialized && scenario != null
                    && component.ScenarioDefName == scenario.defName;
            }
        }

        public static bool TryGetRegionAccess(string regionId, out HistoricalRegionAccess access)
        {
            HistoricalRegionRevealState state = GetRegionState(regionId);
            access = state == null ? HistoricalRegionAccess.Locked : state.Access;
            return state != null;
        }

        public static bool TryInitializeForScenario(HistoricalScenarioDef scenario, out string error)
        {
            if (scenario == null || HistoricalScenarioManager.CurrentScenario == null
                || HistoricalScenarioManager.CurrentScenario.defName != scenario.defName)
                return Reject("The scenario must already be active in this world before its reveal state can be initialized.", out error);
            HistoricalWorldRevealInitializationPlan plan;
            if (!TryPrepareScenarioInitialization(scenario, out plan, out error)) return false;
            return TryCommitScenarioInitialization(plan, out error);
        }

        public static bool IsTileAccessible(int tileId)
        {
            if (!IsActive) return true;
            EnsureMapperWarning();
            return HistoricalWorldTileAccessCache.IsAccessible(tileId);
        }

        public static bool IsTileVisible(int tileId)
        {
            if (!IsActive) return true;
            EnsureMapperWarning();
            return HistoricalWorldTileAccessCache.IsVisible(tileId);
        }

        public static bool TryUnlockRegion(string regionId, HistoricalRevealSource source, out string error)
        {
            error = null;
            if (!CanChangeAccess(source, true, out error)) return false;
            HistoricalWorldRevealWorldComponent component = GetInitializedComponent();
            HistoricalRegionRevealState state = component?.FindRegion(regionId);
            if (state == null) return Reject("Unknown region in the active reveal setup: " + regionId + ".", out error);
            if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
                return Reject("The configured region Def is missing: " + regionId + ".", out error);
            if (state.Access != HistoricalRegionAccess.Locked) return true;

            if (!component.ChangeAccess(regionId, HistoricalRegionAccess.Unexplored, source, false, out error)) return false;
            NotifyRegionUnlocked(regionId);
            return true;
        }

        public static bool TryRevealRegion(string regionId, HistoricalRevealSource source, out string error)
        {
            error = null;
            if (!CanChangeAccess(source, false, out error)) return false;
            HistoricalWorldRevealWorldComponent component = GetInitializedComponent();
            HistoricalRegionRevealState state = component?.FindRegion(regionId);
            if (state == null) return Reject("Unknown region in the active reveal setup: " + regionId + ".", out error);
            if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
                return Reject("The configured region Def is missing: " + regionId + ".", out error);
            if (state.Access == HistoricalRegionAccess.Locked)
                return Reject("A locked region must be unlocked before it can be revealed.", out error);

            bool wasUnexplored = state.Access == HistoricalRegionAccess.Unexplored;
            if (!component.ChangeAccess(regionId, HistoricalRegionAccess.Revealed, source,
                source == HistoricalRevealSource.Exploration, out error)) return false;
            if (wasUnexplored) NotifyRegionRevealed(regionId);
            return true;
        }

        public static bool TryMapPrototypeTile(int tileId, string regionId, HistoricalRevealSource source, out string error)
        {
            error = null;
            if (source != HistoricalRevealSource.DeveloperAction || !Prefs.DevMode)
                return Reject("Prototype tile mapping can only be edited by a developer action in developer mode.", out error);
            HistoricalWorldRevealWorldComponent component = GetInitializedComponent();
            if (component == null || component.MapMode != HistoricalWorldMapMode.PrototypeWorld)
                return Reject("The active world is not using PrototypeWorld mapping.", out error);
            string[] validationErrors = HistoricalWorldAccessValidator.ValidatePrototypeTileMapping(tileId, regionId).ToArray();
            if (validationErrors.Length > 0) return Reject(string.Join(Environment.NewLine, validationErrors), out error);
            if (component.FindRegion(regionId) == null)
                return Reject("The region is not included in the active reveal setup.", out error);

            component.AddPrototypeTileMapping(tileId, regionId);
            error = null;
            return true;
        }

        public static bool TryUnmapPrototypeTile(int tileId, string regionId, HistoricalRevealSource source, out string error)
        {
            error = null;
            if (source != HistoricalRevealSource.DeveloperAction || !Prefs.DevMode)
                return Reject("Prototype tile mapping can only be edited by a developer action in developer mode.", out error);
            HistoricalWorldRevealWorldComponent component = GetInitializedComponent();
            if (component == null || component.MapMode != HistoricalWorldMapMode.PrototypeWorld)
                return Reject("The active world is not using PrototypeWorld mapping.", out error);
            component.RemovePrototypeTileMapping(tileId, regionId);
            return true;
        }

        public static bool TrySetPrototypeMappingCoverageComplete(bool complete, HistoricalRevealSource source, out string error)
        {
            error = null;
            if (source != HistoricalRevealSource.DeveloperAction || !Prefs.DevMode)
                return Reject("Prototype map coverage can only be configured by a developer action in developer mode.", out error);
            HistoricalWorldRevealWorldComponent component = GetInitializedComponent();
            if (component == null || component.MapMode != HistoricalWorldMapMode.PrototypeWorld)
                return Reject("The active world is not using PrototypeWorld mapping.", out error);
            if (complete && Find.WorldGrid != null
                && component.PrototypeMappings.Select(item => item.TileId).Distinct().Count() < Find.WorldGrid.TilesCount)
            {
                return Reject("Every tile must be explicitly mapped before coverage can be marked complete.", out error);
            }
            component.SetPrototypeMappingCoverage(complete);
            return true;
        }

        public static IEnumerable<string> GetRegionsForTile(int tileId) => HistoricalGeographyManager.GetRegionsForTile(tileId);

        internal static HistoricalRegionRevealState GetRegionState(string regionId) =>
            GetInitializedComponent()?.FindRegion(regionId);

        internal static HistoricalWorldRevealWorldComponent GetCurrentComponent()
        {
            return Find.World == null ? null : Find.World.GetComponent<HistoricalWorldRevealWorldComponent>();
        }

        internal static bool TryPrepareScenarioInitialization(HistoricalScenarioDef scenario,
            out HistoricalWorldRevealInitializationPlan plan, out string error)
        {
            plan = null;
            error = null;
            if (scenario == null) return Reject("No historical scenario was supplied for reveal initialization.", out error);
            HistoricalWorldRevealWorldComponent component = GetCurrentComponent();
            if (component == null) return Reject("HistoricalWorldRevealWorldComponent is unavailable.", out error);
            if (component.Initialized)
            {
                if (component.ScenarioDefName == scenario.defName) return true;
                return Reject("World reveal state is already initialized for another scenario.", out error);
            }

            List<HistoricalWorldRevealSetupDef> setups = DefDatabase<HistoricalWorldRevealSetupDef>.AllDefsListForReading
                .Where(item => item.scenario != null && item.scenario.defName == scenario.defName).ToList();
            if (setups.Count != 1)
            {
                return Reject(setups.Count == 0
                    ? "No HistoricalWorldRevealSetupDef is registered for scenario " + scenario.defName + "."
                    : "More than one HistoricalWorldRevealSetupDef is registered for scenario " + scenario.defName + ".", out error);
            }

            HistoricalWorldRevealSetupDef setup = setups[0];
            string[] validationErrors = HistoricalWorldAccessValidator.ValidateSetup(setup).ToArray();
            if (validationErrors.Length > 0) return Reject(string.Join(Environment.NewLine, validationErrors), out error);

            List<HistoricalRegionRevealState> states = new List<HistoricalRegionRevealState>();
            foreach (HistoricalInitialRevealRecord record in setup.regions)
            {
                HistoricalRegionRevealState state = new HistoricalRegionRevealState();
                state.Initialize(record.region.defName, record.access);
                states.Add(state);
            }

            plan = new HistoricalWorldRevealInitializationPlan(setup.scenario.defName, setup.defName,
                setup.mapMode, setup.mapVersion, states);
            return true;
        }

        internal static bool TryCommitScenarioInitialization(HistoricalWorldRevealInitializationPlan plan, out string error)
        {
            error = null;
            HistoricalWorldRevealWorldComponent component = GetCurrentComponent();
            if (component == null) return Reject("HistoricalWorldRevealWorldComponent is unavailable.", out error);
            if (plan == null) return component.Initialized;
            if (!component.ApplyInitializationPlan(plan))
                return Reject("World reveal state is already initialized for another scenario.", out error);

            Log.Message("[Historical RimWorld] Initialized world reveal state for scenario " + plan.ScenarioDefName + ".");
            EnsureMapperWarning();
            return true;
        }

        private static bool CanChangeAccess(HistoricalRevealSource source, bool unlocking, out string error)
        {
            error = null;
            if (!IsActive) return Reject("An active historical scenario is required to change region access.", out error);
            if (!Enum.IsDefined(typeof(HistoricalRevealSource), source)) return Reject("Unknown reveal source.", out error);
            if (source == HistoricalRevealSource.DeveloperAction)
            {
                if (Prefs.DevMode) return true;
                return Reject("Developer actions require developer mode.", out error);
            }
            if (source == HistoricalRevealSource.HistoricalEvent) return true;
            if (!unlocking && (source == HistoricalRevealSource.Exploration
                || source == HistoricalRevealSource.TravellerReport)) return true;
            return Reject(unlocking
                ? "Only a historical event or developer action can unlock a region."
                : "This source cannot reveal a region.", out error);
        }

        private static void EnsureMapperWarning()
        {
            HistoricalWorldRevealWorldComponent component = GetInitializedComponent();
            if (component == null || component.EarthMapperWarningWritten) return;
            IHistoricalWorldGeoMapper mapper = HistoricalGeographyManager.CurrentMapper;
            if (mapper != null && mapper.IsAvailable) return;
            Log.Warning("[Historical RimWorld] Historical geography mapping is unavailable; world access restrictions and tile fog are disabled for this save.");
            component.MarkEarthMapperWarningWritten();
        }

        private static void NotifyRegionUnlocked(string regionId)
        {
            HistoricalRegionDef region = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId);
            string label = region == null ? regionId : region.LabelCap;
            Messages.Message(string.Format(Translator.Translate("HR_World_RegionUnlocked"), label),
                MessageTypeDefOf.PositiveEvent, false);
        }

        private static void NotifyRegionRevealed(string regionId)
        {
            HistoricalRegionDef region = DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId);
            string label = region == null ? regionId : region.LabelCap;
            Messages.Message(string.Format(Translator.Translate("HR_World_RegionRevealed"), label),
                MessageTypeDefOf.PositiveEvent, false);
        }

        private static HistoricalWorldRevealWorldComponent GetInitializedComponent()
        {
            HistoricalWorldRevealWorldComponent component = GetCurrentComponent();
            return component != null && component.Initialized ? component : null;
        }

        private static bool Reject(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
