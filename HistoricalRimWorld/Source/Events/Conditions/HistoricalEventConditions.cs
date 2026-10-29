using System;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Events.Conditions
{
    public sealed class Condition_PolityActive : HistoricalEventCondition
    {
        public string polityId;
        public override bool ValidateData(out string error) => ValidatePolityDef(polityId, out error);
        public override bool Validate(HistoricalEventContext context, out string error) => ValidatePolity(polityId, out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalPoliticsManager.TryGetPolity(polityId, out HistoricalPolityState state)
            && state.Status != HistoricalPolityStatus.Dissolved;
        internal static bool ValidatePolity(string id, out string error)
        {
            if (!ValidatePolityDef(id, out error)) return false;
            if (!HistoricalPoliticsManager.TryGetPolity(id, out _)) { error = "Historical polity is absent from the active political setup: " + id + "."; return false; }
            error = null; return true;
        }
        internal static bool ValidatePolityDef(string id, out string error)
        {
            if (string.IsNullOrWhiteSpace(id) || DefDatabase<HistoricalStateDef>.GetNamedSilentFail(id) == null)
            { error = "Missing historical polity Def " + id + "."; return false; }
            error = null; return true;
        }
    }

    public sealed class Condition_PolityDissolved : HistoricalEventCondition
    {
        public string polityId;
        public override bool ValidateData(out string error) => Condition_PolityActive.ValidatePolityDef(polityId, out error);
        public override bool Validate(HistoricalEventContext context, out string error) => Condition_PolityActive.ValidatePolity(polityId, out error);
        public override bool Evaluate(HistoricalEventContext context) => HistoricalPoliticsManager.TryGetPolity(polityId, out HistoricalPolityState state)
            && state.Status == HistoricalPolityStatus.Dissolved;
    }

    public sealed class Condition_RegionControlledBy : HistoricalEventCondition
    {
        public string regionId;
        public string polityId;
        public override bool ValidateData(out string error)
        {
            if (string.IsNullOrWhiteSpace(regionId) || DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
            { error = "Missing historical region Def " + regionId + "."; return false; }
            if (polityId != null) return Condition_PolityActive.ValidatePolityDef(polityId, out error);
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (string.IsNullOrWhiteSpace(regionId) || DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null
                || !HistoricalPoliticsManager.TryGetRegion(regionId, out _)) { error = "Missing active historical region " + regionId + "."; return false; }
            if (polityId != null && !Condition_PolityActive.ValidatePolity(polityId, out error)) return false;
            error = null; return true;
        }
        public override bool Evaluate(HistoricalEventContext context) => HistoricalPoliticsManager.TryGetRegion(regionId, out HistoricalRegionPoliticalState state)
            && string.Equals(state.EffectiveControllerId, polityId, StringComparison.Ordinal);
    }

    public sealed class Condition_RegionAccessAtLeast : HistoricalEventCondition
    {
        public string regionId;
        public HistoricalRegionAccess access;
        public override bool ValidateData(out string error)
        {
            if (!Enum.IsDefined(typeof(HistoricalRegionAccess), access)
                || string.IsNullOrWhiteSpace(regionId) || DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
            { error = "Invalid or missing historical region/access value " + regionId + "."; return false; }
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (!Enum.IsDefined(typeof(HistoricalRegionAccess), access)
                || !HistoricalWorldRevealManager.TryGetRegionAccess(regionId, out _))
            { error = "Invalid or unavailable region reveal state for " + regionId + "."; return false; }
            error = null; return true;
        }
        public override bool Evaluate(HistoricalEventContext context) => HistoricalWorldRevealManager.TryGetRegionAccess(regionId, out HistoricalRegionAccess current)
            && (int)current >= (int)access;
    }

    public sealed class Condition_EventExecuted : HistoricalEventCondition
    {
        public string eventId;
        public override bool ValidateData(out string error)
        {
            if (string.IsNullOrWhiteSpace(eventId) || DefDatabase<HistoricalEventDef>.GetNamedSilentFail(eventId) == null)
            { error = "Missing prerequisite event Def " + eventId + "."; return false; }
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (string.IsNullOrWhiteSpace(eventId) || DefDatabase<HistoricalEventDef>.GetNamedSilentFail(eventId) == null)
            { error = "Missing prerequisite event Def " + eventId + "."; return false; }
            if (!HistoricalEventManager.IsEventKnownToCurrentScenario(eventId))
            { error = "Prerequisite event is not registered for the active scenario: " + eventId + "."; return false; }
            error = null; return true;
        }
        public override bool Evaluate(HistoricalEventContext context) => HistoricalEventManager.WasExecuted(eventId);
    }
}
