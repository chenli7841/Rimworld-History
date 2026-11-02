using System;
using System.Collections.Generic;
using System.IO;
using Verse;

namespace HistoricalRimWorld.Geography.Terrain
{
    /// <summary>Provenance and sampling parameters for an offline elevation cache.</summary>
    public sealed class HistoricalTerrainDatasetDef : Def
    {
        public string cachePath;
        public string sourceName;
        public string sourceVersion;
        public string licenseNotes;
        public bool required;
        public int cacheVersion = 1;
        public int referenceYearBCE = 770;
        public float flatSlopeMaxDegrees = 3f;
        public float smallHillSlopeMaxDegrees = 8f;
        public float largeHillSlopeMaxDegrees = 15f;
        public float mountainReliefMinMeters = 100f;
        public float worldElevationBase = 0.05f;
        public float worldElevationRange = 0.30f;
        public int footprintSampleCount = 25;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(cachePath)) yield return "cachePath is required.";
            if (string.IsNullOrWhiteSpace(sourceName)) yield return "sourceName is required.";
            if (string.IsNullOrWhiteSpace(sourceVersion)) yield return "sourceVersion is required.";
            if (cacheVersion <= 0) yield return "cacheVersion must be positive.";
            if (flatSlopeMaxDegrees < 0f || smallHillSlopeMaxDegrees <= flatSlopeMaxDegrees
                || largeHillSlopeMaxDegrees <= smallHillSlopeMaxDegrees || largeHillSlopeMaxDegrees > 90f)
                yield return "Slope thresholds must be strictly increasing and no greater than 90 degrees.";
            if (mountainReliefMinMeters < 0f) yield return "mountainReliefMinMeters cannot be negative.";
            if (worldElevationBase <= 0f || worldElevationRange < 0f
                || worldElevationBase + worldElevationRange >= 1f)
                yield return "Normalized game elevation must remain above water and below 1.";
            if (footprintSampleCount < 7 || footprintSampleCount > 257)
                yield return "footprintSampleCount must be in the range 7..257.";
            if (cachePath != null && (Path.IsPathRooted(cachePath) || cachePath.Contains("..")))
                yield return "cachePath must be a safe mod-relative path.";
        }
    }
}
