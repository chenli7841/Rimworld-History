using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Factions;
using HistoricalRimWorld.World;
using RimWorld;
using Verse;
using Verse.AI;

namespace HistoricalRimWorld.Events
{
    /// <summary>Low-frequency battle-only orders built from vanilla Pawn jobs and combat verbs.</summary>
    public static class HistoricalBattleController
    {
        private const string KingId = "HR_Character_ZhouYouWang";

        public static void BeginEngagement(HistoricalBattleDef def, HistoricalBattleInstance instance, int tick)
        {
            if (def == null || instance == null || instance.BattleCompleted || instance.ActiveMap == null) return;
            HistoricalArmyGameComponent armies = Current.Game?.GetComponent<HistoricalArmyGameComponent>();
            HistoricalArmyRuntime royal = armies?.FindArmy(def.royalArmyId);
            HistoricalArmyRuntime opposing = armies?.FindArmy(def.opposingArmyId);
            if (royal == null || opposing == null || !royal.DeploymentComplete || !opposing.DeploymentComplete) return;
            EnsureOpposition(def);
            if (!instance.BattleStarted)
            {
                instance.StartBattle(tick);
                instance.MarkTransition("engagement");
                Log.Message("[Historical RimWorld] " + def.battleId + " entered live engagement with deployed Pawn armies.");
            }
            TickLiveBattle(def, instance, tick);
        }

        public static void BeginRoyalRetreat(HistoricalBattleDef def, HistoricalBattleInstance instance, int tick)
        {
            if (def == null || instance == null || instance.ActiveMap == null || instance.BattleCompleted) return;
            instance.StartRoyalRetreat(tick);
            if (!instance.MarkTransition("royal-retreat")) return;
            HistoricalArmyRuntime royal = Current.Game?.GetComponent<HistoricalArmyGameComponent>()?.FindArmy(def.royalArmyId);
            if (royal == null) return;
            CellRect corridor = GetZone(instance.ActiveMap, def.royalRetreatZoneId);
            IntVec3 destination = FindSafeCell(instance.ActiveMap, corridor, corridor.CenterCell);
            foreach (Pawn pawn in Pawns(royal, instance.ActiveMap))
            {
                if (pawn.def?.defName == "Human" && pawn.Dead) continue;
                OrderGoto(pawn, destination);
            }
            Log.Message("[Historical RimWorld] " + def.battleId + " began the timeline-mandated royal retreat using ordinary Pawn movement jobs.");
        }

        public static void EnterDeathScene(HistoricalBattleDef def, HistoricalBattleInstance instance, int tick)
        {
            if (def == null || instance == null || instance.BattleCompleted) return;
            BeginRoyalRetreat(def, instance, tick);
            instance.StartDeathScene(tick);
            if (!instance.MarkTransition("death-scene-started") && instance.KingYouFateExecuted) return;

            HistoricalCharacterState royal = Current.Game?.GetComponent<HistoricalCharacterWorldComponent>()?.FindState(def.kingCharacterId);
            Pawn king = royal?.Pawn;
            if (king != null && !king.Dead && king.Spawned && king.Map == instance.ActiveMap)
            {
                CellRect zone = GetZone(instance.ActiveMap, def.deathSceneZoneId);
                IntVec3 target = FindSafeCell(instance.ActiveMap, zone, zone.CenterCell);
                if (target.IsValid) OrderGoto(king, target);
                if (target.IsValid && king.Position.DistanceToSquared(target) <= 36)
                    instance.MarkDeathScenePlayed();
            }

            int elapsed = tick - instance.DeathSceneStartTick;
            bool stagingComplete = instance.KingYouDeathScenePlayed && elapsed >= 300;
            bool timedOut = elapsed >= def.deathSceneBudgetTicks;
            if (stagingComplete || timedOut || king == null || king.Dead || !king.Spawned)
            {
                bool committed = HistoricalFateExecutor.ExecuteScheduledDeath(def.kingCharacterId,
                    def.historicalEventId, def.deathStageId);
                if (committed)
                {
                    instance.MarkFateExecuted();
                    instance.MarkDeathScenePlayed();
                    instance.MarkTransition("king-you-fate-executed");
                    Log.Message("[Historical RimWorld] " + def.battleId + " completed King You's scheduled death through HistoricalFateExecutor"
                        + (timedOut ? " after the bounded scene budget." : " after the battlefield staging."));
                }
            }
        }

        public static bool ShouldDeferScheduledDeath(HistoricalFateDef fate) =>
            HistoricalBattleTimelineBridge.ShouldDeferScheduledDeath(fate);

