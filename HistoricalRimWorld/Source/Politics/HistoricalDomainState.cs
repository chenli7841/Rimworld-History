using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalDomainState : IExposable
    {
        private string domainId;
        private string parentPolityId;
        private string nominalOwnerId;
        private string effectiveControllerId;
        private string localLordId;
        private bool isDisputed;

        public string DomainId => domainId;
        public string ParentPolityId => parentPolityId;
        public string NominalOwnerId => nominalOwnerId;
        public string EffectiveControllerId => effectiveControllerId;
        public string LocalLordId => localLordId;
        public bool IsDisputed => isDisputed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_Values.Look(ref parentPolityId, "parentPolityId");
            Scribe_Values.Look(ref nominalOwnerId, "nominalOwnerId");
            Scribe_Values.Look(ref effectiveControllerId, "effectiveControllerId");
            Scribe_Values.Look(ref localLordId, "localLordId");
            Scribe_Values.Look(ref isDisputed, "isDisputed", false);
        }

        internal void Initialize(string id, string parentId, string nominalOwner, string controller, string lordId, bool disputed)
        {
            domainId = id;
            parentPolityId = parentId;
            nominalOwnerId = nominalOwner;
            effectiveControllerId = controller;
            localLordId = lordId;
            isDisputed = disputed;
        }

        internal void SetEffectiveController(string controllerId, bool disputed)
        {
            effectiveControllerId = controllerId;
            isDisputed = disputed;
        }
    }
}
