using System.Runtime.Serialization;

namespace HistoricalRimWorld.Legacy
{
    [DataContract]
    public sealed class LegacyRelicData
    {
        [DataMember(Order = 1)] public string RelicId;
        [DataMember(Order = 2)] public string RelicDefName;
        [DataMember(Order = 3)] public string DisplayName;
        [DataMember(Order = 4)] public string OriginCultureId;
        [DataMember(Order = 5)] public string OriginRegionId;
        [DataMember(Order = 6)] public string CreatorPersonId;
        [DataMember(Order = 7)] public string OriginalOwnerPersonId;
        [DataMember(Order = 8)] public string CurrentOwnerPersonId;
        [DataMember(Order = 9)] public string CurrentFamilyId;
        [DataMember(Order = 10)] public int CreationAstronomicalYear;
        [DataMember(Order = 11)] public bool IsLost;
        [DataMember(Order = 12)] public bool IsDestroyed;
    }

    [DataContract]
    public sealed class LegacyRelicEventData
    {
        [DataMember(Order = 1)] public string EventId;
        [DataMember(Order = 2)] public string RelicId;
        [DataMember(Order = 3)] public string EventType;
        [DataMember(Order = 4)] public string FromPersonId;
        [DataMember(Order = 5)] public string ToPersonId;
        [DataMember(Order = 6)] public string RegionId;
        [DataMember(Order = 7)] public int AstronomicalYear;
        [DataMember(Order = 8)] public string SourceNotes;
    }
}
