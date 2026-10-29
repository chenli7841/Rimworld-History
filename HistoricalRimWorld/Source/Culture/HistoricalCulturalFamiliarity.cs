using Verse;

namespace HistoricalRimWorld.Culture
{
    public sealed class HistoricalCulturalFamiliarity : IExposable
    {
        public string CultureId;
        public float Familiarity;
        public HistoricalCulturalTransmissionSource LastSource;
        public int LastChangedGameTick = -1;
        public void ExposeData()
        {
            Scribe_Values.Look(ref CultureId, "cultureId");
            Scribe_Values.Look(ref Familiarity, "familiarity", 0f);
            Scribe_Values.Look(ref LastSource, "lastSource", HistoricalCulturalTransmissionSource.Family);
            Scribe_Values.Look(ref LastChangedGameTick, "lastChangedGameTick", -1);
        }
    }
}
