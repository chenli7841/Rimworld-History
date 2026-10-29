using System.Runtime.Serialization;

namespace HistoricalRimWorld.Legacy
{
    [DataContract]
    public sealed class LegacyFamilyMigrationData
    {
        [DataMember(Order = 1)] public string FamilyId;
        [DataMember(Order = 2)] public string FromRegionId;
        [DataMember(Order = 3)] public string ToRegionId;
        [DataMember(Order = 4)] public int AstronomicalYear;
        [DataMember(Order = 5)] public string Reason;
        [DataMember(Order = 6)] public bool IsSimulated;
    }
}
