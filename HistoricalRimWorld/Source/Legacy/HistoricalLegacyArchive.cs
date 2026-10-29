using System.Collections.Generic;
using System.Runtime.Serialization;
using HistoricalRimWorld.Calendar;

namespace HistoricalRimWorld.Legacy
{
    [DataContract]
    public sealed class HistoricalLegacyArchive
    {
        [DataMember(Order = 1)] public int SchemaVersion;
        [DataMember(Order = 2)] public string ArchiveId;
        [DataMember(Order = 3)] public string SourceScenarioId;
        [DataMember(Order = 4)] public int SourceEndAstronomicalYear;
        [DataMember(Order = 5)] public int SourceEndMonth;
        [DataMember(Order = 6)] public int SourceEndDay;
        [DataMember(Order = 7)] public string SourceSaveIdentifier;
        [DataMember(Order = 8)] public string ArchiveName;
        [DataMember(Order = 9)] public List<LegacyPersonData> Persons = new List<LegacyPersonData>();
        [DataMember(Order = 10)] public List<LegacyFamilyData> Families = new List<LegacyFamilyData>();
        [DataMember(Order = 11)] public List<LegacyKinshipData> Kinships = new List<LegacyKinshipData>();
        [DataMember(Order = 12)] public List<LegacyRelicData> Relics = new List<LegacyRelicData>();
        [DataMember(Order = 13)] public List<LegacyRelicEventData> RelicEvents = new List<LegacyRelicEventData>();
        [DataMember(Order = 14)] public List<LegacyChronicleData> Chronicle = new List<LegacyChronicleData>();
        [DataMember(Order = 15)] public List<LegacyFamilyMigrationData> Migrations = new List<LegacyFamilyMigrationData>();
        [DataMember(Order = 16)] public List<LegacyImportMarkerData> ImportedMarkers = new List<LegacyImportMarkerData>();
    }

    [DataContract]
    public sealed class LegacyImportMarkerData
    {
        [DataMember(Order = 1)] public string ArchiveId;
        [DataMember(Order = 2)] public string TargetScenarioId;
        [DataMember(Order = 3)] public string ImportId;
        [DataMember(Order = 4)] public int SimulationSeed;
    }
}
