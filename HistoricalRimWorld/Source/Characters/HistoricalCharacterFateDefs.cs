using System;
using System.Collections.Generic;
using HistoricalRimWorld.Events;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public enum HistoricalProtectionLevel
    {
        Normal,
        InteractionProtected,
        FateProtected,
        SceneLocked,
        DeadHistorical
    }

    public enum HistoricalFateType
    {
        ScheduledDeath,
        ScheduledSurvival,
        ScheduledDisappearance,
        ScheduledSuccession,
        HistoricalTransition
    }

    public enum HistoricalFateExecutionStatus
    {
        Pending,
        Executing,
        Completed,
        FailedRecoverable
    }

    public sealed class HistoricalCharacterProtectionDef : Def
    {
        public string historicalCharacterId;
        public HistoricalProtectionLevel protectionLevel = HistoricalProtectionLevel.Normal;
        public string sceneLockEventId;
        public string sceneLockFromStageId;
        public string sceneLockUntilStageId;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(historicalCharacterId)) yield return "historicalCharacterId is required.";
            if (!Enum.IsDefined(typeof(HistoricalProtectionLevel), protectionLevel)) yield return "protectionLevel is unknown.";
            if (!string.IsNullOrWhiteSpace(historicalCharacterId)
                && DefDatabase<HistoricalCharacterDef>.GetNamedSilentFail(historicalCharacterId) == null)
                yield return "No HistoricalCharacterDef exists for historicalCharacterId " + historicalCharacterId + ".";
            bool hasSceneRange = !string.IsNullOrWhiteSpace(sceneLockEventId)
                || !string.IsNullOrWhiteSpace(sceneLockFromStageId)
                || !string.IsNullOrWhiteSpace(sceneLockUntilStageId);
            if (hasSceneRange && (string.IsNullOrWhiteSpace(sceneLockEventId)
                || string.IsNullOrWhiteSpace(sceneLockFromStageId) || string.IsNullOrWhiteSpace(sceneLockUntilStageId)))
                yield return "Scene lock requires event, start stage, and end stage IDs.";
        }
    }

    public sealed class HistoricalFateDef : Def
    {
        public string historicalCharacterId;
        public string fateEventId;
        public string fateStageId;
        public HistoricalFateType fateType;
        public bool preventPrematureDeath;
        public bool preventPlayerCapture = true;
        public bool preventForcedRecruitment = true;
        public bool preventForcedRelocation = true;
        public bool preserveHistoricalIdentity = true;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (string.IsNullOrWhiteSpace(historicalCharacterId)) yield return "historicalCharacterId is required.";
            if (string.IsNullOrWhiteSpace(fateEventId)) yield return "fateEventId is required.";
            if (string.IsNullOrWhiteSpace(fateStageId)) yield return "fateStageId is required.";
            if (!Enum.IsDefined(typeof(HistoricalFateType), fateType)) yield return "fateType is unknown.";
            if (!string.IsNullOrWhiteSpace(historicalCharacterId)
                && DefDatabase<HistoricalCharacterDef>.GetNamedSilentFail(historicalCharacterId) == null)
                yield return "No HistoricalCharacterDef exists for historicalCharacterId " + historicalCharacterId + ".";
            if (!string.IsNullOrWhiteSpace(fateEventId))
            {
                HistoricalEventDef eventDef = HistoricalEventValidator.FindEventDef(fateEventId);
                if (eventDef == null) yield return "No HistoricalEventDef exists for fateEventId " + fateEventId + ".";
                else if (!string.IsNullOrWhiteSpace(fateStageId)
                    && !(eventDef.stages ?? new List<HistoricalEventStageEntry>()).Exists(stage => stage != null && stage.stageId == fateStageId))
                    yield return "Historical event " + fateEventId + " does not define stage " + fateStageId + ".";
            }
        }
    }
}
