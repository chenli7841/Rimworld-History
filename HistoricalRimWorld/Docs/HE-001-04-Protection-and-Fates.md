# HE-001-04 — Historical character protection and fate execution

## Implemented scope

The rules are data-driven through `HistoricalCharacterProtectionDef` and `HistoricalFateDef`. They identify Pawns only through the stable-ID registry added in HE-001-03; display names are never used as identity keys. A runtime Pawn-to-ID index keeps Harmony checks constant-time for ordinary Pawns.

| Character | Base protection | Configured fate |
| --- | --- | --- |
| Zhou You Wang | FateProtected; SceneLocked from `MilitaryCrisis` until `KingYouDeath` | ScheduledDeath at HE-001 `KingYouDeath` |
| Bo Pan | FateProtected | ScheduledDeath at the same historical stage; no death location is specified |
| Yi Jiu | InteractionProtected, FateProtected through a ScheduledSurvival fate | Survives HE-001 and remains available to later events |
| Shen Hou | InteractionProtected | No death date or battlefield appearance is asserted |

Ordinary soldiers receive no fate protection.

## Guard behavior

- Player-directed melee/ranged targeting of FateProtected characters is rejected in both attack and verb targeting entry points.
- Arrest validation and arrest order execution are rejected; recruitability is suppressed; prisoner/slave status changes, faction changes, caravan transfer, world-pawn transfer, and map exits are guarded.
- Before a configured fate is committed, ordinary damage to FateProtected characters is absorbed and `Pawn.Kill`/lethal-threshold death is rejected. This also covers explosions and fire damage that reach the Pawn health path.
- A protection rule only applies to the registered historical Pawn. It does not make ordinary Pawns invulnerable.

## Fate execution and saves

`HistoricalFateGameComponent` polls configured fate definitions at a low frequency and only executes when the existing HE-001 event runtime has recorded the configured stage. It runs even when no Lishan map is loaded. A death record is persisted as `Executing` before calling the real `Pawn.Kill`; after a save/load, an already-dead Pawn is committed without a second kill. If no Pawn was ever generated, the character registry is marked historically dead so later generation is refused. The Pawn reference remains owned by the existing character registry; the fate component stores only outcome metadata.

Yi Jiu's scheduled survival remains fate-protected so he cannot be removed before HE-002. Later historical event code can use `HistoricalCharacterProtectionService.TryBeginHistoricalRelocation(pawn, eventId, stageId)` after its stage is recorded, and must call `EndHistoricalRelocation` in `finally`. This narrowly authorizes the scripted transition without opening player caravan or transport commands.

HE-001 already records the `KingYouDeath` chronicle stage, so fate execution does not add a duplicate chronicle entry. Its record stores the event/stage identifiers, execution date, Pawn thing ID when available, and recoverable status.

## Deferred work and limits

Actual troop combat, retreat, battlefield movement, player-observer restrictions, rescue quests, and aftermath are outside this phase. Protection intentionally prevents all pre-fate damage to keep the key Pawn viable; it does not simulate wounds. Third-party Mods that mutate health or move Pawns through private systems which bypass the patched RimWorld APIs may need compatibility patches. Runtime behavior still requires an in-game save/load and event-stage check.
