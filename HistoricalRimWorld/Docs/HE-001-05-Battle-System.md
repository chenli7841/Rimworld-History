# HE-001-05 — Lishan Battle Runtime

## Authority and phases

`HistoricalBattleDef` binds `HE001_LishanBattle` to the existing HE-001 event, Lishan map template, and the two existing `HistoricalArmyDef` entries. `HistoricalBattleTimelineBridge` reads `HistoricalEventManager`; it never writes to or advances the event timeline.

The runtime progresses through `Deployment`, `Engagement`, `RoyalRetreat`, `DeathScene`, `Aftermath`, and `Completed`. The existing historical stages gate those transitions. The battle instance and transition IDs are stored in `HistoricalBattleGameComponent`, so repeated map ticks and save reloads do not generate a second army or execute the same transition twice.

## Combat and retreat

Both forces use their existing faction Pawns, weapons, health, and equipment. At a low frequency the controller assigns RimWorld's ordinary `AttackMelee` / `AttackStatic` jobs against the nearest living Pawn in the opposing force. The game's own jobs and verbs resolve movement, attacks, damage, downing, and death. A light morale value is derived from the actual live membership and can issue one partial royal withdrawal; it cannot alter an HE-001 stage.

At `RoyalDefeat`, all living royal force members receive a normal `Goto` job into `RoyalRetreatCorridor`. At `KingYouDeath`, Zhou You Wang is directed toward `HistoricalDeathSceneZone`. If the player is observing that exact map, HE-001-04's fate executor is held for a bounded 1,800-tick scene window; proximity for 300 ticks completes the staging earlier. The death itself always goes through `HistoricalFateExecutor`. The existing Bo Pan fate is not relocated or given an invented death location.

At `Aftermath`, active battle jobs are stopped. Pawn references, corpses, equipment, wounds, and map changes remain owned by RimWorld's normal save system. The battle component stores only orchestration state, stable army IDs, map reference/ID, phase dates/ticks, actual member-derived casualty counts, and idempotency markers.

## Offline and late arrival

The battle controller only issues combat jobs on `Find.CurrentMap`; it does not simulate Pawn combat on an unobserved map. If the authoritative timeline reaches the defeat stage with no active battle scene, it marks an off-map royal defeat and creates no invented per-Pawn casualties. A first arrival during `RoyalDefeat` can create a retreat tableau, but arrival at or after `KingYouDeath` never creates a living replay army or replays the death scene. The scheduled fates continue through HE-001-04 independently.

## Current implementation limits

- Orders are assigned directly as vanilla Pawn jobs. This avoids using `LordJob_AssaultColony`, whose normal target is a player settlement; no custom reusable LordJob/LordToil combat framework is introduced in this phase.
- The current combat loop chooses the nearest opposing Pawn. It does not yet form shield/spear/archer formations, model line-of-sight tactics, create reinforcements, or provide the observer/player restrictions planned for HE-001-06.
- If the historical timeline has advanced past a scene before the player first loads it, the mod does not reconstruct individual battle damage or a detailed post-battle tableau.
- A RimWorld launch and in-game battle/save-reload check is still required; a successful compile cannot establish runtime AI behavior under the user's complete Mod list.
