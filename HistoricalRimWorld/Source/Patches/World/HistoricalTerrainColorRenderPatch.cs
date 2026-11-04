using HarmonyLib;
using HistoricalRimWorld.World;
using RimWorld.Planet;

namespace HistoricalRimWorld.Patches.World
{
    // Draw only after the native terrain layer. The overlay follows each tile's
    // actual polygon and leaves unconfigured tiles on their vanilla materials.
    [HarmonyPatch(typeof(WorldDrawLayerBase), nameof(WorldDrawLayerBase.Render))]
    internal static class HistoricalTerrainColorRenderPatch
    {
        private static void Postfix(WorldDrawLayerBase __instance)
        {
            if (__instance is WorldDrawLayer_Terrain)
                HistoricalWorldTerrainColorRenderer.DrawAfterWorldLayers();
        }
    }
}
