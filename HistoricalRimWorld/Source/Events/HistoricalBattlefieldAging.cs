using System.Collections.Generic;
using System.Linq;
using HistoricalRimWorld.Calendar;
using HistoricalRimWorld.Scenarios;
using HistoricalRimWorld.World;
using Verse;

namespace HistoricalRimWorld.Events
{
    public enum HistoricalBattlefieldAge { FreshBattlefield, WeatheredBattlefield, OvergrownBattlefield, HistoricalRuins }

    /// <summary>
    /// Tracks the aftermath on the map itself. Live-battle remains stay native RimWorld Pawns/Things and use
    /// vanilla rot, deterioration and the existing forest regrowth systems. Only an off-map resolution receives
    /// a small, explicitly reconstructed set of anonymous equipment; it never creates casualty Pawns.
    /// </summary>
    public sealed class MapComponent_HistoricalBattlefieldAging : MapComponent
    {
        private string historicalEventId;
        private HistoricalBattlefieldAge age = HistoricalBattlefieldAge.FreshBattlefield;
        private bool liveBattleRemains;
        private bool reconstructionApplied;
        private int lastUpdateTick = -1;
        private List<Thing> reconstructedThings = new List<Thing>();

        public MapComponent_HistoricalBattlefieldAging(Map map) : base(map) { }
        public HistoricalBattlefieldAge Age => age;
        public bool ReconstructionApplied => reconstructionApplied;

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager == null || Find.TickManager.TicksGame % 2500 != 0
                || lastUpdateTick == Find.TickManager.TicksGame || Find.CurrentMap != map) return;
            lastUpdateTick = Find.TickManager.TicksGame;
            UpdateFromHistoricalEvent();
        }

        private void UpdateFromHistoricalEvent()
        {
            if (!HistoricalScenarioManager.IsScenarioActive
                || !HistoricalEventLocationService.TryGetScene(map, out HistoricalMapSceneComponent scene)
                || scene.TemplateId != "HR_Map_LishanBattlefield") return;
            HistoricalBattleDef def = DefDatabase<HistoricalBattleDef>.AllDefsListForReading
                .FirstOrDefault(item => item != null && item.historicalEventId == "HE_001_LishanCrisis");
            HistoricalBattleInstance battle = def == null
                ? null : Current.Game?.GetComponent<HistoricalBattleGameComponent>()?.FindBattle(def.battleId);
            if (battle == null || !battle.BattleCompleted) return;

            historicalEventId = battle.HistoricalEventId;
            liveBattleRemains = battle.BattleStarted && !battle.ResolvedOffMap;
            if (!liveBattleRemains && battle.ResolvedOffMap && !reconstructionApplied && !map.IsPlayerHome)
                ReconstructAnonymousEquipment(scene);

            if (!HistoricalCalendarManager.TryGetCurrentDate(out HistoricalDate today)) return;
            long start = battle.BattleEndAbsoluteDay == long.MinValue ? today.AbsoluteDay : battle.BattleEndAbsoluteDay;
            long elapsed = System.Math.Max(0, today.AbsoluteDay - start);
            HistoricalBattlefieldAge next = elapsed >= def.ruinRemainsDays ? HistoricalBattlefieldAge.HistoricalRuins
                : elapsed >= def.overgrownRemainsDays ? HistoricalBattlefieldAge.OvergrownBattlefield
                : elapsed >= def.weatheredRemainsDays ? HistoricalBattlefieldAge.WeatheredBattlefield
                : HistoricalBattlefieldAge.FreshBattlefield;
            if (next != age)
            {
                age = next;
                Log.Message("[Historical RimWorld] Lishan battlefield aftermath aged to " + age
                    + (liveBattleRemains ? " (real Pawns, corpses and equipment remain under vanilla aging and deterioration)." : " (reconstructed setting; no casualties were fabricated)."));
                if (age == HistoricalBattlefieldAge.HistoricalRuins) RemoveRemainingReconstructedEquipment();
            }
        }

        private void ReconstructAnonymousEquipment(HistoricalMapSceneComponent scene)
        {
            // A bare factual scene reconstruction: abandoned, damaged wooden clubs only. No specific soldier,
            // death, casualty total, battle sequence or unsupported weapon is asserted.
            ThingDef club = DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_Club");
            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
            if (club == null || wood == null || !scene.TryGetZone("MainBattleZone", out CellRect area)) return;
            reconstructionApplied = true;
            for (int i = 0; i < 3; i++)
            {
                IntVec3 cell;
                bool found = CellFinder.TryFindRandomCellNear(area.CenterCell, map, 12,
                    candidate => area.Contains(candidate) && candidate.Standable(map) && candidate.GetEdifice(map) == null
                        && candidate.GetFirstItem(map) == null, out cell);
                if (!found) break;
                Thing item = ThingMaker.MakeThing(club, wood);
                item.HitPoints = System.Math.Max(1, item.MaxHitPoints / 3);
                if (GenPlace.TryPlaceThing(item, cell, map, ThingPlaceMode.Near)) reconstructedThings.Add(item);
                else item.Destroy(DestroyMode.Vanish);
            }
        }

        private void RemoveRemainingReconstructedEquipment()
        {
            foreach (Thing thing in reconstructedThings ?? new List<Thing>())
                if (thing != null && !thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
            reconstructedThings?.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref historicalEventId, "historicalEventId");
            Scribe_Values.Look(ref age, "battlefieldAge", HistoricalBattlefieldAge.FreshBattlefield);
            Scribe_Values.Look(ref liveBattleRemains, "liveBattleRemains", false);
            Scribe_Values.Look(ref reconstructionApplied, "reconstructionApplied", false);
            Scribe_Values.Look(ref lastUpdateTick, "lastUpdateTick", -1);
            Scribe_Collections.Look(ref reconstructedThings, "reconstructedThings", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) reconstructedThings = reconstructedThings ?? new List<Thing>();
        }
    }
}