        public static void EnterAftermath(HistoricalBattleDef def, HistoricalBattleInstance instance, int tick)
        {
            if (def == null || instance == null || instance.BattleCompleted) return;
            if (!instance.HasTransition("aftermath"))
            {
                if (!instance.KingYouFateExecuted
                    && HistoricalEventManager.HasReachedStage(def.historicalEventId, def.deathStageId))
                {
                    bool fateCommitted = HistoricalFateExecutor.ExecuteScheduledDeath(def.kingCharacterId,
                        def.historicalEventId, def.deathStageId);
                    if (fateCommitted) instance.MarkFateExecuted();
                }
                instance.MarkTransition("aftermath");
                instance.EnterPhase(HistoricalBattlePhase.Aftermath, tick);
                StopBattleJobs(def, instance);
                instance.MarkOutcomeCommitted();
                Log.Message("[Historical RimWorld] " + def.battleId + " entered aftermath; living Pawns, corpses, equipment and map changes remain on the map.");
            }
        }

        public static void ResolveOffMap(HistoricalBattleDef def, HistoricalBattleInstance instance, int tick)
        {
            if (def == null || instance == null || instance.BattleCompleted) return;
            // No individual Pawn casualties are invented. HE-001 remains the authority for defeat and death.
            StopBattleJobs(def, instance);
            instance.ResolveOffMap(tick);
            Log.Message("[Historical RimWorld] " + def.battleId + " committed its off-map historical defeat; no simulated casualty list was created.");
        }

        public static void TickLiveBattle(HistoricalBattleDef def, HistoricalBattleInstance instance, int tick)
        {
            if (def == null || instance == null || instance.BattleCompleted || instance.ActiveMap == null) return;
            HistoricalArmyGameComponent component = Current.Game?.GetComponent<HistoricalArmyGameComponent>();
            HistoricalArmyRuntime royal = component?.FindArmy(def.royalArmyId);
            HistoricalArmyRuntime opposing = component?.FindArmy(def.opposingArmyId);
            if (royal == null || opposing == null) return;
            if (instance.LastOrdersTick < 0 || tick - instance.LastOrdersTick >= def.combatOrderIntervalTicks)
                IssueCombatOrders(def, instance, royal, opposing, tick);
            if (tick - instance.LastMoraleTick >= def.moraleCheckIntervalTicks)
            {
                List<Pawn> royals = Pawns(royal, instance.ActiveMap).ToList();
                List<Pawn> enemies = Pawns(opposing, instance.ActiveMap).ToList();
                int royalDead = royal.Members.Count(member => member?.Pawn == null || member.Pawn.Dead || member.Pawn.Destroyed);
                int enemyDead = opposing.Members.Count(member => member?.Pawn == null || member.Pawn.Dead || member.Pawn.Destroyed);
                float survival = royal.Members.Count() == 0 ? 0f : (float)royals.Count / royal.Members.Count();
                float nearby = royals.Count == 0 || enemies.Count == 0 ? 0f : Math.Min(1f, (float)royals.Count / Math.Max(1, enemies.Count));
                float morale = Math.Max(0f, Math.Min(1f, 0.72f * survival + 0.28f * nearby));
                instance.MarkMorale(tick, morale, royalDead, enemyDead);
                // Local battlefield morale may produce an early partial withdrawal, but cannot advance the historical event.
                if (morale < 0.28f && !instance.HasTransition("morale-withdrawal"))
                {
                    instance.MarkTransition("morale-withdrawal");
                    OrderRoyalRearGuardToRetreat(def, instance, royal);
                    Log.Message("[Historical RimWorld] Royal battlefield morale triggered a partial withdrawal; HE-001 timeline is unchanged.");
                }
            }
        }

        private static void IssueCombatOrders(HistoricalBattleDef def, HistoricalBattleInstance instance,
            HistoricalArmyRuntime royal, HistoricalArmyRuntime opposing, int tick)
        {
            if (instance.Phase != HistoricalBattlePhase.Engagement) return;
            List<Pawn> royalPawns = Pawns(royal, instance.ActiveMap).ToList();
            List<Pawn> oppositionPawns = Pawns(opposing, instance.ActiveMap).ToList();
            if (royalPawns.Count == 0 || oppositionPawns.Count == 0) { instance.MarkOrders(tick); return; }
            foreach (Pawn pawn in royalPawns)
                AssignAttackIfIdle(pawn, NearestTarget(pawn, oppositionPawns));
            foreach (Pawn pawn in oppositionPawns)
                AssignAttackIfIdle(pawn, NearestTarget(pawn, royalPawns));
            instance.MarkOrders(tick);
        }

