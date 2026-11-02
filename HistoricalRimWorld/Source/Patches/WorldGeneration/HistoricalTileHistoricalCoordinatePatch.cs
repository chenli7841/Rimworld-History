using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.World;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HistoricalRimWorld.Patches.WorldGeneration
{
    /// <summary>Adds the inverse-projected historical coordinate to RimWorld's selected-tile inspect text.</summary>
    [HarmonyPatch(typeof(WorldInspectPane), "get_TileInspectString")]
    internal static class HistoricalTileHistoricalCoordinatePatch
    {
        private static bool loggedFailure;

        private static void Postfix(ref string __result)
        {
            HistoricalFactionWorldComponent factionState = Find.World?.GetComponent<HistoricalFactionWorldComponent>();
            if (factionState?.GenerationMode != HistoricalFactionGenerationMode.HistoricalScenario
                || factionState.MapScaleState == null
                || !HistoricalMapEditManager.TryGetSelectedTileId(out int tileId)
                || Find.World?.grid?.Surface == null
                || tileId < 0 || tileId >= Find.World.grid.TilesCount)
                return;

            HistoricalMapScaleDef scale = DefDatabase<HistoricalMapScaleDef>
                .GetNamedSilentFail(factionState.MapScaleState.ScaleDefName);
            if (scale == null) return;

            try
            {
                Vector2 gameCoordinate = Find.World.grid.Surface.LongLatOf(tileId);
                HistoricalGeoCoordinate historical = new HistoricalMapScaleTransformer(scale)
                    .ToHistoricalCoordinate(new HistoricalGameGeoCoordinate(gameCoordinate.y, gameCoordinate.x));
                string scaleLabel = scale.scaleFactor.ToString("0.##", CultureInfo.InvariantCulture);
                __result = (__result ?? string.Empty) + "\n历史坐标（比例尺 " + scaleLabel + "×）："
                    + historical.Longitude.ToString("0.00000", CultureInfo.InvariantCulture) + "°E, "
                    + historical.Latitude.ToString("0.00000", CultureInfo.InvariantCulture) + "°N";
                IReadOnlyList<HistoricalGeographicRegionData> features =
                    HistoricalGeographicRegionManager.GetRegionsForTile(tileId);
                if (features.Count > 0)
                    __result += "\n地理区域：" + string.Join("、", features.Select(HistoricalGeographicRegionManager.ResolveName));
            }
            catch (Exception exception)
            {
                if (loggedFailure) return;
                loggedFailure = true;
                Log.Warning("[Historical RimWorld] Could not show the selected tile's historical coordinate: "
                    + exception.Message);
            }
        }
    }
}
