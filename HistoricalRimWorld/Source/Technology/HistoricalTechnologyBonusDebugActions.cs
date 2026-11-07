using LudeonTK;
using RimWorld;
using Verse;

namespace HistoricalRimWorld.Technology
{
    public static class HistoricalTechnologyBonusDebugActions
    {
        [DebugAction("Historical RimWorld", "HR: Clear Technology Bonus Cache", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ClearBonusCache()
        {
            if (!Prefs.DevMode) return;
            HistoricalTechnologyBonusService.ClearCache();
            Log.Message("[Historical RimWorld][Tech Bonus] Equipment bonus cache cleared.");
        }

        [DebugAction("Historical RimWorld", "HR: Show Selected Pawn Equipment Bonuses", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ShowSelectedPawnBonuses()
        {
            if (!Prefs.DevMode) return;
            Pawn pawn = Find.Selector?.SingleSelectedThing as Pawn;
            if (pawn == null)
            {
                Log.Message("[Historical RimWorld][Tech Bonus] Select one pawn to inspect its faction equipment bonuses.");
                return;
            }

            if (pawn.equipment?.Primary != null)
            {
                Thing weapon = pawn.equipment.Primary;
                float damage = HistoricalTechnologyBonusService.GetBonus(pawn.Faction, weapon,
                    HistoricalTechnologyBonusType.MeleeDamage, equipped: true);
                float penetration = HistoricalTechnologyBonusService.GetBonus(pawn.Faction, weapon,
                    HistoricalTechnologyBonusType.MeleeArmorPenetration, equipped: true, sharpTool: true);
                Log.Message("[Historical RimWorld][Tech Bonus] Pawn=" + pawn.LabelShort + "; Faction="
                    + pawn.Faction?.Name + "; Weapon=" + weapon.def.defName + "; MeleeDamage="
                    + damage.ToString("P1") + "; MeleeArmorPenetration=" + penetration.ToString("P1"));
            }
            if (pawn.apparel?.WornApparel != null)
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                float sharp = HistoricalTechnologyBonusService.GetBonus(pawn.Faction, apparel,
                    HistoricalTechnologyBonusType.ArmorSharp, equipped: true);
                float blunt = HistoricalTechnologyBonusService.GetBonus(pawn.Faction, apparel,
                    HistoricalTechnologyBonusType.ArmorBlunt, equipped: true);
                Log.Message("[Historical RimWorld][Tech Bonus] Pawn=" + pawn.LabelShort + "; Apparel="
                    + apparel.def.defName + "; SharpArmor=" + sharp.ToString("P1") + "; BluntArmor=" + blunt.ToString("P1"));
            }
        }
    }
}
