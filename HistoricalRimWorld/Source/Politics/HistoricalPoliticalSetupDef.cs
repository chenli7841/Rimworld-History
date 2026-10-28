using System.Collections.Generic;
using HistoricalRimWorld.Scenarios;
using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalPoliticalSetupDef : Def
    {
        public HistoricalScenarioDef scenario;
        public List<HistoricalInitialPolityRecord> polities;
        public List<HistoricalInitialRegionRecord> regions;
        public List<HistoricalInitialDomainRecord> domains;
        public List<HistoricalInitialRelationRecord> relations;
        public List<HistoricalInitialCommunityRecord> communities;
        public string sourceNotes;
        public HistoricalEvidenceLevel evidenceLevel = HistoricalEvidenceLevel.Probable;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            foreach (string error in HistoricalPoliticsValidator.ValidateSetup(this)) yield return error;
        }
    }

    public sealed class HistoricalInitialPolityRecord
    {
        public string polityId;
        public HistoricalPolityStatus status;
        public string nominalOverlordId;
        public string effectiveOverlordId;
        public List<string> successorPolityIds;
        public int politicalRevision;
    }

    public sealed class HistoricalInitialRegionRecord
    {
        public string regionId;
        public string nominalOwnerId;
        public string effectiveControllerId;
        public string militaryOccupierId;
        public bool isContested;
    }

    public sealed class HistoricalInitialDomainRecord
    {
        public string domainId;
        public string parentPolityId;
        public string nominalOwnerId;
        public string effectiveControllerId;
        public string localLordId;
        public bool isDisputed;
    }

    public sealed class HistoricalInitialRelationRecord
    {
        public string sourcePolityId;
        public string targetPolityId;
        public HistoricalPoliticalRelationType relationType;
        public bool isActive = true;
    }

    public sealed class HistoricalInitialCommunityRecord
    {
        public string communityId;
        public string regionId;
        public string domainId;
        public string nominalPolityId;
        public string administrativePolityId;
        public string effectiveControllerId;
        public string allegiancePolityId;
        public HistoricalCommunityPosture posture;
    }
}
