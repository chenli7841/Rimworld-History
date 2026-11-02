using System;
using Verse;

namespace HistoricalRimWorld.Geography
{
    // Mutable XML data shape; converted to the validated immutable coordinate type at use sites.
    public sealed class HistoricalGeoVertex : IExposable
    {
        public double latitude;
        public double longitude;

        public HistoricalGeoCoordinate ToCoordinate() => new HistoricalGeoCoordinate(latitude, longitude);

        public void ExposeData()
        {
            Scribe_Values.Look(ref latitude, "latitude");
            Scribe_Values.Look(ref longitude, "longitude");
        }
    }
}
