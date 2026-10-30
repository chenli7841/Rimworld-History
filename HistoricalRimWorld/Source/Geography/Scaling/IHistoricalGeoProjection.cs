using HistoricalRimWorld.Geography;

namespace HistoricalRimWorld.Geography.Scaling
{
    public interface IHistoricalGeoProjection
    {
        HistoricalProjectedCoordinate Project(HistoricalGeoCoordinate coordinate);
        HistoricalGeoCoordinate Unproject(HistoricalProjectedCoordinate coordinate);
    }
}
