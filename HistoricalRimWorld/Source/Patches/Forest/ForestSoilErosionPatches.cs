using HarmonyLib;
using HistoricalRimWorld.Forest;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Patches.Forest
{
    [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTerrain), new[] { typeof(IntVec3), typeof(TerrainDef) })]
    internal static class TerrainGrid_SetTerrain_ForestSoilErosionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TerrainGrid __instance, IntVec3 c)
        {
            ForestSoilErosionRegistry.NotifyTerrainChanged(__instance, c);
        }
    }

    [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.RemoveTopLayer), new[] { typeof(IntVec3), typeof(bool) })]
    internal static class TerrainGrid_RemoveTopLayer_ForestSoilErosionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TerrainGrid __instance, IntVec3 c)
        {
            ForestSoilErosionRegistry.NotifyTerrainChanged(__instance, c);
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.SpawnSetup), new[] { typeof(Map), typeof(bool) })]
    internal static class Building_SpawnSetup_ForestSoilErosionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Building __instance, Map map)
        {
            map?.GetComponent<MapComponent_ForestSoilErosion>()?.NotifyBuildingSpawned(__instance);
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.DeSpawn), new[] { typeof(DestroyMode) })]
    internal static class Building_DeSpawn_ForestSoilErosionPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Building __instance)
        {
            __instance?.Map?.GetComponent<MapComponent_ForestSoilErosion>()?.NotifyBuildingDespawned(__instance);
        }
    }
}
