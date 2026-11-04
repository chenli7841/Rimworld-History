using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.WorldFeatures;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.World
{
    public static class HistoricalWorldTerrainColorManager
    {
        private struct EffectiveColorRecord
        {
            public Color Color;
            public string Hex;
            public string Source;
            public int Priority;
            public int FeatureId;
            public EffectiveColorRecord(Color color, string hex, string source, int priority, int featureId)
            { Color = color; Hex = hex; Source = source; Priority = priority; FeatureId = featureId; }
        }

        private static RimWorldWorld cachedWorld;
        private static bool cacheDirty = true;
        private static readonly Dictionary<int, EffectiveColorRecord> effective = new Dictionary<int, EffectiveColorRecord>();

        public static bool TryParseHex(string text, out Color color, out string normalized)
        {
            color = Color.white;
            normalized = null;
            string value = (text ?? string.Empty).Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal)) value = "#" + value;
            if (value.Length != 7 || !ColorUtility.TryParseHtmlString(value, out color)) return false;
            normalized = "#" + ColorUtility.ToHtmlStringRGB(color).ToUpperInvariant();
            return true;
        }

        public static Color? GetTileColorOverride(RimWorldWorld world, int tileId)
        {
            HistoricalMapTileColorOverride entry = world?.GetComponent<HistoricalMapEditWorldComponent>()
                ?.GetTileColorOverride(tileId);
            return TryParseHex(entry?.ColorHex, out Color color, out _) ? color : (Color?)null;
        }

        public static Color? GetFeatureColorOverride(RimWorldWorld world, int featureId)
        {
            HistoricalWorldFeatureColorOverride entry = world?.GetComponent<HistoricalWorldTerrainColorWorldComponent>()
                ?.GetFeatureOverride(featureId);
            return TryParseHex(entry?.ColorHex, out Color color, out _) ? color : (Color?)null;
        }

        public static Color? GetEffectiveColor(RimWorldWorld world, int tileId) =>
            TryGetEffectiveColor(world, tileId, out Color color, out _, out _) ? color : (Color?)null;

        public static bool TryGetEffectiveColor(RimWorldWorld world, int tileId,
            out Color color, out string source, out string hex)
        {
            EnsureCache(world);
            if (effective.TryGetValue(tileId, out EffectiveColorRecord record))
            { color = record.Color; source = record.Source; hex = record.Hex; return true; }
            color = Color.white;
            source = "Original Biome Color";
            hex = null;
            return false;
        }

        public static bool TrySetTileColor(RimWorldWorld world, int tileId, string hex, out string error)
        {
            error = null;
            if (world?.grid?.Surface == null || tileId < 0 || tileId >= world.grid.Surface.TilesCount
                || world.grid.Surface.Tiles[tileId] == null)
            { error = "Select a valid WorldGrid tile first."; return false; }
            HistoricalMapEditWorldComponent edits = world.GetComponent<HistoricalMapEditWorldComponent>();
            HistoricalFactionWorldComponent faction = world.GetComponent<HistoricalFactionWorldComponent>();
            if (edits == null || faction?.MapScaleState == null)
            { error = "The historical map edit layer or map scale is unavailable."; return false; }
            string normalized = null;
            if (!string.IsNullOrWhiteSpace(hex) && !TryParseHex(hex, out _, out normalized))
            { error = "Enter a valid RGB hex color such as #526B5B."; return false; }
            HistoricalMapScaleDef scale = DefDatabase<HistoricalMapScaleDef>.GetNamedSilentFail(faction.MapScaleState.ScaleDefName);
            if (scale == null) { error = "The active historical map projection is unavailable."; return false; }
            Vector2 game = world.grid.Surface.LongLatOf(tileId);
            HistoricalGeoCoordinate historical;
            try { historical = new HistoricalMapScaleTransformer(scale)
                .ToHistoricalCoordinate(new HistoricalGameGeoCoordinate(game.y, game.x)); }
            catch (Exception exception) { error = "Could not map tile coordinates: " + exception.Message; return false; }
            edits.SetTileColor(tileId, historical.Longitude, historical.Latitude, normalized);
            Invalidate(world, true);
            return true;
        }

        public static bool TrySetFeatureColor(RimWorldWorld world, WorldFeature feature,
            string hex, int priority, HistoricalMapScaleTransformer transformer, out string error)
        {
            error = null;
            if (world?.grid?.Surface == null || feature == null || world.features?.features?.Contains(feature) != true)
            { error = "Select an active native WorldFeature first."; return false; }
            HistoricalWorldTerrainColorWorldComponent component = world.GetComponent<HistoricalWorldTerrainColorWorldComponent>();
            if (component == null) { error = "HistoricalWorldTerrainColorWorldComponent is unavailable."; return false; }
            string normalized = null;
            if (!string.IsNullOrWhiteSpace(hex) && !TryParseHex(hex, out _, out normalized))
            { error = "Enter a valid RGB hex color such as #526B5B."; return false; }
            List<int> members = WorldFeatureAdapter.GetFeatureTiles(world, feature).OrderBy(id => id).ToList();
            if (members.Count == 0) { error = "The selected WorldFeature has no member tiles."; return false; }
            Vector2 game = world.grid.Surface.LongLatOf(members[0]);
            HistoricalGeoCoordinate historical;
            try
            {
                historical = transformer.ToHistoricalCoordinate(
                    new HistoricalGameGeoCoordinate(game.y, game.x));
            }
            catch (Exception exception)
            { error = "Could not map the feature coordinates: " + exception.Message; return false; }
            component.SetFeatureOverride(feature.uniqueID, feature.def?.defName, feature.name,
                historical.Longitude, historical.Latitude, normalized, priority);
            Invalidate(world, true);
            return true;
        }

        public static void Invalidate(RimWorldWorld world, bool regenerateWorldLayers = false)
        {
            cachedWorld = world;
            cacheDirty = true;
            effective.Clear();
            HistoricalWorldTerrainColorRenderer.MarkDirty();
            if (regenerateWorldLayers) world?.renderer?.SetAllLayersDirty();
        }

        public static bool TryGetFeatureRecord(RimWorldWorld world, WorldFeature feature,
            out HistoricalWorldFeatureColorOverride record)
        {
            record = feature == null ? null : world?.GetComponent<HistoricalWorldTerrainColorWorldComponent>()
                ?.GetFeatureOverride(feature.uniqueID);
            return record != null;
        }

        private static void EnsureCache(RimWorldWorld world)
        {
            if (ReferenceEquals(cachedWorld, world) && !cacheDirty) return;
            cachedWorld = world;
            cacheDirty = false;
            effective.Clear();
            if (world?.grid?.Surface == null) return;

            HistoricalWorldTerrainColorWorldComponent component = world.GetComponent<HistoricalWorldTerrainColorWorldComponent>();
            if (component != null && world.features?.features != null)
            {
                foreach (HistoricalWorldFeatureColorOverride entry in component.FeatureOverrides)
                {
                    if (entry == null || !TryParseHex(entry.ColorHex, out Color color, out string normalized)) continue;
                    WorldFeature feature = world.features.features.FirstOrDefault(item => item != null
                        && ReferenceEquals(item.layer, world.grid.Surface)
                        && item.uniqueID == entry.FeatureUniqueId && item.def?.defName == entry.FeatureDefName);
                    if (feature == null) continue;
                    foreach (int tileId in WorldFeatureAdapter.GetFeatureTiles(world, feature))
                    {
                        if (effective.TryGetValue(tileId, out EffectiveColorRecord previous)
                            && (previous.Priority > entry.Priority
                                || previous.Priority == entry.Priority && previous.FeatureId < entry.FeatureUniqueId)) continue;
                        effective[tileId] = new EffectiveColorRecord(color, normalized,
                            "World Feature (" + feature.name + ")", entry.Priority, entry.FeatureUniqueId);
                    }
                }
            }

            HistoricalMapEditWorldComponent mapEdits = world.GetComponent<HistoricalMapEditWorldComponent>();
            if (mapEdits == null) return;
            foreach (HistoricalMapTileColorOverride entry in mapEdits.TileColorOverrides)
            {
                if (entry == null || effective.ContainsKey(entry.TileId)
                    || !TryParseHex(entry.ColorHex, out Color color, out string normalized)) continue;
                effective[entry.TileId] = new EffectiveColorRecord(color, normalized,
                    "Tile Override", int.MinValue, int.MaxValue);
            }
        }
    }
}
