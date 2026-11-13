using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Events;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Scenarios;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Characters
{
    public static class HistoricalCharacterRegistry
    {
        public static bool Exists(string historicalCharacterId) => GetComponent()?.Exists(historicalCharacterId) == true;
        public static Pawn GetPawn(string historicalCharacterId) => GetComponent()?.GetPawn(historicalCharacterId);
        public static bool TryGetCharacterState(string id, out HistoricalCharacterState state)
        {
            HistoricalCharacterWorldComponent component = GetComponent();
            state = component?.FindState(id);
            return state != null;
        }

        public static bool TryRegister(HistoricalCharacterDef def, out HistoricalCharacterState state)
        {
            state = GetComponent()?.GetOrRegister(def);
            return state != null;
        }

        private static HistoricalCharacterWorldComponent GetComponent() =>
            Current.Game?.GetComponent<HistoricalCharacterWorldComponent>();
    }

    internal static class HistoricalCharacterGenerator
    {
        private static readonly HashSet<string> ReportedHeldCharacters = new HashSet<string>(StringComparer.Ordinal);

        internal static Pawn GetOrGenerate(HistoricalCharacterDef def, Map map, IntVec3 cell)
        {
            if (def == null || map == null || !def.generateOnBattlefield) return null;
            HistoricalCharacterWorldComponent registry = Current.Game?.GetComponent<HistoricalCharacterWorldComponent>();
            if (registry == null) return null;
            HistoricalCharacterState state = registry.GetOrRegister(def);
            if (state == null) return null;
            if (state.IsHistoricallyDead || HistoricalFateExecutor.HasExecutedScheduledDeath(def.historicalCharacterId)) return null;
            if (state.Pawn != null)
            {
                if (state.Pawn.Spawned && state.Pawn.Map != map)
                {
                    Log.Warning("[Historical RimWorld] Historical character " + def.historicalCharacterId
                        + " is already present on another map; it was not duplicated.");
                    return state.Pawn;
                }
                if (state.Pawn.ParentHolder != null)
                {
                    if (ReportedHeldCharacters.Add(def.historicalCharacterId))
                        Log.Warning("[Historical RimWorld] Historical character " + def.historicalCharacterId
                            + " is held by another game container; it was not moved or duplicated.");
                    return state.Pawn;
                }
                if (!state.Pawn.Spawned && !state.Pawn.Destroyed && !state.Pawn.Dead)
                    GenSpawn.Spawn(state.Pawn, cell, map, WipeMode.Vanish);
                return state.Pawn;
            }
            if (state.GenerationAttempted) return null;

            Faction faction = FindFaction(def.historicalFactionId);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(def.pawnKindDefName);
            if (faction == null || kind == null)
            {
                Log.Error("[Historical RimWorld] Cannot generate historical character " + def.historicalCharacterId
                    + ": faction " + def.historicalFactionId + " or pawn kind " + def.pawnKindDefName + " is unavailable.");
                return null;
            }

            state.BeginGeneration();
            try
            {
                int age = Rand.RangeInclusive(def.minimumEstimatedAge, def.maximumEstimatedAge);
                Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction,
                    fixedBiologicalAge: age, fixedChronologicalAge: age,
                    forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: false, forceNoIdeo: true,
                    fixedGender: def.gender));
                if (pawn == null)
                {
                    state.ResetGenerationAttemptForRecovery();
                    Log.Error("[Historical RimWorld] PawnGenerator returned null for historical character " + def.historicalCharacterId + ".");
                    return null;
                }
                pawn.Name = new NameSingle(def.historicalName);
                registry.BindPawn(def, pawn);
                GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
                RegisterGenealogy(pawn);
                Log.Message("[Historical RimWorld] Generated unique historical character " + def.historicalCharacterId
                    + " (estimated game age " + age + ") for event " + def.relatedEventId + ".");
                return pawn;
            }
            catch (Exception exception)
            {
                state.ResetGenerationAttemptForRecovery();
                Log.Error("[Historical RimWorld] Historical character generation failed for " + def.historicalCharacterId + ": " + exception);
                return null;
            }
        }

        internal static Faction FindFaction(string defName)
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            return def == null ? null : Find.FactionManager?.AllFactions?.FirstOrDefault(item => item != null && item.def == def);
        }

        internal static void RegisterGenealogy(Pawn pawn)
        {
            if (pawn == null || !HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate date)) return;
            HistoricalGenealogyGameComponent genealogy = Current.Game?.GetComponent<HistoricalGenealogyGameComponent>();
            if (genealogy != null) genealogy.TryRegisterPawn(pawn, date, out _, out _);
        }
    }
}
