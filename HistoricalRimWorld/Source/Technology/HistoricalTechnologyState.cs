using Verse;

namespace HistoricalRimWorld.Technology
{
    public sealed class HistoricalTechnologyState : IExposable
    {
        public string TechnologyId;
        public HistoricalTechnologyAvailability Availability;
        public HistoricalTechnologyAdoption Adoption;
        public float StudyProgress;
        public int AdoptionGameTick = -1;
        public int AcquisitionGameTick = -1;
        public HistoricalTechnologyAcquisitionSource LastSource;
        public int AdoptionAstronomicalYear;

        public void ExposeData()
        {
            Scribe_Values.Look(ref TechnologyId, "technologyId");
            Scribe_Values.Look(ref Availability, "availability", HistoricalTechnologyAvailability.ExistsElsewhere);
            Scribe_Values.Look(ref Adoption, "adoption", HistoricalTechnologyAdoption.Unknown);
            Scribe_Values.Look(ref StudyProgress, "studyProgress", 0f);
            Scribe_Values.Look(ref AdoptionGameTick, "adoptionGameTick", -1);
            Scribe_Values.Look(ref AcquisitionGameTick, "acquisitionGameTick", -1);
            Scribe_Values.Look(ref LastSource, "lastSource", HistoricalTechnologyAcquisitionSource.Development);
            Scribe_Values.Look(ref AdoptionAstronomicalYear, "adoptionAstronomicalYear");
        }
    }
}
