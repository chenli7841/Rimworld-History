using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.Factions
{
    /// <summary>Seeds the vanilla goodwill layer. HR-004 remains the authoritative political model.</summary>
    public static class HistoricalFactionDiplomacyAdapter
    {
        public static void ApplyInitialRelations(RimWorldWorld world, string scenarioId)
        {
            if (world?.factionManager == null) return;
            Faction[] historical = HistoricalFactionManager.GetBindings(scenarioId)
                .Select(binding => world.factionManager.AllFactionsListForReading
                    .FirstOrDefault(faction => faction != null && faction.def == binding.rimWorldFactionDef))
                .Where(faction => faction != null)
                .Distinct()
                .ToArray();

            for (int first = 0; first < historical.Length; first++)
            {
                for (int second = first + 1; second < historical.Length; second++)
                {
                    historical[first].SetRelationDirect(historical[second], FactionRelationKind.Neutral,
                        false, "Historical Eastern Zhou initial relation", null);
                }
            }
        }
    }
}
