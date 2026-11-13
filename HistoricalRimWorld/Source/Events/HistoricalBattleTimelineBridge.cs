using System.Linq;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Events
{
    /// <summary>One-way bridge: HE-001 drives battle presentation; battle AI never advances HE-001.</summary>
    public static class HistoricalBattleTimelineBridge
    {
        private const string CrisisId = "HE_001_LishanCrisis";
        private const string KingId = "HR_Character_ZhouYouWang";

        public static void Tick(HistoricalBattleGameComponent component)
        {
            if (component == null || !HistoricalScenarioManager.IsScenarioActive
                || HistoricalScenarioManager.ActiveScenarioDefName != "HR_Scenario_EasternZhou_770BCE") return;

            int tick = Find.TickManager?.TicksGame ?? 0;
            foreach (HistoricalBattleDef def in DefDatabase<HistoricalBattleDef>.AllDefsListForReading
                         .Where(item => item != null && item.historicalEventId == CrisisId))
            {
                HistoricalEventRuntimeState timeline = HistoricalEventManager.GetEvent(def.historicalEventId);
                if (timeline == null) continue;
                HistoricalBattleInstance instance = component.FindBattle(def.battleId);
                if (instance != null) instance.TryRestoreMap();
                bool deploymentReached = HistoricalEventManager.HasReachedStage(def.historicalEventId, def.deploymentStageId);
                bool retreatReached = HistoricalEventManager.HasReachedStage(def.historicalEventId, def.retreatStageId);
                bool deathReached = HistoricalEventManager.HasReachedStage(def.historicalEventId, def.deathStageId);

                // If the player first arrives after the pivotal death stage, show no living replay army.
                if (instance == null && deathReached)
                {
                    instance = component.GetOrCreate(def);
                    instance.ResolveOffMap(tick);
                    Log.Message("[Historical RimWorld] " + def.battleId + " is already past its death stage; no living army or death scene was replayed.");
                    continue;
                }

                if (instance == null)
                {
                    Map activeMap = Find.CurrentMap;
                    if ((!deploymentReached || timeline.TimelineStatus != HistoricalEventRuntimeStatus.Active) && !retreatReached) continue;
                    if (activeMap == null || !HistoricalEventLocationService.TryGetScene(activeMap, out HistoricalMapSceneComponent scene)
                        || scene.TemplateId != def.mapTemplateId)
                        continue;
                    HistoricalArmyPresenceController.TryActivateForMap(activeMap);
                    instance = component.GetOrCreate(def);
                    instance.SetMap(activeMap);
                    instance.EnterPhase(HistoricalBattlePhase.Deployment, tick);
                }

                if (instance.BattleCompleted) continue;
                Map map = instance.ActiveMap;
                // A player can arrive after RoyalDefeat. Rebind to the current scene and show only the
                // authoritative retreat tableau; do not complete the event or replay the engagement.
                if ((map == null || map.Disposed) && !deathReached
                    && (deploymentReached || retreatReached)
                    && Find.CurrentMap != null
                    && HistoricalEventLocationService.TryGetScene(Find.CurrentMap, out HistoricalMapSceneComponent currentScene)
                    && currentScene.TemplateId == def.mapTemplateId)
                {
                    HistoricalArmyPresenceController.TryActivateForMap(Find.CurrentMap);
                    instance.SetMap(Find.CurrentMap);
                    map = Find.CurrentMap;
                    instance.EnterPhase(retreatReached ? HistoricalBattlePhase.RoyalRetreat
                        : instance.BattleStarted ? HistoricalBattlePhase.Engagement : HistoricalBattlePhase.Deployment, tick);
                }
                if (map != null && !map.Disposed && Find.CurrentMap != map)
                {
                    if (deathReached)
                    {
                        HistoricalBattleController.ResolveOffMap(def, instance, tick);
                        continue;
                    }
                    // Do not run combat simulation on an unobserved map. Its historic timeline continues independently.
                    continue;
                }
                if (map == null || map.Disposed)
                {
                    if (deathReached)
                        HistoricalBattleController.ResolveOffMap(def, instance, tick);
                    continue;
                }

                if (HistoricalEventManager.HasReachedStage(def.historicalEventId, def.aftermathStageId))
                {
                    HistoricalBattleController.EnterAftermath(def, instance, tick);
                    if (HistoricalEventManager.HasReachedStage(def.historicalEventId, def.completionStageId))
                        instance.Complete(tick);
                    continue;
                }
                if (HistoricalEventManager.HasReachedStage(def.historicalEventId, def.deathStageId))
                {
                    HistoricalBattleController.EnterDeathScene(def, instance, tick);
                    continue;
                }
                if (HistoricalEventManager.HasReachedStage(def.historicalEventId, def.retreatStageId))
                {
                    HistoricalBattleController.BeginRoyalRetreat(def, instance, tick);
                    continue;
                }
                if (HistoricalEventManager.HasReachedStage(def.historicalEventId, def.deploymentStageId))
                    HistoricalBattleController.BeginEngagement(def, instance, tick);
            }
        }

        /// <summary>HE-001-04 delegates only the brief, bounded player-present death staging to this bridge.</summary>
        public static bool ShouldDeferScheduledDeath(HistoricalFateDef fate)
        {
            if (fate == null || fate.historicalCharacterId != KingId || fate.fateEventId != CrisisId
                || fate.fateStageId != "KingYouDeath" || Find.CurrentMap == null) return false;
            HistoricalBattleDef def = DefDatabase<HistoricalBattleDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.kingCharacterId == KingId);
            HistoricalBattleInstance instance = def == null
                ? null : Current.Game?.GetComponent<HistoricalBattleGameComponent>()?.FindBattle(def.battleId);
            return instance != null && !instance.BattleCompleted && instance.ActiveMap == Find.CurrentMap
                && HistoricalEventManager.HasReachedStage(fate.fateEventId, fate.fateStageId)
                && !instance.KingYouFateExecuted
                && (instance.DeathSceneStartTick < 0
                    || Find.TickManager.TicksGame - instance.DeathSceneStartTick < def.deathSceneBudgetTicks);
        }
    }
}
