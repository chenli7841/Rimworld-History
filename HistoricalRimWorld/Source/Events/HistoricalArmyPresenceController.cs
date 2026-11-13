using System;
using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Characters;
using HistoricalRimWorld.Genealogy;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Events
{
    public static class HistoricalArmyPresenceController
    {
        private const string CrisisEventId = "HE_001_LishanCrisis";
        private const string ActivationStageId = "MilitaryCrisis";
        private const string LishanTemplateId = "HR_Map_LishanBattlefield";
        private static readonly HashSet<string> ReportedErrors = new HashSet<string>(StringComparer.Ordinal);
        public static void TryActivateForMap(Map map)
        {
            if (map == null || !HistoricalScenarioManager.IsScenarioActive
                || HistoricalScenarioManager.ActiveScenarioDefName != "HR_Scenario_EasternZhou_770BCE") return;
            if (!HistoricalEventLocationService.TryGetScene(map, out HistoricalMapSceneComponent scene)
                || scene.TemplateId != LishanTemplateId) return;

            HistoricalEventRuntimeState eventState = HistoricalEventManager.GetEvent(CrisisEventId);
            // Only the active MilitaryCrisis stage creates a battle tableau. In particular,
            // an older save already at RoyalDefeat/KingYouDeath is never repopulated.
            if (eventState == null || eventState.CurrentStageId != ActivationStageId
                || eventState.TimelineStatus != HistoricalEventRuntimeStatus.Active) return;

            HistoricalArmyGameComponent component = Current.Game?.GetComponent<HistoricalArmyGameComponent>();
            if (component == null) return;
            List<HistoricalArmyDef> armyDefs = DefDatabase<HistoricalArmyDef>.AllDefsListForReading
                .Where(item => item != null && item.historicalEventId == CrisisEventId
                    && item.historicalScenarioId == HistoricalScenarioManager.ActiveScenarioDefName)
                .OrderBy(item => item.defName, StringComparer.Ordinal).ToList();
            if (armyDefs.Count == 0) return;
            bool generatedAnyPawn = false;

            HistoricalCharacterWorldComponent characters = Current.Game.GetComponent<HistoricalCharacterWorldComponent>();
            foreach (HistoricalCharacterDef character in DefDatabase<HistoricalCharacterDef>.AllDefsListForReading
                         .Where(item => item != null && item.relatedEventId == CrisisEventId
                             && item.historicalScenarioId == HistoricalScenarioManager.ActiveScenarioDefName))
                characters?.GetOrRegister(character);

            // Resolve the key royal Pawns first; the stable registry is shared with later events.
            HistoricalArmyDef royalDef = armyDefs.FirstOrDefault(item => item.factionId == "HR_Faction_EasternZhou");
            HistoricalArmyRuntime royalState = royalDef == null ? null : component.GetOrCreateArmy(royalDef);
            if (royalState != null) royalState.SetMap(map);

            foreach (HistoricalCharacterDef character in DefDatabase<HistoricalCharacterDef>.AllDefsListForReading
                         .Where(item => item != null && item.generateOnBattlefield
                             && item.relatedEventId == CrisisEventId
                             && item.historicalScenarioId == HistoricalScenarioManager.ActiveScenarioDefName)
                         .OrderBy(item => item.historicalCharacterId, StringComparer.Ordinal))
            {
                if (!scene.TryGetZone("RoyalArmyApproachZone", out CellRect royalZone)) return;
                HistoricalCharacterState savedCharacter = characters?.FindState(character.historicalCharacterId);
                bool alreadyHadPawn = savedCharacter?.Pawn != null;
                IntVec3 cell = FindDeploymentCell(map, royalZone, StableHash(character.historicalCharacterId), 0.5f);
                if (!cell.IsValid) continue;
                Pawn pawn = HistoricalCharacterGenerator.GetOrGenerate(character, map, cell);
                if (pawn == null || royalState == null) continue;
                if (!alreadyHadPawn) generatedAnyPawn = true;
                HistoricalArmyMemberState member = royalState.GetOrCreateMember(character.historicalCharacterId,
                    "RoyalHousehold", character.historicalRole, character.historicalCharacterId);
                if (member.Pawn == null) member.BindPawn(pawn);
            }

            foreach (HistoricalArmyDef def in armyDefs)
            {
                HistoricalArmyRuntime state = component.GetOrCreateArmy(def);
                if (state == null) continue;
                state.SetGenerationStarted();
                state.SetMap(map);
                if (!scene.TryGetZone(def.deploymentZoneId, out CellRect zone))
                {
                    LogOnce("zone:" + def.armyId, "[Historical RimWorld] Army " + def.armyId + " cannot deploy: scene zone " + def.deploymentZoneId + " is missing.");
                    continue;
                }
                if (!scene.TryGetZone("MainBattleZone", out CellRect battleZone))
                {
                    LogOnce("battleZone:" + def.armyId, "[Historical RimWorld] Army " + def.armyId + " cannot form up: MainBattleZone is missing.");
                    continue;
                }

                Faction faction = HistoricalCharacterGenerator.FindFaction(def.factionId);
                if (faction == null)
                {
                    LogOnce("faction:" + def.armyId, "[Historical RimWorld] Army " + def.armyId + " cannot deploy: faction " + def.factionId + " is not present in this world.");
                    continue;
                }

                foreach (HistoricalUnitGroupDef group in def.unitGroups ?? new List<HistoricalUnitGroupDef>())
                {
                    for (int index = 0; index < group.count; index++)
                    {
                        string memberId = def.armyId + "_" + group.groupId + "_" + (index + 1).ToString("D2");
                        HistoricalArmyMemberState member = state.GetOrCreateMember(memberId, group.groupId, group.unitRole);
                        if (member.Pawn != null)
                        {
                            SpawnExistingIfSafe(member.Pawn, map, zone, memberId);
                            continue;
                        }
                        if (member.GenerationAttempted) continue;
                        PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(group.pawnKindDefName);
                        if (kind == null)
                        {
                            LogOnce("kind:" + group.pawnKindDefName, "[Historical RimWorld] Army unit kind " + group.pawnKindDefName + " is missing for " + memberId + ".");
                            continue;
                        }
                        IntVec3 cell = FindDeploymentCell(map, zone, StableHash(memberId), group.deploymentDepth, battleZone);
                        if (!cell.IsValid) continue;

                        member.BeginGeneration();
                        try
                        {
                            int age = Rand.RangeInclusive(group.minimumAge, group.maximumAge);
                            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction,
                                fixedBiologicalAge: age, fixedChronologicalAge: age,
                                forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                                mustBeCapableOfViolence: true, forceNoIdeo: true));
                            if (pawn == null)
                            {
                                member.ResetFailedGeneration();
                                continue;
                            }
                            member.BindPawn(pawn);
                            GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
                            generatedAnyPawn = true;
                            HistoricalCharacterGenerator.RegisterGenealogy(pawn);
                        }
                        catch (Exception exception)
                        {
                            member.ResetFailedGeneration();
                            Log.Error("[Historical RimWorld] Failed to generate army member " + memberId + ": " + exception);
                        }
                    }
                }
            }

            foreach (HistoricalCharacterDef character in DefDatabase<HistoricalCharacterDef>.AllDefsListForReading
                         .Where(item => item != null && item.historicalCharacterId == "HR_Character_BoPan"))
            {
                Pawn father = HistoricalCharacterRegistry.GetPawn("HR_Character_ZhouYouWang");
                Pawn son = HistoricalCharacterRegistry.GetPawn("HR_Character_BoPan");
                if (father != null && son != null && !father.relations.DirectRelationExists(PawnRelationDefOf.Parent, son))
                    father.relations.AddDirectRelation(PawnRelationDefOf.Parent, son);
                if (father != null && son != null
                    && HistoricalGenealogyManager.TryGetPersonId(father, out string fatherId)
                    && HistoricalGenealogyManager.TryGetPersonId(son, out string sonId)
                    && !HistoricalGenealogyManager.RelationsFor(sonId).Any(item => item != null
                        && item.Type == HistoricalKinshipType.BiologicalParent
                        && item.PersonIdA == fatherId && item.PersonIdB == sonId))
                    HistoricalGenealogyManager.TryAddRelation(fatherId, sonId, HistoricalKinshipType.BiologicalParent, out _);
            }

            foreach (HistoricalArmyDef def in armyDefs)
            {
                HistoricalArmyRuntime state = component.FindArmy(def.armyId);
                if (state == null) continue;
                state.SetMap(map);
                bool allConfiguredGroupsHavePawns = (def.unitGroups ?? new List<HistoricalUnitGroupDef>())
                    .All(group => state.Members.Count(member => member != null && member.GroupId == group.groupId
                        && member.Pawn != null) >= group.count);
                if (!state.DeploymentComplete && allConfiguredGroupsHavePawns)
                    state.SetDeploymentComplete();
            }

            if (generatedAnyPawn)
                Log.Message("[Historical RimWorld] Activated HE-001 battlefield presence on the existing Lishan map. Army member identities and Pawn references are persistent; no combat outcome was applied.");
        }

        private static void SpawnExistingIfSafe(Pawn pawn, Map map, CellRect zone, string id)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Spawned) return;
            IntVec3 cell = FindDeploymentCell(map, zone, StableHash(id), 0.5f);
            if (cell.IsValid) GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);
        }

        private static IntVec3 FindDeploymentCell(Map map, CellRect zone, int seed, float deploymentDepth,
            CellRect battleZone = default(CellRect))
        {
            if (map == null || zone.IsEmpty) return IntVec3.Invalid;
            IntVec3 center = zone.CenterCell;
            IntVec3 battleCenter = battleZone.IsEmpty ? center : battleZone.CenterCell;
            float directionX = battleCenter.x - center.x;
            float directionZ = battleCenter.z - center.z;
            float directionLength = (float)Math.Sqrt(directionX * directionX + directionZ * directionZ);
            if (directionLength > 0.001f) { directionX /= directionLength; directionZ /= directionLength; }
            float halfSpan = Math.Abs(directionX) * (zone.Width - 1) * 0.5f
                + Math.Abs(directionZ) * (zone.Height - 1) * 0.5f;
            float frontOffset = (deploymentDepth - 0.5f) * 2f * halfSpan;
            IntVec3 preferred = new IntVec3(
                Math.Max(zone.minX, Math.Min(zone.maxX, (int)Math.Round(center.x + directionX * frontOffset))),
                0,
                Math.Max(zone.minZ, Math.Min(zone.maxZ, (int)Math.Round(center.z + directionZ * frontOffset))));
            IEnumerable<IntVec3> cells = zone.Cells
                .Where(cell => cell.InBounds(map) && cell.Standable(map)
                    && cell.GetEdifice(map) == null
                    && !cell.GetThingList(map).Any(thing => !(thing is Plant) && !(thing is Filth)))
                .OrderBy(cell => Math.Abs(cell.x - preferred.x) + Math.Abs(cell.z - preferred.z))
                .ThenBy(cell => StableHash(seed + ":" + cell.x + ":" + cell.z));
            foreach (IntVec3 cell in cells) return cell;
            return IntVec3.Invalid;
        }

        private static int StableHash(string value)
        {
            unchecked { return (value ?? string.Empty).Aggregate(17, (hash, character) => hash * 31 + character); }
        }

        private static void LogOnce(string key, string message)
        {
            if (ReportedErrors.Add(key)) Log.Error(message);
        }
    }
}
