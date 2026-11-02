using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.Geography;
using HistoricalRimWorld.Geography.Scaling;
using HistoricalRimWorld.WorldGeneration;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using RimWorldWorld = RimWorld.Planet.World;

namespace HistoricalRimWorld.WorldFeatures
{
    /// <summary>Matches scenario names to existing vanilla WorldFeatures, without creating or moving features.</summary>
    public static class HistoricalWorldFeatureOverrideManager
    {
        private const double AutoApplyScore = 0.85d;
        private const double ReviewScore = 0.60d;
        private static RimWorldWorld pendingLabelRefreshWorld;

        public static int ApplyScenarioOverrides(RimWorldWorld world, string scenarioId, HistoricalMapScaleDef scale)
        {
            if (world?.features?.features == null || world.grid?.Surface == null || scale == null
                || string.IsNullOrWhiteSpace(scenarioId)) return 0;
            HistoricalMapScaleTransformer transformer;
            try { transformer = new HistoricalMapScaleTransformer(scale); }
            catch (Exception exception)
            { Log.Warning("[HR-016] Could not construct historical projection: " + exception.Message); return 0; }

            HistoricalWorldFeatureOverrideWorldComponent component =
                world.GetComponent<HistoricalWorldFeatureOverrideWorldComponent>();
            component?.Initialize(scenarioId);
            if (component != null && HistoricalWorldFeatureOverrideFileStore.TryLoad(component, out string loadError)
                == false)
                Log.Warning("[HR-016] Manual World Feature overrides were not loaded: " + loadError);

            IReadOnlyList<WorldFeature> features = WorldFeatureAdapter.GetAllFeatures(world)
                .Where(feature => ReferenceEquals(feature.layer, world.grid.Surface)).ToList();
            List<FeatureTarget> targets = new List<FeatureTarget>();
            foreach (HistoricalWorldFeatureOverrideDef def in DefDatabase<HistoricalWorldFeatureOverrideDef>.AllDefsListForReading
                .Where(item => item != null && item.scenarioId == scenarioId)
                .OrderByDescending(item => item.priority).ThenBy(item => item.defName, StringComparer.Ordinal))
            {
                if (def.minimumOverlapRatio > 0f)
                {
                    Log.Warning("[HR-016] Skipping " + def.defName
                        + ": overlap-based matching requires a configured target polygon; this point-only entry has none.");
                    continue;
                }
                targets.Add(new FeatureTarget(def.defName, def.featureType, def.historicalName,
                    new HistoricalGeoCoordinate(def.targetLatitude, def.targetLongitude),
                    def.searchRadiusKm, def.priority, false));
            }
            foreach (HistoricalWorldFeatureNameRecord manual in component?.ManualOverrides
                ?? Enumerable.Empty<HistoricalWorldFeatureNameRecord>())
            {
                if (manual == null || string.IsNullOrWhiteSpace(manual.historicalName)
                    || string.IsNullOrWhiteSpace(manual.featureType)) continue;
                try
                {
                    targets.Add(new FeatureTarget("manual:" + manual.featureUniqueId, manual.featureType,
                        manual.historicalName, new HistoricalGeoCoordinate(manual.targetLatitude, manual.targetLongitude),
                        manual.searchRadiusKm, int.MaxValue, true, manual.featureUniqueId));
                }
                catch (ArgumentOutOfRangeException exception)
                { Log.Warning("[HR-016] Ignored a manual override with invalid coordinates: " + exception.Message); }
            }

            Dictionary<WorldFeature, List<FeatureMatch>> claims = new Dictionary<WorldFeature, List<FeatureMatch>>();
            foreach (FeatureTarget target in targets)
            {
                List<FeatureMatch> candidates = new List<FeatureMatch>();
                int typeCandidateCount = 0;
                double nearestKm = double.PositiveInfinity;
                foreach (WorldFeature feature in features)
                {
                    if (!FeatureTypeMatches(feature, target.featureType)) continue;
                    typeCandidateCount++;
                    if (!TryGetHistoricalRepresentative(feature, transformer, out HistoricalGeoCoordinate representative)) continue;
                    double distance = HistoricalGeographicDistanceUtility.GetGreatCircleDistanceKm(target.coordinate, representative);
                    nearestKm = Math.Min(nearestKm, distance);
                    if (distance > target.radiusKm) continue;
                    double score = Math.Max(0d, 1d - distance / target.radiusKm);
                    if (target.manual && target.featureUniqueIdHint >= 0 && feature.uniqueID == target.featureUniqueIdHint)
                        score = Math.Max(score, AutoApplyScore);
                    candidates.Add(new FeatureMatch(feature, distance, score, representative));
                }
                FeatureMatch best = candidates.OrderByDescending(item => item.score).ThenBy(item => item.distanceKm).FirstOrDefault();
                if (best == null)
                {
                    Log.Warning("[HR-016] No " + target.featureType + " feature found near " + target.name
                        + " (" + target.coordinate.Longitude.ToString("0.####") + ","
                        + target.coordinate.Latitude.ToString("0.####") + "; radius " + target.radiusKm.ToString("0.#")
                        + " km; native type candidates=" + typeCandidateCount + "; nearest="
                        + (double.IsInfinity(nearestKm) ? "n/a" : nearestKm.ToString("0.#") + " km") + ").");
                    continue;
                }
                if (best.score < AutoApplyScore)
                {
                    if (best.score >= ReviewScore)
                        Log.Warning("[HR-016] Review required for " + target.name + ": candidate '"
                            + best.feature.name + "' is " + best.distanceKm.ToString("0.#")
                            + " km away (score " + best.score.ToString("0.00") + "); left unchanged.");
                    else Log.Warning("[HR-016] Best candidate for " + target.name + " scored only "
                        + best.score.ToString("0.00") + "; left unchanged.");
                    continue;
                }
                if (!claims.TryGetValue(best.feature, out List<FeatureMatch> claimList))
                    claims[best.feature] = claimList = new List<FeatureMatch>();
                claimList.Add(new FeatureMatch(target, best));
            }

            int renamed = 0;
            foreach (KeyValuePair<WorldFeature, List<FeatureMatch>> pair in claims)
            {
                FeatureMatch[] manualMatches = pair.Value.Where(item => item.target.manual)
                    .OrderBy(item => item.distanceKm).ToArray();
                FeatureMatch[] matches = (manualMatches.Length == 1 ? manualMatches : pair.Value
                    .OrderByDescending(item => item.target.priority).ThenBy(item => item.distanceKm).ToArray());
                if (manualMatches.Length > 1) matches = manualMatches;
                if (matches.Length != 1)
                {
                    Log.Error("[HR-016] Conflict: native feature " + pair.Key.uniqueID + " ('" + pair.Key.name
                        + "') matched multiple historical names: " + string.Join(", ", matches.Select(item => item.target.name))
                        + ". No name was changed.");
                    continue;
                }
                FeatureMatch match = matches[0];
                if (pair.Key.name == match.target.name) continue;
                string original = pair.Key.name;
                component?.SetOriginalName(pair.Key.uniqueID, original);
                if (!WorldFeatureAdapter.TrySetName(pair.Key, match.target.name)) continue;
                if (match.target.manual && component != null)
                {
                    HistoricalWorldFeatureNameRecord manualRecord = component.GetRecord(pair.Key.uniqueID)?.Clone()
                        ?? new HistoricalWorldFeatureNameRecord();
                    manualRecord.featureUniqueId = pair.Key.uniqueID;
                    manualRecord.featureType = pair.Key.def.defName;
                    manualRecord.historicalName = match.target.name;
                    manualRecord.originalName = original;
                    manualRecord.targetLongitude = match.target.coordinate.Longitude;
                    manualRecord.targetLatitude = match.target.coordinate.Latitude;
                    manualRecord.searchRadiusKm = match.target.radiusKm;
                    component.SetRecord(manualRecord);
                }
                renamed++;
                Log.Message("[HR-016] Renamed native " + pair.Key.def.defName + " feature '" + original + "' to '"
                    + match.target.name + "' at " + match.distanceKm.ToString("0.#") + " km (score "
                    + match.score.ToString("0.00") + "). Feature ID=" + pair.Key.uniqueID
                    + ", covered tiles=" + WorldFeatureAdapter.GetFeatureTiles(world, pair.Key).Count + ".");
            }
            if (renamed > 0)
            {
                pendingLabelRefreshWorld = world;
                if (ReferenceEquals(Find.World, world)) RefreshPendingLabels(world);
            }
            return renamed;
        }

