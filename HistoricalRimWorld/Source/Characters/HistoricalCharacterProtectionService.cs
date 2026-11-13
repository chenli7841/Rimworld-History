using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Events;
using Verse;

namespace HistoricalRimWorld.Characters
{
    /// <summary>Central, stable-ID based protection rules for registered historical Pawns.</summary>
    public static class HistoricalCharacterProtectionService
    {
        private static readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<Pawn> authorizedHistoricalRelocations = new HashSet<Pawn>();

        public static bool TryGetHistoricalCharacterId(Pawn pawn, out string historicalCharacterId) =>
            Current.Game?.GetComponent<HistoricalCharacterWorldComponent>()
                ?.TryGetHistoricalCharacterId(pawn, out historicalCharacterId) ?? Clear(out historicalCharacterId);

        private static bool Clear(out string id) { id = null; return false; }

        public static HistoricalProtectionLevel GetProtectionLevel(Pawn pawn)
        {
            if (!TryGetHistoricalCharacterId(pawn, out string id)) return HistoricalProtectionLevel.Normal;
            HistoricalCharacterWorldComponent characters = Current.Game?.GetComponent<HistoricalCharacterWorldComponent>();
            HistoricalCharacterState state = characters?.FindState(id);
            if (state?.IsHistoricallyDead == true || (pawn != null && pawn.Dead))
                return HistoricalProtectionLevel.DeadHistorical;

            HistoricalCharacterProtectionDef protection = DefDatabase<HistoricalCharacterProtectionDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.historicalCharacterId == id);
            HistoricalProtectionLevel level = protection?.protectionLevel ?? HistoricalProtectionLevel.Normal;
            foreach (HistoricalFateDef fate in DefDatabase<HistoricalFateDef>.AllDefsListForReading
                         .Where(item => item != null && item.historicalCharacterId == id))
            {
                bool executed = HistoricalFateExecutor.HasExecutedFate(fate);
                bool stageReached = HistoricalEventManager.HasReachedStage(fate.fateEventId, fate.fateStageId);
                if (fate.fateType == HistoricalFateType.ScheduledSurvival && (executed || !stageReached))
                    level = Max(level, HistoricalProtectionLevel.FateProtected);
                else if (fate.preventPrematureDeath && !executed)
                    level = Max(level, HistoricalProtectionLevel.FateProtected);

                if (fate.preventPlayerCapture && !executed)
                    level = Max(level, HistoricalProtectionLevel.InteractionProtected);
                if (fate.preventForcedRecruitment && !executed)
                    level = Max(level, HistoricalProtectionLevel.InteractionProtected);
                if (fate.preserveHistoricalIdentity && !executed)
                    level = Max(level, HistoricalProtectionLevel.InteractionProtected);
                if (fate.preventForcedRelocation && !executed)
                    level = Max(level, HistoricalProtectionLevel.FateProtected);
            }

            if (protection != null && SceneLockActive(protection))
                level = Max(level, HistoricalProtectionLevel.SceneLocked);
            return level;
        }

        private static bool SceneLockActive(HistoricalCharacterProtectionDef protection) =>
            HistoricalEventManager.HasReachedStage(protection.sceneLockEventId, protection.sceneLockFromStageId)
            && !HistoricalEventManager.HasReachedStage(protection.sceneLockEventId, protection.sceneLockUntilStageId);

        private static HistoricalProtectionLevel Max(HistoricalProtectionLevel left, HistoricalProtectionLevel right) =>
            (int)left >= (int)right ? left : right;

        public static bool IsProtected(Pawn pawn) => GetProtectionLevel(pawn) != HistoricalProtectionLevel.Normal;
        public static bool IsFateProtected(Pawn pawn) => GetProtectionLevel(pawn) >= HistoricalProtectionLevel.FateProtected
            && GetProtectionLevel(pawn) != HistoricalProtectionLevel.DeadHistorical;
        public static bool CanPlayerTarget(Pawn pawn) => GetProtectionLevel(pawn) < HistoricalProtectionLevel.FateProtected;
        public static bool CanCapture(Pawn pawn) => GetProtectionLevel(pawn) < HistoricalProtectionLevel.InteractionProtected;
        public static bool CanRecruit(Pawn pawn) => GetProtectionLevel(pawn) < HistoricalProtectionLevel.InteractionProtected;
        public static bool CanRelocate(Pawn pawn) => HistoricalFateExecutor.IsDeathAuthorized(pawn)
            || (pawn != null && authorizedHistoricalRelocations.Contains(pawn))
            || GetProtectionLevel(pawn) < HistoricalProtectionLevel.InteractionProtected;
        public static bool CanChangeFaction(Pawn pawn) => HistoricalFateExecutor.IsDeathAuthorized(pawn)
            || GetProtectionLevel(pawn) < HistoricalProtectionLevel.InteractionProtected;
        public static bool CanDieNaturally(Pawn pawn) => !IsFateProtected(pawn)
            || HistoricalFateExecutor.IsDeathAuthorized(pawn);
        public static bool CanReceiveOrdinaryDamage(Pawn pawn) => !IsFateProtected(pawn)
            || HistoricalFateExecutor.IsDeathAuthorized(pawn);

        /// <summary>
        /// Opens a short, explicit relocation window for a historical event controller.
        /// Call only after that event stage is recorded, then always close it in a finally block.
        /// Player actions never receive this authorization.
        /// </summary>
        public static bool TryBeginHistoricalRelocation(Pawn pawn, string eventId, string stageId)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed
                || !TryGetHistoricalCharacterId(pawn, out _)
                || !HistoricalEventManager.HasReachedStage(eventId, stageId)) return false;
            authorizedHistoricalRelocations.Add(pawn);
            return true;
        }

        public static void EndHistoricalRelocation(Pawn pawn)
        {
            if (pawn != null) authorizedHistoricalRelocations.Remove(pawn);
        }

        public static void ReportBlocked(string action, Pawn pawn)
        {
            if (!TryGetHistoricalCharacterId(pawn, out string id)) return;
            string key = action + ":" + id;
            if (reported.Add(key))
                Log.Warning("[Historical RimWorld] Blocked " + action + " for protected historical character " + id + ".");
        }
    }
}
