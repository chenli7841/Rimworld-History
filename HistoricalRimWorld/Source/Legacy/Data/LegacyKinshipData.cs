using System.Runtime.Serialization;
using HistoricalRimWorld.Genealogy;

namespace HistoricalRimWorld.Legacy
{
    [DataContract]
    public sealed class LegacyKinshipData
    {
        [DataMember(Order = 1)] public string PersonIdA;
        [DataMember(Order = 2)] public string PersonIdB;
        [DataMember(Order = 3)] public HistoricalKinshipType Type;
        [DataMember(Order = 4)] public int? StartAstronomicalYear;
        [DataMember(Order = 5)] public int? StartMonth;
        [DataMember(Order = 6)] public int? StartDay;
        [DataMember(Order = 7)] public int? EndAstronomicalYear;
        [DataMember(Order = 8)] public int? EndMonth;
        [DataMember(Order = 9)] public int? EndDay;
    }
}
