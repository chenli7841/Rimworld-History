using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Geography
{
    public static class HistoricalGeographyManager
    {
        private static HistoricalWorldRevealWorldComponent cachedComponent;
        private static IHistoricalWorldGeoMapper cachedMapper;

        public static HistoricalWorldMapMode MapMode =>
            HistoricalWorldRevealManager.GetCurrentComponent()?.MapMode ?? HistoricalWorldMapMode.HistoricalEarth;

        public static IHistoricalWorldGeoMapper CurrentMapper
        {
            get
            {
                HistoricalWorldRevealWorldComponent component = HistoricalWorldRevealManager.GetCurrentComponent();
                if (!ReferenceEquals(component, cachedComponent))
                {
                    cachedComponent = component;
                    cachedMapper = null;
                }
                if (component == null) return null;
                if (cachedMapper == null)
                {
                    cachedMapper = component.MapMode == HistoricalWorldMapMode.PrototypeWorld
                        ? (IHistoricalWorldGeoMapper)new PrototypeWorldGeoMapper(component)
                        : new HistoricalEarthGeoMapper();
                }
                return cachedMapper;
            }
        }

        public static IReadOnlyList<string> GetRegionsForTile(int tileId)
        {
            IHistoricalWorldGeoMapper mapper = CurrentMapper;
            if (mapper == null || !mapper.IsAvailable) return new List<string>().AsReadOnly();
            HistoricalWorldRevealWorldComponent component = HistoricalWorldRevealManager.GetCurrentComponent();
            HistoricalPrototypeTileMapping mapping = component?.FindTileMapping(tileId);
            if (mapping == null) return new List<string>().AsReadOnly();
            return mapping.RegionIds.OrderBy(id => id, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        public static string GetPrimaryRegionForTile(int tileId)
        {
            IReadOnlyList<string> regionIds = GetRegionsForTile(tileId);
            return regionIds.Select(FindGeographyDef)
                .Where(item => item != null)
                .OrderByDescending(item => item.primaryRegionPriority)
                .ThenBy(item => item.polygons == null || item.polygons.Count == 0
                    ? double.PositiveInfinity
                    : item.polygons.Min(polygon => polygon.ApproximateArea))
                .ThenBy(item => item.region.defName, StringComparer.Ordinal)
                .Select(item => item.region.defName)
                .FirstOrDefault();
        }

        private static HistoricalRegionGeographyDef FindGeographyDef(string regionId)
        {
            return DefDatabase<HistoricalRegionGeographyDef>.AllDefsListForReading
                .FirstOrDefault(item => item.region != null && item.region.defName == regionId);
        }
    }
}
