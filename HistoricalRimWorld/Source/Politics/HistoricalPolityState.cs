using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalPolityState : IExposable
    {
        private string polityId;
        private HistoricalPolityStatus status;
        private string nominalOverlordId;
        private string effectiveOverlordId;
        private List<string> successorPolityIds = new List<string>();
        private int politicalRevision;

        public string PolityId => polityId;
        public HistoricalPolityStatus Status => status;
        public string NominalOverlordId => nominalOverlordId;
        public string EffectiveOverlordId => effectiveOverlordId;
        public IEnumerable<string> SuccessorPolityIds => successorPolityIds;
        public int PoliticalRevision => politicalRevision;

        public void ExposeData()
        {
            Scribe_Values.Look(ref polityId, "polityId");
            Scribe_Values.Look(ref status, "status", HistoricalPolityStatus.Active);
            Scribe_Values.Look(ref nominalOverlordId, "nominalOverlordId");
            Scribe_Values.Look(ref effectiveOverlordId, "effectiveOverlordId");
            Scribe_Collections.Look(ref successorPolityIds, "successorPolityIds", LookMode.Value);
            Scribe_Values.Look(ref politicalRevision, "politicalRevision", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && successorPolityIds == null)
            {
                successorPolityIds = new List<string>();
            }
        }

        internal void Initialize(
            string id,
            HistoricalPolityStatus initialStatus,
            string nominalOverlord,
            string effectiveOverlord,
            IEnumerable<string> successors,
            int revision)
        {
            polityId = id;
            status = initialStatus;
            nominalOverlordId = nominalOverlord;
            effectiveOverlordId = effectiveOverlord;
            successorPolityIds = successors == null ? new List<string>() : new List<string>(successors);
            politicalRevision = revision;
        }

        internal void SetStatus(HistoricalPolityStatus value)
        {
            status = value;
            politicalRevision++;
        }

        internal void SetNominalOverlord(string value)
        {
            nominalOverlordId = value;
            politicalRevision++;
        }

        internal void SetEffectiveOverlord(string value)
        {
            effectiveOverlordId = value;
            politicalRevision++;
        }

        internal void SetSuccessors(IEnumerable<string> successorIds)
        {
            successorPolityIds = successorIds == null ? new List<string>() : new List<string>(successorIds);
            politicalRevision++;
        }
    }
}
