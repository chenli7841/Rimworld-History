using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Patches.World;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.UI;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    public sealed class HistoricalWorldRevealWorldComponent : WorldComponent
    {
        private string scenarioDefName;
        private string setupDefName;
        private HistoricalWorldMapMode mapMode;
        private int mapVersion;
        private bool initialized;
        private bool prototypeMappingCoverageComplete;
        private List<HistoricalRegionRevealState> regions = new List<HistoricalRegionRevealState>();
        private List<HistoricalPrototypeTileMapping> prototypeMappings = new List<HistoricalPrototypeTileMapping>();
        private List<HistoricalRevealChangeRecord> changes = new List<HistoricalRevealChangeRecord>();
        private int nextChangeSequence = 1;
        private bool earthMapperWarningWritten;

        public bool Initialized => initialized;
        public string ScenarioDefName => scenarioDefName;
        public string SetupDefName => setupDefName;
        public HistoricalWorldMapMode MapMode => mapMode;
        public int MapVersion => mapVersion;
        public bool PrototypeMappingCoverageComplete => prototypeMappingCoverageComplete;
        public IEnumerable<HistoricalRegionRevealState> Regions => regions ?? Enumerable.Empty<HistoricalRegionRevealState>();
        public IEnumerable<HistoricalPrototypeTileMapping> PrototypeMappings => prototypeMappings ?? Enumerable.Empty<HistoricalPrototypeTileMapping>();
        public IEnumerable<HistoricalRevealChangeRecord> Changes => changes ?? Enumerable.Empty<HistoricalRevealChangeRecord>();

        public HistoricalWorldRevealWorldComponent(RimWorldWorld world) : base(world)
        {
        }

        public HistoricalRegionRevealState FindRegion(string regionId) =>
            regions?.FirstOrDefault(item => item != null && item.RegionId == regionId);

        public HistoricalPrototypeTileMapping FindTileMapping(int tileId) =>
            prototypeMappings?.FirstOrDefault(item => item != null && item.TileId == tileId);

        internal bool ApplyInitializationPlan(HistoricalWorldRevealInitializationPlan plan)
        {
            if (plan == null) return false;
            if (initialized) return string.Equals(scenarioDefName, plan.ScenarioDefName, StringComparison.Ordinal);

            scenarioDefName = plan.ScenarioDefName;
            setupDefName = plan.SetupDefName;
            mapMode = plan.MapMode;
            mapVersion = plan.MapVersion;
            regions = plan.Regions;
            changes = new List<HistoricalRevealChangeRecord>();
            nextChangeSequence = 1;
            prototypeMappingCoverageComplete = false;
            initialized = true;
            HistoricalWorldPatchContext.ResetNotifications();
            LoadConfiguredPrototypeMappings();
            RebuildDerivedCaches();
            return true;
        }

        internal bool ChangeAccess(string regionId, HistoricalRegionAccess access, HistoricalRevealSource source,
            bool visited, out string error)
        {
            error = null;
            HistoricalRegionRevealState state = FindRegion(regionId);
            if (state == null)
            {
                error = "Region " + regionId + " is not part of the active reveal setup.";
                return false;
            }

            HistoricalRegionAccess oldAccess = state.Access;
            bool wasVisited = state.EverVisited;
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            bool changed = state.SetAccess(access, tick, visited);
            if (!changed) return true;

            if (changes == null) changes = new List<HistoricalRevealChangeRecord>();
            HistoricalRevealChangeRecord record = new HistoricalRevealChangeRecord();
            record.Initialize(NextChangeId(), regionId, oldAccess, access, source, tick, wasVisited, state.EverVisited);
            changes.Add(record);
            RebuildDerivedCaches();
            return true;
        }

        internal bool AddPrototypeTileMapping(int tileId, string regionId)
        {
            if (mapMode != HistoricalWorldMapMode.PrototypeWorld) return false;
            HistoricalPrototypeTileMapping mapping = FindTileMapping(tileId);
            if (mapping == null)
            {
                mapping = new HistoricalPrototypeTileMapping();
                mapping.Initialize(tileId, null);
                if (prototypeMappings == null) prototypeMappings = new List<HistoricalPrototypeTileMapping>();
                prototypeMappings.Add(mapping);
            }

            bool changed = mapping.AddRegion(regionId);
            if (changed) RebuildDerivedCaches();
            return changed;
        }

        internal bool ReplacePrototypeTileRegion(int tileId, string regionId)
        {
            if (mapMode != HistoricalWorldMapMode.PrototypeWorld) return false;
            HistoricalPrototypeTileMapping mapping = FindTileMapping(tileId);
            if (mapping == null)
            {
                mapping = new HistoricalPrototypeTileMapping();
                mapping.Initialize(tileId, null);
                if (prototypeMappings == null) prototypeMappings = new List<HistoricalPrototypeTileMapping>();
                prototypeMappings.Add(mapping);
            }
            foreach (string existingId in mapping.RegionIds.ToList()) mapping.RemoveRegion(existingId);
            bool changed = mapping.AddRegion(regionId);
            if (changed) RebuildDerivedCaches();
            return changed;
        }

        internal bool RemovePrototypeTileMapping(int tileId, string regionId)
        {
            HistoricalPrototypeTileMapping mapping = FindTileMapping(tileId);
            if (mapping == null || !mapping.RemoveRegion(regionId)) return false;
            if (!mapping.RegionIds.Any()) prototypeMappings.Remove(mapping);
            RebuildDerivedCaches();
            return true;
        }

        internal void SetPrototypeMappingCoverage(bool complete)
        {
            if (mapMode != HistoricalWorldMapMode.PrototypeWorld || !prototypeMappings.Any()) return;
            prototypeMappingCoverageComplete = complete;
            RebuildDerivedCaches();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioDefName, "historicalWorldScenarioDefName");
            Scribe_Values.Look(ref setupDefName, "historicalWorldSetupDefName");
            Scribe_Values.Look(ref mapMode, "historicalWorldMapMode", HistoricalWorldMapMode.PrototypeWorld);
            Scribe_Values.Look(ref mapVersion, "historicalWorldMapVersion", 0);
            Scribe_Values.Look(ref initialized, "historicalWorldRevealInitialized", false);
            Scribe_Values.Look(ref prototypeMappingCoverageComplete, "prototypeMappingCoverageComplete", false);
            Scribe_Collections.Look(ref regions, "historicalRegionRevealStates", LookMode.Deep);
            Scribe_Collections.Look(ref prototypeMappings, "prototypeTileMappings", LookMode.Deep);
            Scribe_Collections.Look(ref changes, "historicalRevealChanges", LookMode.Deep);
            Scribe_Values.Look(ref nextChangeSequence, "historicalRevealNextChangeSequence", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                regions = regions ?? new List<HistoricalRegionRevealState>();
                prototypeMappings = prototypeMappings ?? new List<HistoricalPrototypeTileMapping>();
                changes = changes ?? new List<HistoricalRevealChangeRecord>();
                nextChangeSequence = Math.Max(nextChangeSequence, changes.Count + 1);
                HistoricalWorldPatchContext.ResetNotifications();
                if (initialized) WarnForMissingDefinitions();
                if (initialized) WarnForMapVersionChange();
                RebuildDerivedCaches();
            }
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            RebuildDerivedCaches();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            if (!initialized || Find.World != world || mapMode == HistoricalWorldMapMode.HistoricalEarth) return;

            foreach (Caravan caravan in Find.WorldObjects.Caravans.Where(item => item != null && item.IsPlayerControlled))
            {
                RevealRegionsAtTile((int)caravan.Tile);
            }
            foreach (Settlement settlement in Find.WorldObjects.Settlements.Where(item => item != null && item.Faction == Faction.OfPlayer))
            {
                RevealRegionsAtTile((int)settlement.Tile);
            }
        }

        public override void WorldComponentOnGUI()
        {
            base.WorldComponentOnGUI();
            if (!initialized || Find.World != world) return;
            HistoricalWorldMapUI.Draw(this);
        }

        internal string NextChangeId()
        {
            string id = "HR_Reveal_" + (scenarioDefName ?? "Unknown") + "_"
                + nextChangeSequence.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
            nextChangeSequence++;
            return id;
        }

        internal void MarkEarthMapperWarningWritten()
        {
            earthMapperWarningWritten = true;
        }

        internal bool EarthMapperWarningWritten => earthMapperWarningWritten;

        internal void RebuildDerivedCaches()
        {
            HistoricalWorldTileAccessCache.Rebuild(this);
            HistoricalWorldFogRenderer.MarkDirty();
        }

        private void RevealRegionsAtTile(int tileId)
        {
            foreach (string regionId in HistoricalGeographyManager.GetRegionsForTile(tileId))
            {
                HistoricalRegionRevealState state = FindRegion(regionId);
                if (state != null && state.Access == HistoricalRegionAccess.Unexplored)
                {
                    HistoricalWorldRevealManager.TryRevealRegion(regionId, HistoricalRevealSource.Exploration, out _);
                }
                else if (state != null && state.Access == HistoricalRegionAccess.Revealed && !state.EverVisited)
                {
                    ChangeAccess(regionId, HistoricalRegionAccess.Revealed, HistoricalRevealSource.Exploration, true, out _);
                }
            }
        }

        private void LoadConfiguredPrototypeMappings()
        {
            if (mapMode != HistoricalWorldMapMode.PrototypeWorld) return;
            HashSet<string> activeRegions = new HashSet<string>(Regions.Select(item => item.RegionId), StringComparer.Ordinal);
            foreach (HistoricalRegionGeographyDef geography in DefDatabase<HistoricalRegionGeographyDef>.AllDefsListForReading
                .Where(item => item.region != null && activeRegions.Contains(item.region.defName)
                    && item.prototypeTileIds != null))
            {
                foreach (int tileId in geography.prototypeTileIds)
                {
                    if (Find.WorldGrid == null || tileId < 0 || tileId >= Find.WorldGrid.TilesCount)
                    {
                        Log.Warning("[Historical RimWorld] Ignored configured prototype tile " + tileId
                            + " for " + geography.region.defName + " because it is outside the current world grid.");
                        continue;
                    }

                    HistoricalPrototypeTileMapping mapping = FindTileMapping(tileId);
                    if (mapping == null)
                    {
                        mapping = new HistoricalPrototypeTileMapping();
                        mapping.Initialize(tileId, null);
                        prototypeMappings.Add(mapping);
                    }
                    mapping.AddRegion(geography.region.defName);
                }
            }
        }

        private void WarnForMissingDefinitions()
        {
            if (HistoricalScenarioRegistry.TryGetById(scenarioDefName, out _) == false)
            {
                Log.Warning("[Historical RimWorld] World reveal state references missing scenario Def " + scenarioDefName + "; saved IDs were preserved.");
            }

            foreach (HistoricalRegionRevealState state in regions.Where(item => item != null))
            {
                if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(state.RegionId) == null)
                {
                    Log.Warning("[Historical RimWorld] Saved reveal state references missing region Def " + state.RegionId + "; the ID was preserved.");
                }
            }

            foreach (HistoricalPrototypeTileMapping mapping in prototypeMappings.Where(item => item != null))
            {
                foreach (string regionId in mapping.RegionIds)
                {
                    if (DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
                    {
                        Log.Warning("[Historical RimWorld] Prototype tile mapping references missing region Def " + regionId + "; the ID was preserved.");
                    }
                }
            }

            if (DefDatabase<HistoricalWorldRevealSetupDef>.GetNamedSilentFail(setupDefName) == null)
            {
                Log.Warning("[Historical RimWorld] Saved world reveal state references missing setup Def " + setupDefName + "; saved IDs were preserved.");
            }
        }

        private void WarnForMapVersionChange()
        {
            HistoricalWorldRevealSetupDef setup = DefDatabase<HistoricalWorldRevealSetupDef>.GetNamedSilentFail(setupDefName);
            if (setup == null || mapVersion == setup.mapVersion) return;
            Log.Warning("[Historical RimWorld] Historical world map version changed from " + mapVersion
                + " to " + setup.mapVersion + "; saved reveal states and tile IDs were preserved and caches rebuilt.");
            mapVersion = setup.mapVersion;
        }
    }
}
