using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Genealogy
{
    public sealed class HistoricalFamilyRecord : IExposable
    {
        private string familyId;
        private string familyName;
        private string founderPersonId;
        private string originRegionId;
        private string originPolityId;
        private string parentFamilyId;
        private List<string> memberPersonIds = new List<string>();

        public string FamilyId => familyId;
        public string FamilyName => familyName;
        public string FounderPersonId => founderPersonId;
        public string OriginRegionId => originRegionId;
        public string OriginPolityId => originPolityId;
        public string ParentFamilyId => parentFamilyId;
        public IReadOnlyList<string> MemberPersonIds => memberPersonIds ?? (IReadOnlyList<string>)new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref familyId, "familyId");
            Scribe_Values.Look(ref familyName, "familyName");
            Scribe_Values.Look(ref founderPersonId, "founderPersonId");
            Scribe_Values.Look(ref originRegionId, "originRegionId");
            Scribe_Values.Look(ref originPolityId, "originPolityId");
            Scribe_Values.Look(ref parentFamilyId, "parentFamilyId");
            Scribe_Collections.Look(ref memberPersonIds, "memberPersonIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) memberPersonIds = memberPersonIds ?? new List<string>();
        }

        internal void Initialize(string id, string name, string founder, string region, string polity)
        {
            familyId = id;
            familyName = name;
            founderPersonId = founder;
            originRegionId = region;
            originPolityId = polity;
            memberPersonIds = new List<string>();
        }

        internal void AddMember(string personId)
        {
            if (!string.IsNullOrWhiteSpace(personId) && !memberPersonIds.Contains(personId)) memberPersonIds.Add(personId);
        }

        internal void SetParentFamilyId(string value) => parentFamilyId = value;

        internal void RemoveMember(string personId) => memberPersonIds?.Remove(personId);
    }
}
