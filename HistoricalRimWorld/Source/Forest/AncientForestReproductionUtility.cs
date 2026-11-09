using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Shared habitat checks and soil-transition multipliers for ancient trees.</summary>
    public static class AncientForestReproductionUtility
    {
        public static float GetReproductionMultiplier(bool parentLivingSoil, bool targetLivingSoil)
        {
            if (parentLivingSoil && targetLivingSoil) return AncientForestGrowthSettings.LivingToLivingMultiplier;
            if (parentLivingSoil) return AncientForestGrowthSettings.LivingToNormalMultiplier;
            if (targetLivingSoil) return AncientForestGrowthSettings.NormalToLivingMultiplier;
            return AncientForestGrowthSettings.NormalToNormalMultiplier;
        }

        public static bool CanSpawnAncientTree(IntVec3 cell, Map map, ThingDef treeDef)
        {
            if (!HistoricalScenarioManager.IsScenarioActive || map == null || !cell.InBounds(map)
                || !cell.Standable(map) || cell.GetEdifice(map) != null || cell.GetPlant(map) != null)
                return false;

            if (map.zoneManager?.ZoneAt(cell) is Zone_Growing) return false;

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            MapComponent_AncientForestSoil soil = map.GetComponent<MapComponent_AncientForestSoil>();
            bool livingSoil = soil != null && soil.IsAncientForestSoil(cell);
            if (!livingSoil && (terrain == null || !terrain.natural || terrain.IsWater || terrain.fertility <= 0.05f
                || terrain.tags == null || !terrain.tags.Contains("Soil")))
                return false;

            // TerrainAt returns the visible top layer, so a constructed floor blocks both
            // active and ordinary soil even though the permanent active-soil bit is retained.
            if (terrain == null || terrain.tags == null || terrain.tags.Contains("Floor") || terrain.IsWater)
                return false;

            TerrainAffordanceDef growSoil = DefDatabase<TerrainAffordanceDef>.GetNamedSilentFail("GrowSoil");
            if (growSoil == null || terrain.affordances == null || !terrain.affordances.Contains(growSoil))
                return false;

            if (treeDef?.plant != null && terrain.fertility < treeDef.plant.fertilityMin)
                return false;

            // GenSpawn must never need to wipe an item, pawn, blueprint, or task object.
            foreach (Thing thing in map.thingGrid.ThingsListAtFast(cell))
            {
                if (thing == null || thing.Destroyed || thing is Plant || thing.def.category == ThingCategory.Filth
                    || thing.def.category == ThingCategory.Mote)
                    continue;
                return false;
            }

            return true;
        }
    }
}
