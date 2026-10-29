using System.Collections.Generic;
using Verse;

namespace HistoricalRimWorld.Rituals
{
    public sealed class HistoricalRitualInstance : IExposable
    {
        public string RitualInstanceId;
        public string RitualDefName;
        public string CommunityId;
        public string HostPersonId;
        public string FamilyId;
        public List<string> ParticipantPersonIds = new List<string>();
        public HistoricalRitualStatus Status;
        public int StartGameTick = -1;
        public int CompletionGameTick = -1;
        public int MapUniqueId = -1;
        public int CellX;
        public int CellZ;
        public string TargetPersonId;
        public void ExposeData()
        {
            Scribe_Values.Look(ref RitualInstanceId, "ritualInstanceId");
            Scribe_Values.Look(ref RitualDefName, "ritualDefName");
            Scribe_Values.Look(ref CommunityId, "communityId");
            Scribe_Values.Look(ref HostPersonId, "hostPersonId");
            Scribe_Values.Look(ref FamilyId, "familyId");
            Scribe_Collections.Look(ref ParticipantPersonIds, "participantPersonIds", LookMode.Value);
            Scribe_Values.Look(ref Status, "status", HistoricalRitualStatus.Planned);
            Scribe_Values.Look(ref StartGameTick, "startGameTick", -1);
            Scribe_Values.Look(ref CompletionGameTick, "completionGameTick", -1);
            Scribe_Values.Look(ref MapUniqueId, "mapUniqueId", -1);
            Scribe_Values.Look(ref CellX, "cellX");
            Scribe_Values.Look(ref CellZ, "cellZ");
            Scribe_Values.Look(ref TargetPersonId, "targetPersonId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) ParticipantPersonIds = ParticipantPersonIds ?? new List<string>();
        }
    }
}
