using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalRegionPoliticalState : IExposable
    {
        private string regionId;
        private string nominalOwnerId;
        private string effectiveControllerId;
        private string militaryOccupierId;
        private bool isContested;

        public string RegionId => regionId;
        public string NominalOwnerId => nominalOwnerId;
        public string EffectiveControllerId => effectiveControllerId;
        public string MilitaryOccupierId => militaryOccupierId;
        public bool IsContested => isContested;

        public void ExposeData()
        {
            Scribe_Values.Look(ref regionId, "regionId");
            Scribe_Values.Look(ref nominalOwnerId, "nominalOwnerId");
            Scribe_Values.Look(ref effectiveControllerId, "effectiveControllerId");
            Scribe_Values.Look(ref militaryOccupierId, "militaryOccupierId");
            Scribe_Values.Look(ref isContested, "isContested", false);
        }

        internal void Initialize(string id, string nominalOwner, string controller, string occupier, bool contested)
        {
            regionId = id;
            nominalOwnerId = nominalOwner;
            effectiveControllerId = controller;
            militaryOccupierId = occupier;
            isContested = contested;
        }

        internal void SetEffectiveController(string controllerId, string occupierId, bool contested)
        {
            effectiveControllerId = controllerId;
            militaryOccupierId = occupierId;
            isContested = contested;
        }

        internal void SetNominalOwner(string ownerId) => nominalOwnerId = ownerId;
    }
}
