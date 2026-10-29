using System;
using HistoricalRimWorld.Chronicle;
using HistoricalRimWorld.Events.Conditions;
using HistoricalRimWorld.Politics;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Events.Actions
{
    public sealed class Action_SetPolityStatus : HistoricalEventAction
    {
        public string polityId;
        public HistoricalPolityStatus status;
        public override bool ValidateData(out string error)
        {
            if (!Condition_PolityActive.ValidatePolityDef(polityId, out error)) return false;
            if (!Enum.IsDefined(typeof(HistoricalPolityStatus), status)) { error = "Unknown polity status."; return false; }
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (!Condition_PolityActive.ValidatePolity(polityId, out error)) return false;
            if (!Enum.IsDefined(typeof(HistoricalPolityStatus), status)) { error = "Unknown polity status."; return false; }
            error = null; return true;
        }
        public override bool Execute(HistoricalEventContext context, out string error) =>
            HistoricalPoliticsManager.TrySetPolityStatus(polityId, status, HistoricalPoliticalChangeSource.HistoricalEvent, out error);
    }

    public sealed class Action_ChangeRegionController : HistoricalEventAction
    {
        public string regionId;
        public string polityId;
        public override bool ValidateData(out string error)
        {
            if (string.IsNullOrWhiteSpace(regionId) || DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null) { error = "Missing region Def " + regionId + "."; return false; }
            if (polityId != null) return Condition_PolityActive.ValidatePolityDef(polityId, out error);
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (!HistoricalPoliticsManager.TryGetRegion(regionId, out _)) { error = "Unknown active political region " + regionId + "."; return false; }
            if (polityId != null && !Condition_PolityActive.ValidatePolity(polityId, out error)) return false;
            error = null; return true;
        }
        public override bool Execute(HistoricalEventContext context, out string error) =>
            HistoricalPoliticsManager.TryChangeRegionController(regionId, polityId, HistoricalPoliticalChangeSource.HistoricalEvent, out error);
    }

    public sealed class Action_ChangeRegionNominalOwner : HistoricalEventAction
    {
        public string regionId;
        public string polityId;
        public override bool ValidateData(out string error)
        {
            if (string.IsNullOrWhiteSpace(regionId) || DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null) { error = "Missing region Def " + regionId + "."; return false; }
            if (polityId != null) return Condition_PolityActive.ValidatePolityDef(polityId, out error);
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (!HistoricalPoliticsManager.TryGetRegion(regionId, out _)) { error = "Unknown active political region " + regionId + "."; return false; }
            if (polityId != null && !Condition_PolityActive.ValidatePolity(polityId, out error)) return false;
            error = null; return true;
        }
        public override bool Execute(HistoricalEventContext context, out string error) =>
            HistoricalPoliticsManager.TryChangeRegionNominalOwner(regionId, polityId, HistoricalPoliticalChangeSource.HistoricalEvent, out error);
    }

    public sealed class Action_ChangePoliticalRelation : HistoricalEventAction
    {
        public string sourcePolityId;
        public string targetPolityId;
        public HistoricalPoliticalRelationType relationType;
        public bool isActive = true;
        public override bool ValidateData(out string error)
        {
            if (!Condition_PolityActive.ValidatePolityDef(sourcePolityId, out error)) return false;
            if (!Condition_PolityActive.ValidatePolityDef(targetPolityId, out error)) return false;
            if (sourcePolityId == targetPolityId || !Enum.IsDefined(typeof(HistoricalPoliticalRelationType), relationType))
            { error = "Political relation target or type is invalid."; return false; }
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error) => ValidateData(out error);
        public override bool Execute(HistoricalEventContext context, out string error) =>
            HistoricalPoliticsManager.TryChangePoliticalRelation(sourcePolityId, targetPolityId, relationType,
                isActive, HistoricalPoliticalChangeSource.HistoricalEvent, out error);
    }

    public sealed class Action_UnlockRegion : HistoricalEventAction
    {
        public string regionId;
        public override bool ValidateData(out string error)
        {
            if (string.IsNullOrWhiteSpace(regionId) || DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null) { error = "Missing region Def " + regionId + "."; return false; }
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (!HistoricalWorldRevealManager.TryGetRegionAccess(regionId, out _)) { error = "Unknown active reveal region " + regionId + "."; return false; }
            error = null; return true;
        }
        public override bool Execute(HistoricalEventContext context, out string error) =>
            HistoricalWorldRevealManager.TryUnlockRegion(regionId, HistoricalRevealSource.HistoricalEvent, false, out error);
    }

    public sealed class Action_RevealRegion : HistoricalEventAction
    {
        public string regionId;
        public override bool ValidateData(out string error)
        {
            if (string.IsNullOrWhiteSpace(regionId) || DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null) { error = "Missing region Def " + regionId + "."; return false; }
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (!HistoricalWorldRevealManager.TryGetRegionAccess(regionId, out _)) { error = "Unknown active reveal region " + regionId + "."; return false; }
            error = null; return true;
        }
        public override bool Execute(HistoricalEventContext context, out string error) =>
            HistoricalWorldRevealManager.TryRevealRegion(regionId, HistoricalRevealSource.HistoricalEvent, false, out error);
    }

    public sealed class Action_AddChronicleEntry : HistoricalEventAction
    {
        public string entryId;
        public string titleKey;
        public string descriptionKey;
        public string regionId;
        public string polityId;
        public override bool IsStateMutation => false;
        public override bool ValidateData(out string error)
        {
            if (string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(titleKey) || string.IsNullOrWhiteSpace(descriptionKey))
            { error = "Chronicle entry ID, title key, and description key are required."; return false; }
            if (!string.IsNullOrWhiteSpace(regionId) && DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
            { error = "Missing chronicle region Def " + regionId + "."; return false; }
            if (!string.IsNullOrWhiteSpace(polityId) && DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polityId) == null)
            { error = "Missing chronicle polity Def " + polityId + "."; return false; }
            error = null; return true;
        }
        public override bool Validate(HistoricalEventContext context, out string error)
        {
            if (!HistoricalChronicleManager.IsActive) { error = "Historical Chronicle is not initialized."; return false; }
            if (string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(titleKey) || string.IsNullOrWhiteSpace(descriptionKey))
            { error = "Chronicle entry ID, title key, and description key are required."; return false; }
            if (!string.IsNullOrWhiteSpace(regionId) && DefDatabase<HistoricalRegionDef>.GetNamedSilentFail(regionId) == null)
            { error = "Missing chronicle region Def " + regionId + "."; return false; }
            if (!string.IsNullOrWhiteSpace(polityId) && DefDatabase<HistoricalStateDef>.GetNamedSilentFail(polityId) == null)
            { error = "Missing chronicle polity Def " + polityId + "."; return false; }
            error = null; return true;
        }
        public override bool Execute(HistoricalEventContext context, out string error) =>
            HistoricalChronicleManager.TryAddCustomEventEntry(entryId, context.Event.defName,
                context.Event.historicalDate, titleKey, descriptionKey, regionId, polityId,
                context.Event.evidenceLevel, context.Event.sourceNotes, context.Event.category,
                context.Event.scope, HistoricalEventManager.IsVisible(context.Event), out error);
    }
}
