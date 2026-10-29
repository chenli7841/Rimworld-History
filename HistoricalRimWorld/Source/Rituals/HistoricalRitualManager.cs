using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Culture;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Rituals
{
    public static class HistoricalRitualManager
    {
        private static HistoricalRitualGameComponent Component => Current.Game?.GetComponent<HistoricalRitualGameComponent>();
        public static bool IsActive => Component?.Initialized == true && HistoricalScenarioManager.IsScenarioActive;
        public static IEnumerable<HistoricalRitualInstance> Instances => Component?.Instances ?? Enumerable.Empty<HistoricalRitualInstance>();

        public static bool TryInitializeForScenario(HistoricalScenarioDef scenario, out string error)
        {
            error = null;
            if (scenario == null || HistoricalScenarioManager.CurrentScenario?.defName != scenario.defName) return Fail("Scenario must be active before rituals initialize.", out error);
            if (Component == null) return Fail("HistoricalRitualGameComponent is unavailable.", out error);
            return Component.Initialize(scenario.defName) || Fail("Ritual state belongs to another scenario.", out error);
        }

        public static bool TryPlanRitual(string ritualDefName, Pawn hostPawn, out string instanceId, out string error)
        {
            instanceId = null;
            error = null;
            if (!IsActive) return Fail("Historical rituals are inactive.", out error);
            HistoricalRitualDef ritual = DefDatabase<HistoricalRitualDef>.GetNamedSilentFail(ritualDefName);
            if (ritual == null || ritual.minimumParticipants < 1 || ritual.durationTicks < 0) return Fail("Ritual Def is missing or invalid.", out error);
            if (hostPawn == null || hostPawn.Dead || hostPawn.Destroyed || !hostPawn.Spawned || hostPawn.Faction != Faction.OfPlayer)
                return Fail("Select a living, spawned colony host.", out error);
            if (!HistoricalGenealogyManager.TryGetPersonId(hostPawn, out string hostId)) return Fail("The host has no historical PersonId.", out error);
            if (!HistoricalCultureManager.TryEnsurePerson(hostId, out error)) return false;
            string familyId = HistoricalGenealogyManager.FindPerson(hostId)?.FamilyId;
            string targetId = ritual.category == HistoricalRitualCategory.Funeral
                ? HistoricalGenealogyManager.FindFamily(familyId)?.MemberPersonIds.Select(HistoricalGenealogyManager.FindPerson).Where(person => person != null && !person.IsAlive)
                    .OrderByDescending(person => person.DeathDate?.AbsoluteDay ?? long.MinValue).FirstOrDefault()?.PersonId
                : null;
            if (ritual.category == HistoricalRitualCategory.Funeral && targetId == null) return Fail("A funeral or memorial requires a deceased family member.", out error);

            int tick = Find.TickManager?.TicksGame ?? -1;
            if (tick < 0) return Fail("The game tick manager is unavailable.", out error);
            instanceId = "HR_Ritual_" + Guid.NewGuid().ToString("N");
            Component.Add(new HistoricalRitualInstance
            {
                RitualInstanceId = instanceId,
                RitualDefName = ritual.defName,
                CommunityId = HistoricalCultureManager.Communities.FirstOrDefault()?.CommunityId,
                HostPersonId = hostId,
                FamilyId = familyId,
                ParticipantPersonIds = new List<string> { hostId },
                Status = HistoricalRitualStatus.Planned,
                StartGameTick = tick,
                MapUniqueId = hostPawn.Map.uniqueID,
                CellX = hostPawn.Position.x,
                CellZ = hostPawn.Position.z,
                TargetPersonId = targetId
            });
            return true;
        }

        public static bool TryCompleteRitual(string instanceId, out string error)
        {
            error = null;
            HistoricalRitualInstance instance = IsActive ? Component.Find(instanceId) : null;
            if (instance == null || (instance.Status != HistoricalRitualStatus.Planned && instance.Status != HistoricalRitualStatus.InProgress)) return Fail("Only an active ritual can be completed.", out error);
            HistoricalRitualDef def = DefDatabase<HistoricalRitualDef>.GetNamedSilentFail(instance.RitualDefName);
            Pawn host;
            if (!HistoricalGenealogyManager.TryGetPawn(instance.HostPersonId, out host) || host == null || host.Dead || host.Destroyed)
                return SetFailed(instance, "Host is no longer available.", out error);
            Map map = Find.Maps.FirstOrDefault(item => item != null && item.uniqueID == instance.MapUniqueId);
            IntVec3 site = new IntVec3(instance.CellX, 0, instance.CellZ);
            if (map == null || host.Map != map || !host.Spawned || host.Position.DistanceToSquared(site) > 25)
                return SetFailed(instance, "Ritual location or host is no longer available.", out error);
            if (def == null) return SetFailed(instance, "Ritual Def was removed.", out error);

            List<Pawn> participants = map.mapPawns.AllPawnsSpawned.Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned
                && pawn.Position.DistanceToSquared(site) <= 25
                && (pawn.Faction == Faction.OfPlayer || (def.category == HistoricalRitualCategory.Hospitality && pawn.Faction != null && !pawn.Faction.HostileTo(Faction.OfPlayer))))
                .Distinct().ToList();
            if (participants.Count < def.minimumParticipants) return Fail("Gather the required participants at the selected site first.", out error);
            if (def.category == HistoricalRitualCategory.Hospitality && !participants.Any(pawn => pawn.Faction != Faction.OfPlayer))
                return Fail("Hospitality requires a non-hostile visitor at the gathering.", out error);

            Thing offering = null;
            if (def.requiredOffering != null)
            {
                offering = map.listerThings.AllThings.FirstOrDefault(thing => thing != null && !thing.Destroyed
                    && thing.Spawned && thing.Map == map && thing.Position.DistanceToSquared(site) <= 25
                    && thing.def == def.requiredOffering && thing.stackCount > 0);
                if (offering == null) return Fail("The required offering is not available on this map.", out error);
            }
            if (def.durationTicks > 0 && Find.TickManager.TicksGame - instance.StartGameTick < def.durationTicks)
            {
                instance.Status = HistoricalRitualStatus.InProgress;
                return Fail("The gathering has not lasted long enough.", out error);
            }

            // The ceremony can only be completed after validating real participants, presence and offering.
            // A failed precondition does not consume an offering or change the status.
            if (offering != null) offering.SplitOff(1).Destroy(DestroyMode.Vanish);
            instance.ParticipantPersonIds = participants.Select(pawn => HistoricalGenealogyManager.TryGetPersonId(pawn, out string id) ? id : null)
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
            if (def.completionThought != null)
                foreach (Pawn pawn in participants)
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtMaker.MakeThought(def.completionThought, (Precept)null), null);
            foreach (HistoricalCultureDef culture in def.associatedCultures ?? new List<HistoricalCultureDef>())
                foreach (string personId in instance.ParticipantPersonIds)
                    HistoricalCultureManager.TryIncreaseFamiliarity(personId, culture.defName, 0.05f, HistoricalCulturalTransmissionSource.Ritual, out _);
            if (def.category == HistoricalRitualCategory.AncestorRite)
            {
                if (!string.IsNullOrWhiteSpace(def.preservedTraditionId))
                    HistoricalCultureManager.TryAddPreservedTradition(instance.FamilyId, def.preservedTraditionId, out _);
            }
            instance.Status = HistoricalRitualStatus.Completed;
            instance.CompletionGameTick = Find.TickManager.TicksGame;
            HistoricalCultureManager.TryRecordCultureExperience("Ritual:" + instance.RitualInstanceId,
                "HR_Ritual_Chronicle_Title", "HR_Ritual_Chronicle_Description", def.evidenceLevel, def.sourceNotes, out _);
            return true;
        }

        public static bool TryCancelRitual(string instanceId, out string error)
        {
            error = null;
            HistoricalRitualInstance item = IsActive ? Component.Find(instanceId) : null;
            if (item == null || (item.Status != HistoricalRitualStatus.Planned && item.Status != HistoricalRitualStatus.InProgress)) return Fail("Only an active ritual can be cancelled.", out error);
            item.Status = HistoricalRitualStatus.Cancelled;
            return true;
        }

        private static bool SetFailed(HistoricalRitualInstance instance, string reason, out string error)
        {
            instance.Status = HistoricalRitualStatus.Failed;
            error = reason;
            return false;
        }
        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
