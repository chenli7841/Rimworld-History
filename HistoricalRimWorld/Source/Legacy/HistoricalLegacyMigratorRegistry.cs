using System.Collections.Generic;
using System.Linq;

namespace HistoricalRimWorld.Legacy
{
    public static class HistoricalLegacyMigratorRegistry
    {
        private static readonly List<IHistoricalLegacyMigrator> Migrators = new List<IHistoricalLegacyMigrator>();
        public static void Register(IHistoricalLegacyMigrator migrator)
        {
            if (migrator != null && !Migrators.Any(item => item.FromVersion == migrator.FromVersion)) Migrators.Add(migrator);
        }
        public static bool TryMigrate(HistoricalLegacyArchive input, out HistoricalLegacyArchive output, out string error)
        {
            output = input; error = null;
            if (input == null) { error = "Archive is empty."; return false; }
            if (input.SchemaVersion > HistoricalLegacySchema.CurrentVersion) { error = "Archive uses a newer unsupported schema."; return false; }
            while (output.SchemaVersion < HistoricalLegacySchema.CurrentVersion)
            {
                int currentVersion = output.SchemaVersion;
                IHistoricalLegacyMigrator migrator = Migrators.FirstOrDefault(item => item.FromVersion == currentVersion && item.ToVersion == currentVersion + 1);
                if (migrator == null) { error = "No migration is registered from schema " + output.SchemaVersion + "."; return false; }
                output = migrator.Migrate(output);
                if (output == null || output.SchemaVersion != migrator.ToVersion) { error = "Archive migration returned an invalid version."; return false; }
            }
            return true;
        }
    }
}
