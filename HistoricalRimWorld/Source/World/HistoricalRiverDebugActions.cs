using System.Linq;
using LudeonTK;
using Verse;

namespace HistoricalRimWorld.World
{
    public static class HistoricalRiverDebugActions
    {
        [DebugAction("Historical RimWorld", "HR: Inspect Historical River Registry", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void InspectHistoricalRiverRegistry()
        {
            if (!Prefs.DevMode) return;
            HistoricalRiverWorldComponent component = Find.World?.GetComponent<HistoricalRiverWorldComponent>();
            if (component == null)
            {
                Log.Warning("[HR-017] HistoricalRiverWorldComponent is unavailable.");
                return;
            }

            foreach (HistoricalRiverDef river in DefDatabase<HistoricalRiverDef>.AllDefsListForReading.OrderBy(item => item.defName))
            {
                HistoricalRiverGeometry geometry = component.GetGeometry(river.defName);
                int tileCount = geometry?.Segments?.SelectMany(segment => segment.PathTiles ?? Enumerable.Empty<int>()).Distinct().Count() ?? 0;
                Log.Message("[HR-017] River=" + river.defName + "; label=" + river.LabelCap
                    + "; segments=" + (geometry?.Segments?.Count ?? 0) + "; indexedTiles=" + tileCount
                    + "; alternateNames=" + string.Join(", ", HistoricalRiverRegistry.GetAlternateNames(river.defName)));
            }
        }
    }
}
