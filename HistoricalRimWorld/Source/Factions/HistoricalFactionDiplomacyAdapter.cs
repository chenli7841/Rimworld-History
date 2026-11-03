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
            Faction[] scenarioFactions = HistoricalFactionManager.GetScenarioFactionDefs(scenarioId)
                .Select(factionDef => world.factionManager.AllFactionsListForReading
                    .FirstOrDefault(faction => faction != null && faction.def == factionDef))
                .Where(faction => faction != null)
                .Distinct()
                .ToArray();

            for (int first = 0; first < scenarioFactions.Length; first++)
            {
                for (int second = first + 1; second < scenarioFactions.Length; second++)
                {
                    Faction faction = scenarioFactions[first];
                    Faction other = scenarioFactions[second];
                    if (faction.HasGoodwill && other.HasGoodwill)
                    {
                        int goodwillAdjustment = -faction.BaseGoodwillWith(other);
                        if (goodwillAdjustment != 0
                            && !faction.TryAffectGoodwillWith(other, goodwillAdjustment,
                                false, false, null, null))
                        {
                            Log.Warning("[Historical RimWorld] Could not initialize neutral goodwill between "
                                + faction.Name + " and " + other.Name + ".");
                        }
                        continue;
                    }

                    faction.SetRelationDirect(other, FactionRelationKind.Neutral,
                        false, "Historical Eastern Zhou initial relation", null);
                }
            }
        }
    }
}
