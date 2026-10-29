using System;

namespace HistoricalRimWorld.Genealogy
{
    public static class HistoricalFamilyIdGenerator
    {
        public static string Generate()
        {
            return "HR_Family_" + Guid.NewGuid().ToString("D");
        }
    }
}