        public static bool TryGetFeatureForTile(RimWorldWorld world, int tileId, out WorldFeature feature)
        {
            feature = null;
            if (world?.grid?.Surface?.Tiles == null || tileId < 0 || tileId >= world.grid.Surface.TilesCount) return false;
            feature = world.grid.Surface.Tiles[tileId]?.feature;
            return feature != null;
        }

        public static bool TryGetHistoricalRepresentative(WorldFeature feature,
            HistoricalMapScaleTransformer transformer, out HistoricalGeoCoordinate coordinate)
        {
            coordinate = default(HistoricalGeoCoordinate);
            if (feature == null || transformer == null) return false;
            Vector3 center = feature.drawCenter.normalized;
            double longitude = Math.Atan2(center.x, -center.z) * 180d / Math.PI;
            double latitude = Math.Asin(Math.Max(-1d, Math.Min(1d, center.y))) * 180d / Math.PI;
            try
            {
                coordinate = transformer.ToHistoricalCoordinate(new HistoricalGameGeoCoordinate(latitude, longitude));
                return true;
            }
            catch { return false; }
        }

        public static bool FeatureTypeMatches(WorldFeature feature, string requestedType)
        {
            if (feature?.def == null || string.IsNullOrWhiteSpace(requestedType)) return false;
            if (string.Equals(feature.def.defName, requestedType, StringComparison.OrdinalIgnoreCase)) return true;
            // A semantic alias is accepted only when the actual native worker confirms the intended class.
            return requestedType == "MountainRange"
                && feature.def.workerClass != null
                && string.Equals(feature.def.workerClass.Name, "FeatureWorker_MountainRange", StringComparison.Ordinal);
        }

