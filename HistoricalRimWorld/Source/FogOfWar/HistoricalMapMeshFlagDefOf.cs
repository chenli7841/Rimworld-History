using RimWorld;
using Verse;

namespace HistoricalRimWorld.FogOfWar
{
    [DefOf]
    public static class HistoricalMapMeshFlagDefOf
    {
        public static MapMeshFlagDef LocalFog;

        static HistoricalMapMeshFlagDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HistoricalMapMeshFlagDefOf));
        }
    }
}
