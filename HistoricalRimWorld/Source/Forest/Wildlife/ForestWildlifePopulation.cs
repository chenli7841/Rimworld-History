using Verse;

namespace HistoricalRimWorld.Forest
{
    /// <summary>Cached population snapshot. CurrentCount is recalculated from living wild pawns.</summary>
    public sealed class ForestWildlifePopulation : IExposable
    {
        public string SpeciesDefName;
        public int ForestRegionId;
        public int ForestRegionAnchorCell;
        public int CurrentCount;
        public int TargetCount;
        public IntVec3 HabitatCenter = IntVec3.Invalid;
        public float FoodAvailability;
        public float DisturbanceLevel;
        public float HabitatSuitability;
        public int LastPopulationUpdateTick;
        public int LastMigrationOrderTick;
        public int LastLivestockHuntTick;
        public bool MigrationPressure;

        public void ExposeData()
        {
            Scribe_Values.Look(ref SpeciesDefName, "speciesDefName");
            Scribe_Values.Look(ref ForestRegionId, "forestRegionId", 0);
            Scribe_Values.Look(ref ForestRegionAnchorCell, "forestRegionAnchorCell", -1);
            Scribe_Values.Look(ref CurrentCount, "currentCount", 0);
            Scribe_Values.Look(ref TargetCount, "targetCount", 0);
            Scribe_Values.Look(ref HabitatCenter, "habitatCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref FoodAvailability, "foodAvailability", 0f);
            Scribe_Values.Look(ref DisturbanceLevel, "disturbanceLevel", 0f);
            Scribe_Values.Look(ref HabitatSuitability, "habitatSuitability", 0f);
            Scribe_Values.Look(ref LastPopulationUpdateTick, "lastPopulationUpdateTick", 0);
            Scribe_Values.Look(ref LastMigrationOrderTick, "lastMigrationOrderTick", 0);
            Scribe_Values.Look(ref LastLivestockHuntTick, "lastLivestockHuntTick", 0);
            Scribe_Values.Look(ref MigrationPressure, "migrationPressure", false);
        }
    }
}
