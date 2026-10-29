using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HistoricalRimWorld.Legacy
{
    public sealed class HistoricalLegacyGameComponent : GameComponent
    {
        private string sourceSaveIdentifier;
        private List<HistoricalLegacyImportRecord> imports = new List<HistoricalLegacyImportRecord>();
        private List<HistoricalLegacyMigrationRecord> migrations = new List<HistoricalLegacyMigrationRecord>();
        public HistoricalLegacyGameComponent(Game game) { sourceSaveIdentifier = Guid.NewGuid().ToString("D"); }
        public string SourceSaveIdentifier => sourceSaveIdentifier;
        public IEnumerable<HistoricalLegacyImportRecord> Imports => imports ?? Enumerable.Empty<HistoricalLegacyImportRecord>();
        public IEnumerable<HistoricalLegacyMigrationRecord> Migrations => migrations ?? Enumerable.Empty<HistoricalLegacyMigrationRecord>();
        public bool HasImport(string archiveId, string targetScenarioId) => Imports.Any(item => item.ArchiveId == archiveId && item.TargetScenarioId == targetScenarioId);
        public bool RecordImport(string archiveId, string targetScenarioId, string importId, int seed)
        {
            if (HasImport(archiveId, targetScenarioId)) return false;
            (imports ??= new List<HistoricalLegacyImportRecord>()).Add(new HistoricalLegacyImportRecord { ArchiveId = archiveId, TargetScenarioId = targetScenarioId, ImportId = importId, SimulationSeed = seed });
            return true;
        }
        public void RecordMigration(string familyId, string from, string to, int year, string reason, bool simulated)
        {
            (migrations ??= new List<HistoricalLegacyMigrationRecord>()).Add(new HistoricalLegacyMigrationRecord { FamilyId = familyId, FromRegionId = from, ToRegionId = to, AstronomicalYear = year, Reason = reason, IsSimulated = simulated });
        }
        public void RollbackImport(string importId, bool removeMarker = true)
        {
            if (removeMarker) imports.RemoveAll(item => item != null && item.ImportId == importId);
            migrations.RemoveAll(item => item != null && item.Reason != null && item.Reason.StartsWith("Import " + importId + ":", StringComparison.Ordinal));
        }
        public override void ExposeData()
        {
            base.ExposeData(); Scribe_Values.Look(ref sourceSaveIdentifier, "historicalLegacySourceSaveIdentifier");
            Scribe_Collections.Look(ref imports, "historicalLegacyImports", LookMode.Deep);
            Scribe_Collections.Look(ref migrations, "historicalLegacyMigrations", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (string.IsNullOrWhiteSpace(sourceSaveIdentifier)) sourceSaveIdentifier = Guid.NewGuid().ToString("D");
                imports = (imports ?? new List<HistoricalLegacyImportRecord>()).Where(item => item != null).GroupBy(item => item.ArchiveId + "|" + item.TargetScenarioId).Select(group => group.First()).ToList();
                migrations = (migrations ?? new List<HistoricalLegacyMigrationRecord>()).Where(item => item != null).ToList();
            }
        }
    }

    public sealed class HistoricalLegacyImportRecord : IExposable
    {
        public string ArchiveId, TargetScenarioId, ImportId;
        public int SimulationSeed;
        public void ExposeData()
        { Scribe_Values.Look(ref ArchiveId, "archiveId"); Scribe_Values.Look(ref TargetScenarioId, "targetScenarioId"); Scribe_Values.Look(ref ImportId, "importId"); Scribe_Values.Look(ref SimulationSeed, "simulationSeed"); }
    }

    public sealed class HistoricalLegacyMigrationRecord : IExposable
    {
        public string FamilyId, FromRegionId, ToRegionId, Reason;
        public int AstronomicalYear;
        public bool IsSimulated;
        public void ExposeData()
        {
            Scribe_Values.Look(ref FamilyId, "familyId"); Scribe_Values.Look(ref FromRegionId, "fromRegionId");
            Scribe_Values.Look(ref ToRegionId, "toRegionId"); Scribe_Values.Look(ref Reason, "reason");
            Scribe_Values.Look(ref AstronomicalYear, "astronomicalYear"); Scribe_Values.Look(ref IsSimulated, "isSimulated");
        }
    }
}
