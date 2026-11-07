using System;
using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.World
{
    /// <summary>Static geographic data for a natural mountain passage.</summary>
    public sealed class HistoricalMountainPassDef : Def
    {
        public HistoricalScenarioDef scenario;
        public float latitude;
        public float longitude;
        public string historicalRegionId;
        public string connectedRouteId;
        public string relatedFortifiedPassDefName;
        public string historicalDescription;
        public string sourceNotes;
        public string texturePath = "World/HR/NaturalMountainPass";
        public int labelPriority = 50;
        public float minimumZoom;
        public float maximumZoom = 1f;
        public bool showOnWorldMap = true;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (latitude < -90f || latitude > 90f) yield return "latitude must be within -90..90 degrees.";
            if (longitude < -180f || longitude > 180f) yield return "longitude must be within -180..180 degrees.";
            if (string.IsNullOrWhiteSpace(texturePath)) yield return "texturePath is required.";
            if (minimumZoom < 0f || maximumZoom > 1f || minimumZoom > maximumZoom)
                yield return "minimumZoom and maximumZoom must be an ordered range within 0..1.";
            if (string.IsNullOrWhiteSpace(sourceNotes)) yield return "sourceNotes should explain the geographic identification and coordinate precision.";
            if (!string.IsNullOrWhiteSpace(relatedFortifiedPassDefName)
                && DefDatabase<HistoricalFortifiedPassDef>.GetNamedSilentFail(relatedFortifiedPassDefName) == null)
                yield return "relatedFortifiedPassDefName does not refer to a HistoricalFortifiedPassDef.";
        }
    }

    /// <summary>Static historical data for a human-built or human-controlled fortified pass.</summary>
    public sealed class HistoricalFortifiedPassDef : Def
    {
        public HistoricalScenarioDef scenario;
        public float latitude;
        public float longitude;
        public string relatedMountainPassDefName;
        // Years use HR-002 astronomical numbering. int.MinValue means unknown/not supplied.
        public int establishedAstronomicalYear = int.MinValue;
        public int abandonedAstronomicalYear = int.MinValue;
        public string texturePath = "World/HR/MountainPass";
        public int labelPriority = 70;
        public float minimumZoom;
        public float maximumZoom = 1f;
        public bool showOnWorldMap = true;
        public string sourceNotes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (scenario == null) yield return "scenario is required.";
            if (latitude < -90f || latitude > 90f) yield return "latitude must be within -90..90 degrees.";
            if (longitude < -180f || longitude > 180f) yield return "longitude must be within -180..180 degrees.";
            if (string.IsNullOrWhiteSpace(texturePath)) yield return "texturePath is required.";
            if (minimumZoom < 0f || maximumZoom > 1f || minimumZoom > maximumZoom)
                yield return "minimumZoom and maximumZoom must be an ordered range within 0..1.";
            if (showOnWorldMap && establishedAstronomicalYear == int.MinValue)
                yield return "A visible fortified pass requires a verified establishedAstronomicalYear; otherwise set showOnWorldMap to false.";
            if (abandonedAstronomicalYear != int.MinValue
                && establishedAstronomicalYear != int.MinValue
                && abandonedAstronomicalYear < establishedAstronomicalYear)
                yield return "abandonedAstronomicalYear must be later than establishedAstronomicalYear in astronomical numbering.";
            if (!string.IsNullOrWhiteSpace(relatedMountainPassDefName)
                && DefDatabase<HistoricalMountainPassDef>.GetNamedSilentFail(relatedMountainPassDefName) == null)
                yield return "relatedMountainPassDefName does not refer to a HistoricalMountainPassDef.";
            if (string.IsNullOrWhiteSpace(sourceNotes)) yield return "sourceNotes should explain the evidence or chronology uncertainty.";
        }
    }
}
