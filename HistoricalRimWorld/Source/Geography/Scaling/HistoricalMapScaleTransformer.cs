using System;
using HistoricalRimWorld.Geography;

namespace HistoricalRimWorld.Geography.Scaling
{
    public sealed class HistoricalMapScaleTransformer
    {
        public const int CurrentProjectionVersion = 1;
        private readonly IHistoricalGeoProjection projection;
        public double ScaleFactor { get; }

        public HistoricalMapScaleTransformer(HistoricalMapScaleDef definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            foreach (string error in definition.ValidateSettings())
                throw new ArgumentException("Invalid map scale definition: " + error, nameof(definition));
            ScaleFactor = definition.scaleFactor;
            projection = new AzimuthalEquidistantProjection(definition.centerLatitude, definition.centerLongitude);
        }

        public HistoricalProjectedCoordinate Project(HistoricalGeoCoordinate coordinate) => projection.Project(coordinate);

        public HistoricalGameGeoCoordinate ToGameCoordinate(HistoricalGeoCoordinate historicalCoordinate)
        {
            HistoricalProjectedCoordinate projected = projection.Project(historicalCoordinate);
            return UnprojectScaled(new HistoricalProjectedCoordinate(
                projected.X * ScaleFactor, projected.Y * ScaleFactor));
        }

        public HistoricalGeoCoordinate ToHistoricalCoordinate(HistoricalGameGeoCoordinate gameCoordinate)
        {
            HistoricalGeoCoordinate gameAsGeo = new HistoricalGeoCoordinate(gameCoordinate.Latitude, gameCoordinate.Longitude);
            HistoricalProjectedCoordinate projected = projection.Project(gameAsGeo);
            return projection.Unproject(new HistoricalProjectedCoordinate(
                projected.X / ScaleFactor, projected.Y / ScaleFactor));
        }

        private HistoricalGameGeoCoordinate UnprojectScaled(HistoricalProjectedCoordinate coordinate)
        {
            HistoricalGeoCoordinate result = projection.Unproject(coordinate);
            return new HistoricalGameGeoCoordinate(result.Latitude, result.Longitude);
        }
    }
}