        public static void RefreshPendingLabels(RimWorldWorld world)
        {
            if (world == null || !ReferenceEquals(world, pendingLabelRefreshWorld)) return;
            try
            {
                WorldFeatureAdapter.RefreshFeatureLabels(world);
                pendingLabelRefreshWorld = null;
            }
            catch (Exception exception)
            { Log.Warning("[HR-016] Native feature names changed, but label cache refresh failed: " + exception.Message); }
        }

        private sealed class FeatureTarget
        {
            public readonly string id, featureType, name;
            public readonly HistoricalGeoCoordinate coordinate;
            public readonly double radiusKm;
            public readonly int priority;
            public readonly bool manual;
            public readonly int featureUniqueIdHint;
            public FeatureTarget(string id, string featureType, string name, HistoricalGeoCoordinate coordinate,
                double radiusKm, int priority, bool manual, int featureUniqueIdHint = -1)
            { this.id = id; this.featureType = featureType; this.name = name; this.coordinate = coordinate;
              this.radiusKm = radiusKm; this.priority = priority; this.manual = manual; this.featureUniqueIdHint = featureUniqueIdHint; }
        }

        private sealed class FeatureMatch
        {
            public readonly WorldFeature feature;
            public readonly FeatureTarget target;
            public readonly double distanceKm, score;
            public readonly HistoricalGeoCoordinate representative;
            public FeatureMatch(WorldFeature feature, double distanceKm, double score, HistoricalGeoCoordinate representative)
            { this.feature = feature; this.distanceKm = distanceKm; this.score = score; this.representative = representative; }
            public FeatureMatch(FeatureTarget target, FeatureMatch match)
            { this.target = target; feature = match.feature; distanceKm = match.distanceKm; score = match.score; representative = match.representative; }
        }
    }
}
