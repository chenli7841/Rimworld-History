using Verse;

namespace HistoricalRimWorld.Politics
{
    public sealed class HistoricalPoliticalRelation : IExposable
    {
        private string sourcePolityId;
        private string targetPolityId;
        private HistoricalPoliticalRelationType relationType;
        private bool isActive;

        public string SourcePolityId => sourcePolityId;
        public string TargetPolityId => targetPolityId;
        public HistoricalPoliticalRelationType RelationType => relationType;
        public bool IsActive => isActive;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sourcePolityId, "sourcePolityId");
            Scribe_Values.Look(ref targetPolityId, "targetPolityId");
            Scribe_Values.Look(ref relationType, "relationType", HistoricalPoliticalRelationType.Neutral);
            Scribe_Values.Look(ref isActive, "isActive", true);
        }

        internal void Initialize(string sourceId, string targetId, HistoricalPoliticalRelationType type, bool active)
        {
            sourcePolityId = sourceId;
            targetPolityId = targetId;
            relationType = type;
            isActive = active;
        }

        internal void SetActive(bool value)
        {
            isActive = value;
        }
    }
}
