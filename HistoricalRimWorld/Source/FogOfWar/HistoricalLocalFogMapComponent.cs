using System;
using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.FogOfWar
{
    /// <summary>Per-map exploration memory and current observer visibility.</summary>
    public sealed class HistoricalLocalFogMapComponent : MapComponent
    {
        private const int DataVersion = 2;
        private const int RefreshIntervalTicks = 10;
        private const int PeriodicRefreshTicks = 250;
        private const int MapSectionSize = 17;
        private bool[] exploredCells;
        private bool[] revealedRoomCells;
        private bool[] visibleCells;
        private bool[] nextVisibleCells;
        private bool[] dirtyFogSections;
        private int fogSectionColumns;
        private int fogSectionRows;
        private int savedWidth;
        private int savedHeight;
        private int dataVersion;
        private string encodedExploration;
        private string encodedRevealedRooms;
        private bool dirty = true;
        private bool wasEnabled;
        private int lastUpdateTick = -1;
        private int lastFullRefreshTick = -1;
        private int lastVisionSignature;
        private bool environmentCacheInitialized;
        private bool cachedIsDay;
        private string cachedWeatherDefName;
        private float cachedWeatherMultiplier = HistoricalVisibilitySettings.ClearWeatherMultiplier;
        private int activeVisionSourceCount;
        private long lastRefreshMilliseconds;
        private int refreshCount;

        public HistoricalLocalFogMapComponent(Map map) : base(map)
        {
            AllocateArrays();
        }

        public bool IsExplored(IntVec3 cell)
        {
            int index = IndexOf(cell);
            return index >= 0 && exploredCells[index];
        }

        public bool IsCurrentlyVisible(IntVec3 cell)
        {
            int index = IndexOf(cell);
            return index >= 0 && visibleCells[index];
        }

        public int ExploredCellCount
        {
            get { int count = 0; for (int i = 0; i < exploredCells.Length; i++) if (exploredCells[i]) count++; return count; }
        }

        public int VisibleCellCount
        {
            get { int count = 0; for (int i = 0; i < visibleCells.Length; i++) if (visibleCells[i]) count++; return count; }
        }

        public int ActiveVisionSourceCount => activeVisionSourceCount;
        public long LastRefreshMilliseconds => lastRefreshMilliseconds;
        public int RefreshCount => refreshCount;

        public void RevealEntireMap()
        {
            for (int i = 0; i < exploredCells.Length; i++) exploredCells[i] = true;
            dirty = true;
            MarkAllFogSectionsDirty();
            FlushDirtyFogSections();
        }

        public void ResetExploration()
        {
            Array.Clear(exploredCells, 0, exploredCells.Length);
            Array.Clear(revealedRoomCells, 0, revealedRoomCells.Length);
            dirty = true;
            MarkAllFogSectionsDirty();
            FlushDirtyFogSections();
        }

        public void RevealCell(IntVec3 cell)
        {
            int index = IndexOf(cell);
            if (index < 0) return;
            exploredCells[index] = true;
            visibleCells[index] = true;
            MarkFogSectionDirty(index);
            FlushDirtyFogSections();
            dirty = true;
        }

        public void RevealArea(IntVec3 center, int radius)
        {
            if (!center.InBounds(map) || radius < 0) return;
            int radiusSquared = radius * radius;
            int minX = Math.Max(0, center.x - radius);
            int maxX = Math.Min(map.Size.x - 1, center.x + radius);
            int minZ = Math.Max(0, center.z - radius);
            int maxZ = Math.Min(map.Size.z - 1, center.z + radius);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - center.x;
                int dz = z - center.z;
                if (dx * dx + dz * dz > radiusSquared) continue;
                int index = z * map.Size.x + x;
                bool changed = !exploredCells[index] || !visibleCells[index];
                exploredCells[index] = true;
                visibleCells[index] = true;
                if (changed) MarkFogSectionDirty(index);
            }
            FlushDirtyFogSections();
            dirty = true;
        }

        public void MarkVisibilityDirty()
        {
            dirty = true;
            MarkAllFogSectionsDirty();
            FlushDirtyFogSections();
        }

        /// <summary>Forest and weather visibility modifiers are intentionally neutral in P1.</summary>
        public float GetVisibilityMultiplier(IntVec3 cell) => cell.InBounds(map) ? 1f : 0f;

        public void RefreshVisibility()
        {
            if (map == null || map.Disposed) return;
            int started = Environment.TickCount;
            Array.Clear(nextVisibleCells, 0, nextVisibleCells.Length);
            activeVisionSourceCount = 0;

            System.Collections.Generic.IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns != null)
            {
                HistoricalRimWorldSettings settings = HistoricalLocalFogSettings.Current;
                UpdateEnvironmentCache();
                float dayRadius = Mathf.Clamp(settings?.DayVisionRadius ?? 12f, 0f, 30f);
                float nightRadius = Mathf.Clamp(settings?.NightVisionRadius ?? 4f, 0f, 30f);
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (!IsVisionSource(pawn, settings)) continue;
                    activeVisionSourceCount++;
                    RevealPawnCell(pawn.Position);

                    float sight = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Sight) ?? 1f;
                    sight = Mathf.Clamp01(sight);
                    float consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
                    if (sight <= 0f || consciousness <= 0.1f) continue;

                    bool dayNightEnabled = settings?.EnableDayNightVision == true;
                    bool isDay = dayNightEnabled && cachedIsDay;
                    float baseRadius = dayNightEnabled ? (isDay ? dayRadius : nightRadius) : dayRadius;
                    if (pawn.Downed)
                    {
                        float downedDayRadius = Mathf.Min(2f, dayRadius);
                        float downedNightRadius = Mathf.Min(1f, nightRadius);
                        baseRadius = dayNightEnabled ? (isDay ? downedDayRadius : downedNightRadius) : downedDayRadius;
                    }
                    if (sight <= 0f) continue;

                    // Preserve the pre-existing health-capacity penalty as part of R0,
                    // then apply the requested integer radius formula.
                    float effectiveBaseRadius = baseRadius * sight;
                    float dayMultiplier = isDay
                        ? HistoricalVisibilitySettings.DayMultiplier
                        : HistoricalVisibilitySettings.NightMultiplier;
                    int radius = HistoricalVisibilityRadiusUtility.CalculateRadius(
                        effectiveBaseRadius,
                        dayMultiplier,
                        cachedWeatherMultiplier,
                        HistoricalVisibilityRadiusUtility.GetEffectiveSkill(pawn));
                    RevealVisibleRadius(pawn.Position, radius, settings?.EnableLineOfSightBlocking == true);
                }
            }

            for (int i = 0; i < nextVisibleCells.Length; i++)
            {
                bool visibleChanged = nextVisibleCells[i] != visibleCells[i];
                bool becameExplored = nextVisibleCells[i] && !exploredCells[i];
                if (visibleChanged || becameExplored) MarkFogSectionDirty(i);
                if (nextVisibleCells[i]) exploredCells[i] = true;
            }
            RevealPlayerRooms();
            bool[] oldVisibleCells = visibleCells;
            visibleCells = nextVisibleCells;
            nextVisibleCells = oldVisibleCells;

            lastRefreshMilliseconds = Environment.TickCount - started;
            refreshCount++;
            lastFullRefreshTick = Find.TickManager?.TicksGame ?? 0;
            dirty = false;
            FlushDirtyFogSections();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (tick % RefreshIntervalTicks != 0 || lastUpdateTick == tick) return;
            lastUpdateTick = tick;

            bool enabled = HistoricalLocalFogOfWarManager.IsEnabled;
            if (enabled != wasEnabled)
            {
                wasEnabled = enabled;
                dirty = true;
                if (enabled) MarkAllFogSectionsDirty();
                FlushDirtyFogSections();
            }
            if (!enabled)
                return;

            bool environmentChanged = UpdateEnvironmentCache();
            int signature = GetVisionSignature(cachedIsDay, cachedWeatherDefName);
            if (environmentChanged || signature != lastVisionSignature
                || tick - lastFullRefreshTick >= PeriodicRefreshTicks)
                dirty = true;
            if (dirty) RefreshVisibility();
            lastVisionSignature = signature;
        }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            if (!Prefs.DevMode || HistoricalLocalFogSettings.Current?.EnableFogDebugVisualization != true) return;
            GUI.color = Color.white;
            GUI.Label(new Rect(12f, 12f, 360f, 100f),
                "HR Fog | Map " + map.Size.x + "x" + map.Size.z
                + " | Explored " + ExploredCellCount
                + " | Visible " + VisibleCellCount
                + " | Sources " + activeVisionSourceCount
                + " | Last refresh " + lastRefreshMilliseconds + " ms"
                + " | Refreshes " + refreshCount);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                encodedExploration = EncodeExploration();
                encodedRevealedRooms = EncodeCellArray(revealedRoomCells);
                savedWidth = map.Size.x;
                savedHeight = map.Size.z;
                dataVersion = DataVersion;
            }
            Scribe_Values.Look(ref dataVersion, "hrLocalFogDataVersion", 0);
            Scribe_Values.Look(ref savedWidth, "hrLocalFogWidth", 0);
            Scribe_Values.Look(ref savedHeight, "hrLocalFogHeight", 0);
            Scribe_Values.Look(ref encodedExploration, "hrLocalFogExploredCells");
            Scribe_Values.Look(ref encodedRevealedRooms, "hrLocalFogRevealedRoomCells");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                AllocateArrays();
                bool valid = savedWidth == map.Size.x && savedHeight == map.Size.z
                    && DecodeExploration(encodedExploration);
                if (valid && dataVersion >= 2)
                    DecodeCellArray(encodedRevealedRooms, revealedRoomCells);
                if (!valid && HistoricalLocalFogSettings.Current?.LegacyMapsFullyExplored == true)
                {
                    for (int i = 0; i < exploredCells.Length; i++) exploredCells[i] = true;
                }
                if (dataVersion > DataVersion)
                    Log.Warning("[Historical RimWorld] Local fog data was written by a newer version; applying safe explored-map migration.");
                Array.Clear(visibleCells, 0, visibleCells.Length);
                Array.Clear(nextVisibleCells, 0, nextVisibleCells.Length);
                dirty = true;
            }
        }

        internal byte FogAlphaAt(int index)
        {
            if ((uint)index >= exploredCells.Length || visibleCells[index] || revealedRoomCells[index]) return 0;
            HistoricalRimWorldSettings settings = HistoricalLocalFogSettings.Current;
            float opacity = exploredCells[index]
                ? settings?.ExploredFogOpacity ?? 0.65f
                : settings?.UnexploredFogOpacity ?? 1f;
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * byte.MaxValue);
        }

        private bool IsVisionSource(Pawn pawn, HistoricalRimWorldSettings settings)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map || pawn.Faction != Faction.OfPlayer
                || pawn.InContainerEnclosed || pawn.health == null || pawn.health.capacities == null) return false;
            if (pawn.RaceProps.Humanlike) return true;
            return settings?.TamedAnimalsProvideVision == true && pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer;
        }

        private void RevealPawnCell(IntVec3 position)
        {
            int index = IndexOf(position);
            if (index >= 0) nextVisibleCells[index] = true;
        }

        private void RevealVisibleRadius(IntVec3 origin, int radius, bool checkLineOfSight)
        {
            int radiusSquared = radius * radius;
            int minX = Math.Max(0, origin.x - radius);
            int maxX = Math.Min(map.Size.x - 1, origin.x + radius);
            int minZ = Math.Max(0, origin.z - radius);
            int maxZ = Math.Min(map.Size.z - 1, origin.z + radius);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - origin.x;
                int dz = z - origin.z;
                if (dx * dx + dz * dz > radiusSquared) continue;
                IntVec3 cell = new IntVec3(x, 0, z);
                if (checkLineOfSight && cell != origin && !GenSight.LineOfSight(origin, cell, map)) continue;
                nextVisibleCells[z * map.Size.x + x] = true;
            }
        }

        private bool UpdateEnvironmentCache()
        {
            bool dayNightEnabled = HistoricalLocalFogSettings.Current?.EnableDayNightVision == true;
            // CurSkyGlow is map-local and already drives the existing fog day/night system.
            // A threshold gives the requested discrete day/night transition without per-tick radius drift.
            bool isDay = dayNightEnabled && (map.skyManager?.CurSkyGlow ?? 1f) >= 0.5f;
            WeatherDef weather = map.weatherManager?.curWeather;
            string weatherDefName = weather?.defName ?? string.Empty;
            float weatherMultiplier = HistoricalVisibilityRadiusUtility.GetWeatherMultiplier(weather);

            bool changed = !environmentCacheInitialized || isDay != cachedIsDay
                || weatherDefName != cachedWeatherDefName
                || !Mathf.Approximately(weatherMultiplier, cachedWeatherMultiplier);
            environmentCacheInitialized = true;
            cachedIsDay = isDay;
            cachedWeatherDefName = weatherDefName;
            cachedWeatherMultiplier = weatherMultiplier;
            return changed;
        }

        private int GetVisionSignature(bool isDay, string weatherDefName)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (isDay ? 1 : 0);
                hash = hash * 31 + (weatherDefName?.GetHashCode() ?? 0);
                System.Collections.Generic.IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
                if (pawns == null) return hash;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn == null || pawn.Faction != Faction.OfPlayer) continue;
                    hash = hash * 31 + pawn.thingIDNumber;
                    hash = hash * 31 + pawn.Position.GetHashCode();
                    hash = hash * 31 + (pawn.Downed ? 1 : 0);
                    hash = hash * 31 + (pawn.Dead ? 1 : 0);
                    hash = hash * 31 + Mathf.RoundToInt((pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Sight) ?? 1f) * 100f);
                    hash = hash * 31 + HistoricalVisibilityRadiusUtility.CalculateSkillBonus(
                        HistoricalVisibilityRadiusUtility.GetEffectiveSkill(pawn));
                }
                return hash;
            }
        }

        private int IndexOf(IntVec3 cell)
        {
            return cell.InBounds(map) ? map.cellIndices.CellToIndex(cell) : -1;
        }

        private void AllocateArrays()
        {
            int cellCount = map.Size.x * map.Size.z;
            if (exploredCells == null || exploredCells.Length != cellCount) exploredCells = new bool[cellCount];
            if (revealedRoomCells == null || revealedRoomCells.Length != cellCount) revealedRoomCells = new bool[cellCount];
            if (visibleCells == null || visibleCells.Length != cellCount) visibleCells = new bool[cellCount];
            if (nextVisibleCells == null || nextVisibleCells.Length != cellCount) nextVisibleCells = new bool[cellCount];
            fogSectionColumns = (map.Size.x + MapSectionSize - 1) / MapSectionSize;
            fogSectionRows = (map.Size.z + MapSectionSize - 1) / MapSectionSize;
            if (dirtyFogSections == null || dirtyFogSections.Length != fogSectionColumns * fogSectionRows)
                dirtyFogSections = new bool[fogSectionColumns * fogSectionRows];
        }

        private string EncodeExploration()
        {
            return EncodeCellArray(exploredCells);
        }

        private static string EncodeCellArray(bool[] cells)
        {
            byte[] bytes = new byte[(cells.Length + 7) / 8];
            for (int i = 0; i < cells.Length; i++)
                if (cells[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            return Convert.ToBase64String(bytes);
        }

        private bool DecodeExploration(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return false;
            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                if (bytes.Length != (exploredCells.Length + 7) / 8) return false;
                for (int i = 0; i < exploredCells.Length; i++)
                    exploredCells[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool DecodeCellArray(string encoded, bool[] cells)
        {
            if (string.IsNullOrEmpty(encoded)) return false;
            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                if (bytes.Length != (cells.Length + 7) / 8) return false;
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private void RevealPlayerRooms()
        {
            IReadOnlyList<Room> rooms = map.regionGrid?.AllRooms;
            if (rooms == null) return;

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                Room room = rooms[roomIndex];
                if (room == null || room.Dereferenced || !room.ProperRoom
                    || room.PsychologicallyOutdoors
                    || !ContainsPlayerRoomOwnerOrBuilding(room))
                    continue;

                foreach (IntVec3 cell in room.Cells)
                {
                    int index = IndexOf(cell);
                    if (index < 0 || revealedRoomCells[index]) continue;
                    revealedRoomCells[index] = true;
                    exploredCells[index] = true;
                    MarkFogSectionDirty(index);
                }
            }
        }

        private static bool ContainsPlayerRoomOwnerOrBuilding(Room room)
        {
            foreach (Pawn owner in room.Owners)
                if (owner?.Faction == Faction.OfPlayer)
                    return true;

            foreach (Thing thing in room.ContainedAndAdjacentThings)
                if (thing is Building && thing.Faction == Faction.OfPlayer)
                    return true;

            return false;
        }

        private void MarkAllFogSectionsDirty()
        {
            if (dirtyFogSections == null) return;
            for (int i = 0; i < dirtyFogSections.Length; i++) dirtyFogSections[i] = true;
        }

        private void MarkFogSectionDirty(int cellIndex)
        {
            int x = cellIndex % map.Size.x;
            int z = cellIndex / map.Size.x;
            int sectionIndex = (z / MapSectionSize) * fogSectionColumns + x / MapSectionSize;
            if ((uint)sectionIndex < dirtyFogSections.Length) dirtyFogSections[sectionIndex] = true;
        }

        private void FlushDirtyFogSections()
        {
            if (dirtyFogSections == null || map?.mapDrawer == null) return;
            ulong meshFlag = HistoricalMapMeshFlagDefOf.LocalFog;
            for (int sectionIndex = 0; sectionIndex < dirtyFogSections.Length; sectionIndex++)
            {
                if (!dirtyFogSections[sectionIndex]) continue;
                int sectionX = sectionIndex % fogSectionColumns;
                int sectionZ = sectionIndex / fogSectionColumns;
                map.mapDrawer.MapMeshDirty(
                    new IntVec3(sectionX * MapSectionSize, 0, sectionZ * MapSectionSize),
                    meshFlag);
                dirtyFogSections[sectionIndex] = false;
            }
        }
    }
}
