# HE-001-03: Characters and battlefield forces

## Identity

HistoricalCharacterDef provides stable IDs that are independent of Pawn names and Pawn instance IDs. HistoricalCharacterWorldComponent stores the character-to-Pawn reference in save data; the existing genealogy component additionally records generated Pawns and the known King You–Bo Pan parent relationship. Yi Jiu and Marquis of Shen are registered as character definitions and save-state entries, but are not generated at Lishan.

## Battlefield activation

HistoricalArmyPresenceController checks the existing HE-001 event runtime and the existing HR_Map_LishanBattlefield scene. It runs only while MilitaryCrisis is the current event stage. It uses RoyalArmyApproachZone and OpposingArmyApproachZone from the scene component.

Configured groups generate 30 royal soldiers/attendants and 26 opposing Pawns. King You and Bo Pan add two more Pawns, for 58 configured battlefield Pawns. The group counts are an illustrative game scale, not historical troop estimates. Existing historical Neolithic pawn kinds provide tribal clothing and melee or bow equipment; all generated Pawns receive fixed estimated ages from XML ranges.

## Persistence and limits

Army instances and members are stored by the game's HistoricalArmyGameComponent; Pawn references use Scribe_References, while army/member records use deep Scribe. A member ID is stable within the save. Existing, dead, destroyed, or already-spawned Pawns are never replaced with a fresh member. The map itself remains owned and saved by its existing Lishan MapParent.

This phase does not command combat, rout units, force King You's death, alter historical event outcomes, create quests, or restrict player intervention. A hidden, scenario-only HR_Faction_LishanOpposition is added as an abstract event coalition because no dedicated historical adversary faction exists in the current scenario data. It has no settlements, traders, or selectable world-generation entry, and its definition does not assign an ethnic identity to those Pawns.
