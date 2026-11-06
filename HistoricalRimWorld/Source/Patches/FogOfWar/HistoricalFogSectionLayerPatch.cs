using System.Collections.Generic;
using HarmonyLib;
using HistoricalRimWorld.FogOfWar;
using Verse;

namespace HistoricalRimWorld.Patches.FogOfWar
{
    [HarmonyPatch(typeof(Section), MethodType.Constructor, new[] { typeof(IntVec3), typeof(Map) })]
    internal static class HistoricalFogSectionLayerPatch
    {
        [HarmonyPostfix]
        private static void AddHistoricalFogLayer(Section __instance, List<SectionLayer> ___layers)
        {
            if (__instance == null || ___layers == null)
                return;

            for (int i = 0; i < ___layers.Count; i++)
                if (___layers[i] is HistoricalLocalFogSectionLayer)
                    return;

            ___layers.Add(new HistoricalLocalFogSectionLayer(__instance));
        }
    }
}
