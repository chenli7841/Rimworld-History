using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalLocalCommunityState : IExposable
    {
        private string communityId;
        private string regionId;
        private string domainId;
        private string nominalPolityId;
        private string administrativePolityId;
        private string effectiveControllerId;
        private string allegiancePolityId;
        private HistoricalCommunityPosture posture;

        public string CommunityId => communityId;
        public string RegionId => regionId;
        public string DomainId => domainId;
        public string NominalPolityId => nominalPolityId;
        public string AdministrativePolityId => administrativePolityId;
        public string EffectiveControllerId => effectiveControllerId;
        public string AllegiancePolityId => allegiancePolityId;
        public HistoricalCommunityPosture Posture => posture;

        public void ExposeData()
        {
            Scribe_Values.Look(ref communityId, "communityId");
            Scribe_Values.Look(ref regionId, "regionId");
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref nominalPolityId, "nominalPolityId");
            Scribe_Values.Look(ref administrativePolityId, "administrativePolityId");
            Scribe_Values.Look(ref effectiveControllerId, "effectiveControllerId");
            Scribe_Values.Look(ref allegiancePolityId, "allegiancePolityId");
            Scribe_Values.Look(ref posture, "posture", HistoricalCommunityPosture.Peaceful);
        }

        internal void Initialize(
            string id,
            string region,
            string domain,
            string nominalPolity,
            string administrativePolity,
            string controller,
            string allegiance,
            HistoricalCommunityPosture initialPosture)
        {
            communityId = id;
            regionId = region;
            domainId = domain;
            nominalPolityId = nominalPolity;
            administrativePolityId = administrativePolity;
            effectiveControllerId = controller;
            allegiancePolityId = allegiance;
            posture = initialPosture;
        }

        internal void SetAllegiance(string polityId)
        {
            allegiancePolityId = polityId;
        }

        internal void SetController(string polityId)
        {
            effectiveControllerId = polityId;
        }

        internal void SetPosture(HistoricalCommunityPosture value)
        {
            posture = value;
        }

        internal void SubmitTo(string polityId)
        {
            effectiveControllerId = polityId;
            allegiancePolityId = polityId;
            posture = HistoricalCommunityPosture.Submitting;
        }

        internal void SetLocation(string newRegionId, string newDomainId)
        {
            regionId = newRegionId;
            domainId = newDomainId;
        }
    }
}
