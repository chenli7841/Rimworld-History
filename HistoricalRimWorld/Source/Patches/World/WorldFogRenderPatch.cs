using HarmonyLib;
using HistoricalRimWorld.World;
using RimWorld.Planet;

namespace HistoricalRimWorld.Patches.World
{
    // Verified against RimWorld 1.6 Assembly-CSharp: WorldRenderer.DrawWorldLayers().
    [HarmonyPatch(typeof(WorldRenderer), nameof(WorldRenderer.DrawWorldLayers))]
    internal static class WorldFogRenderPatch
    {
        private static void Postfix()
        {
            HistoricalWorldFogRenderer.Draw();
        }
    }
}