        private static void AssignAttackIfIdle(Pawn pawn, Pawn target)
        {
            if (pawn == null || target == null || !pawn.Spawned || !pawn.RaceProps.Humanlike
                || pawn.Downed || pawn.Dead || pawn.Drafted || pawn.jobs == null || pawn.CurJob?.playerForced == true
                || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return;
            if (HistoricalCharacterProtectionService.IsProtected(pawn)) return;
            if (pawn.CurJob?.def == JobDefOf.Goto) return;
            if (pawn.CurJob != null && pawn.CurJob.targetA.Thing == target
                && (pawn.CurJob.def == JobDefOf.AttackMelee || pawn.CurJob.def == JobDefOf.AttackStatic)) return;
            bool melee = pawn.equipment?.Primary == null || pawn.equipment.Primary.def.IsMeleeWeapon;
            Job job = JobMaker.MakeJob(melee ? JobDefOf.AttackMelee : JobDefOf.AttackStatic, target);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private static Pawn NearestTarget(Pawn pawn, IEnumerable<Pawn> candidates) => candidates
            .Where(target => target != null && target.Spawned && !target.Dead && !target.Downed
                && target.Map == pawn.Map && !HistoricalCharacterProtectionService.IsFateProtected(target))
            .OrderBy(target => pawn.Position.DistanceToSquared(target.Position)).FirstOrDefault();

        private static void OrderRoyalRearGuardToRetreat(HistoricalBattleDef def, HistoricalBattleInstance instance,
            HistoricalArmyRuntime royal)
        {
            CellRect corridor = GetZone(instance.ActiveMap, def.royalRetreatZoneId);
            IntVec3 destination = FindSafeCell(instance.ActiveMap, corridor, corridor.CenterCell);
            foreach (Pawn pawn in Pawns(royal, instance.ActiveMap).Where(item => item != null && item.kindDef?.defName != "HR_PawnKind_NobleRetainer"))
                OrderGoto(pawn, destination);
        }

        private static void OrderGoto(Pawn pawn, IntVec3 destination)
        {
            if (pawn == null || !pawn.Spawned || !destination.IsValid || pawn.Drafted || pawn.CurJob?.playerForced == true) return;
            if (pawn.CurJob?.def == JobDefOf.Goto && pawn.CurJob.targetA.Cell == destination) return;
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, destination), JobTag.Misc);
        }

        private static void StopBattleJobs(HistoricalBattleDef def, HistoricalBattleInstance instance)
        {
            HistoricalArmyGameComponent component = Current.Game?.GetComponent<HistoricalArmyGameComponent>();
            foreach (Pawn pawn in (component?.FindArmy(def.royalArmyId)?.Members ?? Enumerable.Empty<HistoricalArmyMemberState>())
                         .Concat(component?.FindArmy(def.opposingArmyId)?.Members ?? Enumerable.Empty<HistoricalArmyMemberState>())
                         .Select(item => item?.Pawn).Where(item => item != null && item.Spawned && !item.Dead && !item.Downed))
            {
                if (pawn.CurJob?.playerForced == true) continue;
                Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = 60000;
                pawn.jobs.TryTakeOrderedJob(wait, JobTag.Misc);
            }
        }

        private static IEnumerable<Pawn> Pawns(HistoricalArmyRuntime army, Map map) => army?.Members
            .Select(item => item?.Pawn).Where(pawn => pawn != null && pawn.Spawned && pawn.Map == map && !pawn.Dead
                && pawn.Faction?.def?.defName == army.FactionId)
            ?? Enumerable.Empty<Pawn>();

        private static CellRect GetZone(Map map, string zoneId)
        {
            HistoricalMapSceneComponent scene = map?.GetComponent<HistoricalMapSceneComponent>();
            return scene != null && scene.TryGetZone(zoneId, out CellRect zone) ? zone : CellRect.Empty;
        }

        private static IntVec3 FindSafeCell(Map map, CellRect zone, IntVec3 preferred)
        {
            if (map == null || zone.IsEmpty) return IntVec3.Invalid;
            foreach (IntVec3 cell in zone.Cells.Where(cell => cell.InBounds(map) && cell.Standable(map) && cell.GetEdifice(map) == null
                    && !cell.GetThingList(map).Any(thing => thing is Building && thing.def.passability == Traversability.Impassable))
                .OrderBy(cell => cell.DistanceToSquared(preferred))) return cell;
            return IntVec3.Invalid;
        }

        private static void EnsureOpposition(HistoricalBattleDef def)
        {
            Faction royal = HistoricalCharacterGenerator.FindFaction(def.royalFactionId);
            Faction opposition = HistoricalCharacterGenerator.FindFaction(def.opposingFactionId);
            if (royal == null || opposition == null || royal == opposition || royal.HostileTo(opposition)) return;
            if (royal.HasGoodwill && opposition.HasGoodwill)
            {
                int adjustment = -100 - royal.BaseGoodwillWith(opposition);
                if (adjustment != 0) royal.TryAffectGoodwillWith(opposition, adjustment, false, false, null, null);
                if (!royal.HostileTo(opposition))
                    Log.Warning("[Historical RimWorld] Could not establish hostile goodwill between the HE-001 armies; battle Pawn jobs will wait for valid hostile targets.");
            }
            else royal.SetRelationDirect(opposition, FactionRelationKind.Hostile, false, "HE-001 historical battle", null);
        }
    }
}
