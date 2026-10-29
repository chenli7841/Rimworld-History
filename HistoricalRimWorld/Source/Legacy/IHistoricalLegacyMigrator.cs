namespace HistoricalRimWorld.Legacy
{
    public interface IHistoricalLegacyMigrator
    {
        int FromVersion { get; }
        int ToVersion { get; }
        HistoricalLegacyArchive Migrate(HistoricalLegacyArchive archive);
    }
}
