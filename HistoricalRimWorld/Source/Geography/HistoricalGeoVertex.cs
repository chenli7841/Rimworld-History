using System;

namespace HistoricalRimWorld.Geography
{
    // Mutable XML data shape; converted to the validated immutable coordinate type at use sites.
    public sealed class HistoricalGeoVertex
    {
        public double latitude;
        public double longitude;

        public HistoricalGeoCoordinate ToCoordinate() => new HistoricalGeoCoordinate(latitude, longitude);
    }
}
