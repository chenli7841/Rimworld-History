using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.FogOfWar
{
    public static class HistoricalLocalFogOfWarManager
    {
        public static bool IsEnabled => HistoricalLocalFogSettings.Current?.EnableLocalFogOfWar == true
            && HistoricalScenarioManager.IsScenarioActive;

        public static HistoricalLocalFogMapComponent GetComponent(Map map) => map?.GetComponent<HistoricalLocalFogMapComponent>();

        public static bool IsCellVisible(Map map, IntVec3 cell)
        {
            if (!IsEnabled) return true;
            HistoricalLocalFogMapComponent component = GetComponent(map);
            return component == null || component.IsCurrentlyVisible(cell);
        }

        public static bool IsCellExplored(Map map, IntVec3 cell)
        {
            if (!IsEnabled) return true;
            HistoricalLocalFogMapComponent component = GetComponent(map);
            return component == null || component.IsExplored(cell);
        }

        public static bool IsCellVisibleToPlayer(IntVec3 cell, Map map) => IsCellVisible(map, cell);

        public static bool IsCellExploredByPlayer(IntVec3 cell, Map map) => IsCellExplored(map, cell);

        public static bool IsCellVisibleToPlayer(IntVec3 cell) => IsCellVisible(Find.CurrentMap, cell);

        public static bool IsCellExploredByPlayer(IntVec3 cell) => IsCellExplored(Find.CurrentMap, cell);

        public static bool CanPlayerSee(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return false;
            if (!IsEnabled || pawn.Faction == Faction.OfPlayer) return true;
            return IsCellVisible(pawn.Map, pawn.Position);
        }

        public static bool CanPlayerSee(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map == null) return false;
            if (!IsEnabled) return true;
            if (thing is Pawn pawn) return CanPlayerSee(pawn);

            HistoricalLocalFogMapComponent component = GetComponent(thing.Map);
            if (component == null) return true;
            CellRect occupied = thing.OccupiedRect();
            foreach (IntVec3 cell in occupied.Cells)
                if (component.IsCurrentlyVisible(cell)) return true;
            return false;
        }

        public static bool ShouldHide(Thing thing)
        {
            if (!IsEnabled || thing == null || !thing.Spawned || thing.Map == null) return false;
            if (thing is Pawn pawn)
            {
                if (pawn.Faction == Faction.OfPlayer || IsCellVisible(pawn.Map, pawn.Position)) return false;
                HistoricalRimWorldSettings pawnSettings = HistoricalLocalFogSettings.Current;
                if (pawn.RaceProps.Humanlike)
                    return pawnSettings?.HideUnseenHostilePawns == true && pawn.Faction != null && Faction.OfPlayer.HostileTo(pawn.Faction);
                return pawn.RaceProps.Animal && pawn.Faction == null && pawnSettings?.HideUnseenWildAnimals == true;
            }

            if (CanPlayerSee(thing)) return false;
            HistoricalLocalFogMapComponent component = GetComponent(thing.Map);
            if (component != null)
            {
                CellRect rect = thing.OccupiedRect();
                bool overlapsExploredCell = false;
                foreach (IntVec3 cell in rect.Cells)
                    if (component.IsExplored(cell)) { overlapsExploredCell = true; break; }
                if (!overlapsExploredCell) return true;
            }
            return false;
        }

        internal static void MarkAllMapsDirty()
        {
            List<Map> maps = Find.Maps;
            if (maps == null) return;
            for (int i = 0; i < maps.Count; i++)
                maps[i]?.GetComponent<HistoricalLocalFogMapComponent>()?.MarkVisibilityDirty();
        }
    }
}
