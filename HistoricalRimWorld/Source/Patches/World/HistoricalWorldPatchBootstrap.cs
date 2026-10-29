using System.Reflection;
using HarmonyLib;
using Verse;

namespace HistoricalRimWorld.Patches.World
{
    internal static class HistoricalWorldPatchBootstrap
    {
        private const string HarmonyId = "LiChen.HistoricalRimWorld.World";
        private static bool applied;

        public static void Apply()
        {
            if (applied) return;
            try
            {
                new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
                applied = true;
                Log.Message("[Historical RimWorld] Centralized world and technology patches initialized.");
            }
            catch (System.Exception exception)
            {
                Log.Error("[Historical RimWorld] World patch initialization failed: " + exception);
            }
        }
    }
}
