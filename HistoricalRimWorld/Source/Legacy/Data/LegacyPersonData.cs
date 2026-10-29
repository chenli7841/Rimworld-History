using System.Collections.Generic;
using System.Runtime.Serialization;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Lifespan;

namespace HistoricalRimWorld.Legacy
{
    [DataContract]
    public sealed class LegacyPersonData
    {
        [DataMember(Order = 1)] public string PersonId;
        [DataMember(Order = 2)] public string FamilyId;
        [DataMember(Order = 3)] public string DisplayName;
        [DataMember(Order = 4)] public HistoricalPersonOrigin Origin;
        [DataMember(Order = 5)] public int BirthAstronomicalYear;
        [DataMember(Order = 6)] public int BirthMonth;
        [DataMember(Order = 7)] public int BirthDay;
        [DataMember(Order = 8)] public bool IsAliveAtExport;
        [DataMember(Order = 9)] public int? DeathAstronomicalYear;
        [DataMember(Order = 10)] public int? DeathMonth;
        [DataMember(Order = 11)] public int? DeathDay;
        [DataMember(Order = 12)] public string BirthRegionId;
        [DataMember(Order = 13)] public string LastKnownRegionId;
        [DataMember(Order = 14)] public string BirthCultureId;
        [DataMember(Order = 15)] public string PrimaryCultureId;
        [DataMember(Order = 16)] public string LastKnownAllegiancePolityId;
        [DataMember(Order = 17)] public string BirthAllegiancePolityId;
        [DataMember(Order = 18)] public int? AssignedLifespanYears;
        [DataMember(Order = 19)] public int? NaturalDeathAstronomicalYear;
        [DataMember(Order = 20)] public int? NaturalDeathMonth;
        [DataMember(Order = 21)] public int? NaturalDeathDay;
        [DataMember(Order = 22)] public string Gender;
        [DataMember(Order = 23)] public List<LegacyCulturalFamiliarityData> Familiarities = new List<LegacyCulturalFamiliarityData>();
        [DataMember(Order = 24)] public HistoricalDeathCause DeathCause;
    }

    [DataContract]
    public sealed class LegacyCulturalFamiliarityData
    {
        [DataMember(Order = 1)] public string CultureId;
        [DataMember(Order = 2)] public float Familiarity;
    }
}
