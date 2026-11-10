using System;
using System.Linq;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Forest
{
    public static class ForestSoilErosionUtility
    {
        public const int TicksPerDay = 60000;
        public const int ErosionUpdateInterval = 2500;
        public const float DefaultRepairThreshold = 0.50f;
        public const float PriorityRepairThreshold = 0.25f;
        public const float FullFloorRepairWork = 1200f;

        public static bool IsConstructedFloor(Map map, IntVec3 cell, out TerrainDef floor)
        {
            floor = null;
            if (map == null || !cell.InBounds(map)) return false;
            TerrainDef top = map.terrainGrid.TopTerrainAt(cell);
            if (top == null || top.IsWater) return false;
            bool taggedFloor = top.tags != null && top.tags.Contains("Floor");
            if (!taggedFloor && !(top.layerable && !top.natural)) return false;
            floor = top;
            return true;
        }

        public static bool IsExposedAncientSoil(Map map, IntVec3 cell)
        {
            return map != null && cell.InBounds(map)
                && map.GetComponent<MapComponent_AncientForestSoil>()?.IsAncientForestSoil(cell) == true
                && !IsConstructedFloor(map, cell, out _);
        }

        public static int FloorDurabilityDays(TerrainDef floor)
        {
            string name = floor?.defName ?? string.Empty;
            if (ContainsAny(name, "PackedDirt", "Dirt", "Earth", "Loam")) return 12;
            if (ContainsAny(name, "Wood", "Bark", "Log")) return 20;
            if (ContainsAny(name, "Brick", "TileBrick", "PaverBrick")) return 55;
            if (ContainsAny(name, "Reinforced", "StoneBase", "PavementReinforced", "StoneRoad")) return 80;
            if (ContainsAny(name, "Flagstone", "Paved", "Concrete", "Tile", "Stone", "Slate")) return 40;
            return 40; // Unknown authored floors remain vulnerable; there is no permanent immunity.
        }

        public static float MoistureFactor(Map map)
        {
            if (map?.weatherManager != null && map.weatherManager.RainRate >= 0.35f) return 1.4f;
            if (map?.weatherManager != null && map.weatherManager.RainRate >= 0.05f) return 1.2f;
            float rainfall = map?.TileInfo.rainfall ?? 0f;
            return rainfall < 250f ? 0.8f : 1f;
        }

        public static float RootPressureFactor(Map map, IntVec3 center)
        {
            int matureTrees = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 5f, true))
            {
                if (!cell.InBounds(map)) continue;
                Plant plant = cell.GetPlant(map);
                if (plant != null && !plant.Destroyed && plant.Growth >= 1f
                    && plant.def?.GetModExtension<HR_AncientTreeExtension>() != null)
                    matureTrees++;
            }
            return matureTrees == 0 ? 1f : matureTrees == 1 ? 1.2f : 1.4f;
        }

        public static float BuildingDamageFractionPerDay(Building building)
        {
            var categories = building?.Stuff?.stuffProps?.categories;
            if (categories == null || categories.Count == 0)
                categories = building?.def?.defaultStuff?.stuffProps?.categories;
            if (categories?.Any(category => category?.defName == "Woody") == true) return 0.01f;
            if (categories?.Any(category => category?.defName == "Metallic") == true) return 0.003f;
            if (categories?.Any(category => category?.defName == "Stony") == true) return 0.004f;
            string name = building?.def?.defName ?? string.Empty;
            if (ContainsAny(name, "Wood", "Timber", "Log")) return 0.01f;
            if (ContainsAny(name, "Steel", "Metal", "Silver", "Gold")) return 0.003f;
            return 0.004f;
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            foreach (string fragment in fragments)
                if (value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
