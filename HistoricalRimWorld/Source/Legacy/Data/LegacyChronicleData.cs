using System.Runtime.Serialization;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Scenarios;

namespace HistoricalRimWorld.Legacy
{
    public enum LegacyChronicleOrigin { RecordedGameplay, SimulatedInterlude, AttestedWorldHistory }

    [DataContract]
    public sealed class LegacyChronicleData
    {
        [DataMember(Order = 1)] public string EntryId;
        [DataMember(Order = 2)] public LegacyChronicleOrigin Origin;
        [DataMember(Order = 3)] public HistoricalChronicleEntryType EntryType;
        [DataMember(Order = 4)] public int AstronomicalYear;
        [DataMember(Order = 5)] public int? Month;
        [DataMember(Order = 6)] public int? Day;
        [DataMember(Order = 7)] public HistoricalDatePrecision Precision;
        [DataMember(Order = 8)] public string TitleKey;
        [DataMember(Order = 9)] public string DescriptionKey;
        [DataMember(Order = 10)] public string RegionId;
        [DataMember(Order = 11)] public string PolityId;
        [DataMember(Order = 12)] public HistoricalEvidenceLevel EvidenceLevel;
        [DataMember(Order = 13)] public string SourceNotes;
        [DataMember(Order = 14)] public HistoricalEventCategory Category;
        [DataMember(Order = 15)] public HistoricalEventScope Scope;
        [DataMember(Order = 16)] public bool VisibleToPlayer;
    }
}
