using Verse;

namespace HistoricalRimWorld.Characters
{
    public sealed class HistoricalPawnIdentity : IExposable
    {
        private string personId;
        private Pawn pawn;

        public string PersonId => personId;
        public Pawn Pawn => pawn;

        public void ExposeData()
        {
            Scribe_Values.Look(ref personId, "personId");
            Scribe_References.Look(ref pawn, "pawn");
        }

        internal void Initialize(string id, Pawn value)
        {
            personId = id;
            pawn = value;
        }
    }
}
