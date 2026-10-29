using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HistoricalRimWorld.Legacy
{
    [DataContract]
    public sealed class LegacyFamilyData
    {
        [DataMember(Order = 1)] public string FamilyId;
        [DataMember(Order = 2)] public string FamilyName;
        [DataMember(Order = 3)] public string FounderPersonId;
        [DataMember(Order = 4)] public string OriginRegionId;
        [DataMember(Order = 5)] public string OriginPolityId;
        [DataMember(Order = 6)] public string PrimaryCultureId;
        [DataMember(Order = 7)] public List<string> PreservedTraditionIds = new List<string>();
        [DataMember(Order = 8)] public string ParentFamilyId;
    }
}
