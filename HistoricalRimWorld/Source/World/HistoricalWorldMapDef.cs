using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HistoricalRimWorld.World
{
    /// <summary>
    /// Editable, seed-independent surface terrain data for a historical world.
    /// Unlisted tiles inherit defaults; overrides resolve by nearest surface tile center.
    /// </summary>
    public sealed class HistoricalWorldMapDef : Def
    {
        public BiomeDef defaultBiome;
        public float defaultElevation = 0.1f;
        public Hilliness defaultHilliness = Hilliness.Flat;
        public List<HistoricalWorldTileOverride> tileOverrides = new List<HistoricalWorldTileOverride>();
        public List<HistoricalWorldMapLocation> locations = new List<HistoricalWorldMapLocation>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (defaultBiome == null) yield return "defaultBiome is required.";
            else if (defaultBiome.impassable || !defaultBiome.canBuildBase)
                yield return "defaultBiome must be passable and allow settlements.";
            if (defaultElevation <= 0f) yield return "defaultElevation must be above zero so unspecified tiles remain land.";
            if (!Enum.IsDefined(typeof(Hilliness), defaultHilliness)) yield return "defaultHilliness is invalid.";
            if (tileOverrides == null)
            {
                yield return "tileOverrides cannot be null.";
                yield break;
            }

            foreach (HistoricalWorldTileOverride tile in tileOverrides.Where(item => item != null))
            {
                if (tile.longitude < -180f || tile.longitude > 180f)
                    yield return "A tile override longitude must be between -180 and 180 degrees.";
                if (tile.latitude < -90f || tile.latitude > 90f)
                    yield return "A tile override latitude must be between -90 and 90 degrees.";
                if (tile.biome == null)
                    yield return "A tile override at " + tile.longitude + ", " + tile.latitude + " requires a biome.";
                if (!Enum.IsDefined(typeof(Hilliness), tile.hilliness))
                    yield return "A tile override at " + tile.longitude + ", " + tile.latitude + " has invalid hilliness.";
            }

            if (locations == null)
            {
                yield return "locations cannot be null.";
                yield break;
            }

            HashSet<string> locationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (HistoricalWorldMapLocation location in locations.Where(item => item != null))
            {
                if (string.IsNullOrWhiteSpace(location.locationId))
                    yield return "Each map location requires a locationId.";
                else if (!locationIds.Add(location.locationId))
                    yield return "Map locationId '" + location.locationId + "' is duplicated.";
                if (string.IsNullOrWhiteSpace(location.label))
                    yield return "Map location '" + location.locationId + "' requires a label.";
                if (string.IsNullOrWhiteSpace(location.description))
                    yield return "Map location '" + location.locationId + "' requires a description.";
                if (location.longitude < -180f || location.longitude > 180f)
                    yield return "Map location '" + location.locationId + "' longitude must be between -180 and 180 degrees.";
                if (location.latitude < -90f || location.latitude > 90f)
                    yield return "Map location '" + location.locationId + "' latitude must be between -90 and 90 degrees.";
                if (location.worldObjectDef == null)
                    yield return "Map location '" + location.locationId + "' requires a worldObjectDef.";
            }
        }
    }

    /// <summary>Full terrain values assigned to the nearest RimWorld tile center.</summary>
    public sealed class HistoricalWorldTileOverride
    {
        public float longitude;
        public float latitude;
        public BiomeDef biome;
        public float elevation;
        public Hilliness hilliness = Hilliness.Flat;
    }

    /// <summary>A named place shown on the world map at a geographic coordinate.</summary>
    public sealed class HistoricalWorldMapLocation
    {
        public string locationId;
        public string label;
        public string description;
        public float longitude;
        public float latitude;
        public WorldObjectDef worldObjectDef;
        public FactionDef factionDef;
    }
}
