using System;

namespace HistoricalRimWorld.Characters
{
    public static class HistoricalPersonIdGenerator
    {
        public static string Generate()
        {
            return "HR_Person_" + Guid.NewGuid().ToString("D");
        }
    }
}
