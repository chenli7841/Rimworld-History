# HE-001-06 — Lishan player side quests

This stage adds four native RimWorld quests attached to HE_001_LishanCrisis.
The historical event timeline remains authoritative; side-quest success or failure
does not advance, pause, or rewrite it.

## Implemented

- Witness: a conscious, awake player colonist must stand in ObserverOuterZone
  and have unobstructed line of sight to real Pawns from both registered armies.
  Observation is accumulated in 250-tick steps and saved with one of three grades.
- Refugees: five real farmer-kind Pawns are generated once in
  OpposingArmyApproachZone and directed to CivilianEscapeZone. Rescue counts
  only when a player colonist accompanies a refugee at the exit. Once at least
  half are escorted, the player chooses to admit them individually to the player
  faction or let them remain with their original neutral faction.
- Field medical aid: targets are real wounded members of the two existing armies.
  Protected historical characters are excluded. A contextual menu lets the player
  tend a downed soldier or rescue and individually admit them; successful vanilla
  Hediff.Tended calls count toward the objective.
- Royal records: one actual HR_Thing_RoyalRecordBundle is placed in the royal
  approach zone. The quest completes only after the Thing is held by an actual
  ThingOwner (for example, a colonist inventory, storage building, or caravan).
  It remains in the game after quest completion.
- The danger message is one-shot. Construction is restricted only inside the
  royal retreat corridor and historical death-scene zone from MilitaryCrisis
  through the battle aftermath.
- Quest, map, Pawn, treatment, item, observation, and choice state use RimWorld
  Quest/GameComponent references and Scribe persistence. Creation is one-shot.

## Scope notes

- Refugees use real Pawn and Goto/escape behavior, not a simulated counter.
- Battle medical targets are limited to living, spawned, non-protected members
  of the HE-001 armies. Admitting one soldier changes only that Pawn's faction,
  never faction-wide goodwill.
- The records bundle is explicitly a fictional gameplay prop, not a claim about
  a named excavated artifact.
- When the HE-001 expiration stage is reached, unfinished side quests fail; this
  never changes the historical event result.
- Historical witness grades are stored for the later HE-001-07 archive/UI stage.

## Verification

The project was compiled against the configured RimWorld 1.6 Assembly-CSharp
and Harmony assemblies. The game itself was not launched in this environment;
runtime behavior and interactions should be confirmed in a fresh HE-001 playthrough.
